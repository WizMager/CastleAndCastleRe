using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
partial struct AnimationApplicationSystem
{

//=================================================================================================================//

[BurstCompile]
partial struct ApplyAnimationToSkinnedMeshJob: IJobEntity
{
	[ReadOnly]
	public ComponentLookup<RigDefinitionComponent> rigDefinitionLookup;
	[ReadOnly]
	public NativeList<BoneTransform> boneTransforms;
	[ReadOnly]
	public NativeParallelHashMap<Entity, int2> entityToDataOffsetMap;
	[ReadOnly]
	public NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> rigToSkinnedMeshRemapTables;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	static public Hash128 CalculateBoneRemapTableHash(in BlobAssetReference<SkinnedMeshInfoBlob> skinnedMesh, in BlobAssetReference<RigDefinitionBlob> rigDef)
	{
		var rv = new Hash128(skinnedMesh.Value.hash.Value.x, skinnedMesh.Value.hash.Value.y, rigDef.Value.hash.Value.z, rigDef.Value.hash.Value.w);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	ref BoneRemapTableBlob GetBoneRemapTable(in BlobAssetReference<SkinnedMeshInfoBlob> skinnedMesh, in BlobAssetReference<RigDefinitionBlob> rigDef)
	{
		var h = CalculateBoneRemapTableHash(skinnedMesh, rigDef);
		return ref rigToSkinnedMeshRemapTables[h].Value;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	SkinMatrix MakeSkinMatrixForBone(ref SkinnedMeshBoneInfo boneInfo, in float4x4 boneXForm, in float4x4 entityToRootBoneTransform)
	{
		var boneTransformMatrix = math.mul(entityToRootBoneTransform, boneXForm);
		boneTransformMatrix = math.mul(boneTransformMatrix, boneInfo.bindPose);

		var skinMatrix = new SkinMatrix() { Value = new float3x4(boneTransformMatrix.c0.xyz, boneTransformMatrix.c1.xyz, boneTransformMatrix.c2.xyz, boneTransformMatrix.c3.xyz) };
		return skinMatrix;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	void Execute(in AnimatedSkinnedMeshComponent animatedSkinnedMesh, ref DynamicBuffer<SkinMatrix> outSkinMatricesBuf)
	{
		var rigEntity = animatedSkinnedMesh.animatedRigEntity;

		if (!rigDefinitionLookup.TryGetComponent(rigEntity, out var rigDef))
			return;

		if (!entityToDataOffsetMap.TryGetValue(rigEntity, out var boneDataOffset))
			return;

		ref var boneRemapTable = ref GetBoneRemapTable(animatedSkinnedMesh.boneInfos, rigDef.rigBlob);

		ref var rigBones = ref rigDef.rigBlob.Value.bones;

		var skinMeshBonesInfo = animatedSkinnedMesh.boneInfos;
		var absoluteBoneTransforms = RuntimeAnimationData.GetAnimationDataForRigRO(boneTransforms, boneDataOffset.x, rigBones.Length);

		var rootBoneIndex = math.max(0, animatedSkinnedMesh.rootBoneIndexInRig);
		var boneObjLocalPose = absoluteBoneTransforms[rootBoneIndex];
		var entityToRootBoneTransform = math.inverse(boneObjLocalPose.ToFloat4x4());

		// Iterate over all animated bones and set pose for corresponding skin matrices
		for (int animationBoneIndex = 0; animationBoneIndex < rigBones.Length; ++animationBoneIndex)
		{
			var skinnedMeshBoneIndex = boneRemapTable.rigBoneToSkinnedMeshBoneRemapIndices[animationBoneIndex];

			//	Skip bone if it is not present in skinned mesh
			if (skinnedMeshBoneIndex < 0)
				continue;

			var absBonePose = absoluteBoneTransforms[animationBoneIndex];
			var boneXForm = absBonePose.ToFloat4x4();

			ref var boneInfo = ref skinMeshBonesInfo.Value.bones[skinnedMeshBoneIndex];
			var skinMatrix = MakeSkinMatrixForBone(ref boneInfo, boneXForm, entityToRootBoneTransform);
			outSkinMatricesBuf[skinnedMeshBoneIndex] = skinMatrix;
		}
	}
}

//=================================================================================================================//

[BurstCompile]
partial struct PropagateBoneTransformToEntityTRSJob: IJobEntity
{
	[ReadOnly]
	public ComponentLookup<RigDefinitionComponent> rigDefLookup;
	[ReadOnly]
	public NativeList<BoneTransform> boneTransforms;
	[ReadOnly]
	public NativeParallelHashMap<Entity, int2> entityToDataOffsetMap;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute(in AnimatorEntityRefComponent animatorRef, ref LocalTransform lt)
	{
		if (!rigDefLookup.TryGetComponent(animatorRef.animatorEntity, out var rigDef))
			return;

		var boneData = RuntimeAnimationData.GetAnimationDataForRigRO(boneTransforms, entityToDataOffsetMap, rigDef, animatorRef.animatorEntity);
		if (boneData.IsEmpty)
			return;
		
		lt = boneData[animatorRef.boneIndexInAnimationRig].ToLocalTransformComponent();
	}
}

//=================================================================================================================//

[BurstCompile]
struct FillRigToSkinBonesRemapTableCacheJob: IJob
{
	[ReadOnly]
	public ComponentLookup<RigDefinitionComponent> rigDefinitionArr;
	[ReadOnly]
	public NativeList<AnimatedSkinnedMeshComponent> skinnedMeshes;
	public NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> rigToSkinnedMeshRemapTables;

#if RUKHANKA_DEBUG_INFO
	public bool doLogging;
#endif

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	public void Execute()
	{
		for (int l = 0; l < skinnedMeshes.Length; ++l)
		{
			var sm = skinnedMeshes[l];
			if (!rigDefinitionArr.TryGetComponent(sm.animatedRigEntity, out var rigDef))
				continue;

			//	Try cache first
			var h = ApplyAnimationToSkinnedMeshJob.CalculateBoneRemapTableHash(sm.boneInfos, rigDef.rigBlob);
			if (rigToSkinnedMeshRemapTables.TryGetValue(h, out var rv))
				continue;

			//	Compute new remap table
			var bb = new BlobBuilder(Allocator.Temp);
			ref var brt = ref bb.ConstructRoot<BoneRemapTableBlob>();

		#if RUKHANKA_DEBUG_INFO
			ref var rnd = ref rigDef.rigBlob.Value.name;
			ref var snd = ref sm.boneInfos.Value.skeletonName;
			if (doLogging)
				Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Creating rig '{rnd.ToFixedString()}' to skinned mesh '{snd.ToFixedString()}' remap table");
		#endif
			
			var bba = bb.Allocate(ref brt.rigBoneToSkinnedMeshBoneRemapIndices, rigDef.rigBlob.Value.bones.Length);
			for (int i = 0; i < bba.Length; ++i)
			{
				bba[i] = -1;
				ref var rb = ref rigDef.rigBlob.Value.bones[i];
				var rbHash =  rb.hash;
				
				for (int j = 0; j < sm.boneInfos.Value.bones.Length; ++j)
				{
					ref var bn = ref sm.boneInfos.Value.bones[j];
					var bnHash = bn.hash;

					if (bnHash == rbHash)
					{ 
						bba[i] = j;
					#if RUKHANKA_DEBUG_INFO
						if (doLogging)
							Debug.Log($"[FillRigToSkinBonesRemapTableCacheJob] Remap {rb.name.ToFixedString()}->{bn.name.ToFixedString()} : {i} -> {j}");
					#endif
					}
				}
			}
			rv = bb.CreateBlobAssetReference<BoneRemapTableBlob>(Allocator.Persistent);
			rigToSkinnedMeshRemapTables.Add(h, rv);
		}
	}
}

}
}
