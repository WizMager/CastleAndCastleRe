using Unity.Burst;
using Unity.Collections;
using Unity.Deformations;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[DisableAutoCreation]
[RequireMatchingQueriesForUpdate]
partial struct AnimationApplicationSystem: ISystem
{
	private EntityQuery
		boneObjectEntitiesWithParentQuery,
		boneObjectEntitiesNoParentQuery;

	NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>> rigToSkinnedMeshRemapTables;

/////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnCreate(ref SystemState ss)
	{
		using var eqb0 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<AnimatorEntityRefComponent, Parent>()
		.WithAllRW<LocalTransform>();
		boneObjectEntitiesWithParentQuery = ss.GetEntityQuery(eqb0);

		using var eqb1 = new EntityQueryBuilder(Allocator.Temp)
		.WithAll<AnimatorEntityRefComponent>()
		.WithNone<Parent>()
		.WithAllRW<LocalTransform>();
		boneObjectEntitiesNoParentQuery = ss.GetEntityQuery(eqb1);

		rigToSkinnedMeshRemapTables = new NativeParallelHashMap<Hash128, BlobAssetReference<BoneRemapTableBlob>>(128, Allocator.Persistent);
	}
	
/////////////////////////////////////////////////////////////////////////////////

	[BurstCompile]
	public void OnDestroy(ref SystemState ss)
	{
		rigToSkinnedMeshRemapTables.Dispose();
	}

/////////////////////////////////////////////////////////////////////////////////

    [BurstCompile]
    public void OnUpdate(ref SystemState ss)
    {
		ref var runtimeData = ref SystemAPI.GetSingletonRW<RuntimeAnimationData>().ValueRW;

		var fillRigToSkinnedMeshRemapTablesJH = FillRigToSkinBonesRemapTableCache(ref ss, ss.Dependency);

		//	Propagate local animated transforms to the entities with and without parents
		var propagateTRSToEntitiesWithParentsJH = PropagateAnimatedBonesToEntitiesTRS(ref ss, runtimeData, boneObjectEntitiesWithParentQuery, true, ss.Dependency);
		var propagateTRSToEntitiesNoParentsJH = PropagateAnimatedBonesToEntitiesTRS(ref ss, runtimeData, boneObjectEntitiesNoParentQuery, false, propagateTRSToEntitiesWithParentsJH);

		//	Make corresponding skin matrices for all skinned meshes
		var jh = JobHandle.CombineDependencies(fillRigToSkinnedMeshRemapTablesJH, propagateTRSToEntitiesNoParentsJH);
		var applySkinJobHandle = ApplySkinning(ref ss, runtimeData, jh);

		ss.Dependency = applySkinJobHandle;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle FillRigToSkinBonesRemapTableCache(ref SystemState ss, JobHandle dependsOn)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);

	#if RUKHANKA_DEBUG_INFO
		SystemAPI.TryGetSingleton<DebugConfigurationComponent>(out var dc);
	#endif
		var skinnedMeshWithAnimatorQuery = SystemAPI.QueryBuilder().WithAll<SkinMatrix, AnimatedSkinnedMeshComponent>().Build();
		var skinnedMeshes = skinnedMeshWithAnimatorQuery.ToComponentDataListAsync<AnimatedSkinnedMeshComponent>(ss.WorldUpdateAllocator, dependsOn, out var skinnedMeshFromQueryJH);

		var j = new FillRigToSkinBonesRemapTableCacheJob()
		{
			rigDefinitionArr = rigDefinitionComponentLookup,
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables,
			skinnedMeshes = skinnedMeshes,
		#if RUKHANKA_DEBUG_INFO
			doLogging = dc.logAnimationCalculationProcesses
		#endif
		};

		var rv = j.Schedule(skinnedMeshFromQueryJH);
		return rv;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle PropagateAnimatedBonesToEntitiesTRS(ref SystemState ss, in RuntimeAnimationData runtimeData, EntityQuery eq, bool withParents, JobHandle dependsOn)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);

		var propagateAnimationJob = new PropagateBoneTransformToEntityTRSJob()
		{
			entityToDataOffsetMap = runtimeData.entityToDataOffsetMap,
			boneTransforms = withParents ? runtimeData.animatedBonesBuffer : runtimeData.worldSpaceBonesBuffer,
			rigDefLookup = rigDefinitionComponentLookup,
		};

		var jh = propagateAnimationJob.ScheduleParallel(eq, dependsOn);
		return jh;
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	JobHandle ApplySkinning(ref SystemState ss, in RuntimeAnimationData runtimeData, JobHandle dependsOn)
	{
		var rigDefinitionComponentLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);

		var animationApplyJob = new ApplyAnimationToSkinnedMeshJob()
		{
			boneTransforms = runtimeData.worldSpaceBonesBuffer,
			entityToDataOffsetMap = runtimeData.entityToDataOffsetMap,
			rigDefinitionLookup = rigDefinitionComponentLookup,
			rigToSkinnedMeshRemapTables = rigToSkinnedMeshRemapTables,
		};

		var jh = animationApplyJob.ScheduleParallel(dependsOn);
		return jh;
	}
}
}
