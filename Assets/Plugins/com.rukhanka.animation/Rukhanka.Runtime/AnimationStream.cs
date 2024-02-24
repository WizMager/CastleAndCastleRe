using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Assertions;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
public struct AnimationStream: IDisposable
{
    public int boneOffset;
    public int flagsOffset;
    public RuntimeAnimationData runtimeData;
    public BlobAssetReference<RigDefinitionBlob> rigBlob;
    //  World pose dirty flags
    public NativeBitArray worldPoseDirtyFlags;
    
/////////////////////////////////////////////////////////////////////////////////

    public static AnimationStream Create(RuntimeAnimationData rd, Entity rigEntity, in RigDefinitionComponent rdc)
    {
        var offsets = RuntimeAnimationData.CalculateBufferOffset(rd.entityToDataOffsetMap, rigEntity);
        var rv = new AnimationStream()
        {
            boneOffset = offsets.x,
            flagsOffset = offsets.y,
            runtimeData = rd,
            rigBlob = rdc.rigBlob,
            worldPoseDirtyFlags = new NativeBitArray(rdc.rigBlob.Value.bones.Length, Allocator.Temp)
        };
        return rv;
    }
    
/////////////////////////////////////////////////////////////////////////////////

    public void Dispose()
    {
        RebuildOutdatedBonePoses(-1);
    }

/////////////////////////////////////////////////////////////////////////////////

    public BoneTransform GetLocalPose(int boneIndex) => runtimeData.animatedBonesBuffer[boneOffset + boneIndex];
    public float3 GetLocalPosition(int boneIndex) => GetLocalPose(boneIndex).pos;
    public quaternion GetLocalRotation(int boneIndex) => GetLocalPose(boneIndex).rot;

/////////////////////////////////////////////////////////////////////////////////

    public BoneTransform GetWorldPose(int boneIndex)
    {
        var isWorldPoseDirty = worldPoseDirtyFlags.IsSet(boneIndex);
        if (isWorldPoseDirty)
            RebuildOutdatedBonePoses(boneIndex);
        
        return runtimeData.worldSpaceBonesBuffer[boneOffset + boneIndex];   
    }
    public float3 GetWorldPosition(int boneIndex) => GetWorldPose(boneIndex).pos;
    public quaternion GetWorldRotation(int boneIndex) => GetWorldPose(boneIndex).rot;
    
/////////////////////////////////////////////////////////////////////////////////

    BoneTransform GetParentBoneWorldPose(int boneIndex)
    {
        var parentBoneIndex = rigBlob.Value.bones[boneIndex].parentBoneIndex;
        var parentWorldPose = BoneTransform.Identity();
        if (parentBoneIndex >= 0)
        {
            if (worldPoseDirtyFlags.IsSet(parentBoneIndex))
                RebuildOutdatedBonePoses(parentBoneIndex);
            parentWorldPose = runtimeData.worldSpaceBonesBuffer[parentBoneIndex + boneOffset];
        }

        return parentWorldPose;
    }

/////////////////////////////////////////////////////////////////////////////////

    public void SetWorldPose(int boneIndex, in BoneTransform bt)
    {
        var absBoneIndex = boneOffset + boneIndex;
        runtimeData.worldSpaceBonesBuffer[absBoneIndex] = bt;
        
        var parentWorldPose = GetParentBoneWorldPose(boneIndex);
        
        ref var boneLocalPose = ref runtimeData.animatedBonesBuffer.ElementAt(absBoneIndex);
        boneLocalPose = BoneTransform.Multiply(BoneTransform.Inverse(parentWorldPose), bt);
        
        MarkChildrenWorldPosesAsDirty(boneIndex);
    }
    
/////////////////////////////////////////////////////////////////////////////////

    public void SetWorldPosition(int boneIndex, float3 pos)
    {
        var absBoneIndex = boneOffset + boneIndex;
        ref var curPose = ref runtimeData.worldSpaceBonesBuffer.ElementAt(absBoneIndex);
        curPose.pos = pos;
        
        var parentWorldPosition = GetParentBoneWorldPose(boneIndex).pos;
        
        ref var boneLocalPose = ref runtimeData.animatedBonesBuffer.ElementAt(absBoneIndex);
        boneLocalPose.pos = pos - parentWorldPosition;
        
        MarkChildrenWorldPosesAsDirty(boneIndex);
    }
    
/////////////////////////////////////////////////////////////////////////////////

    public void SetWorldRotation(int boneIndex, quaternion rot)
    {
        var absBoneIndex = boneOffset + boneIndex;
        ref var boneWorldPose = ref runtimeData.worldSpaceBonesBuffer.ElementAt(absBoneIndex);
        boneWorldPose.rot = rot;
        
        var parentWorldRot = GetParentBoneWorldPose(boneIndex).rot;

        ref var boneLocalPose = ref runtimeData.animatedBonesBuffer.ElementAt(absBoneIndex);
        boneLocalPose.rot = math.mul(math.conjugate(parentWorldRot), boneWorldPose.rot);
        
        MarkChildrenWorldPosesAsDirty(boneIndex);
    }

/////////////////////////////////////////////////////////////////////////////////

    public void SetLocalPose(int boneIndex, in BoneTransform bt)
    {
        var absBoneIndex = boneOffset + boneIndex;
        runtimeData.animatedBonesBuffer[absBoneIndex] = bt;
        MarkChildrenWorldPosesAsDirty(boneIndex);
        worldPoseDirtyFlags.Set(boneIndex, true);
    }
    
/////////////////////////////////////////////////////////////////////////////////

    public void SetLocalPosition(int boneIndex, float3 pos)
    {
        var absBoneIndex = boneOffset + boneIndex;
        ref var curPose = ref runtimeData.animatedBonesBuffer.ElementAt(absBoneIndex);
        curPose.pos = pos;
        MarkChildrenWorldPosesAsDirty(boneIndex);
        worldPoseDirtyFlags.Set(boneIndex, true);
    }
    
/////////////////////////////////////////////////////////////////////////////////

    public void SetLocalRotation(int boneIndex, quaternion rot)
    {
        var absBoneIndex = boneOffset + boneIndex;
        ref var curPose = ref runtimeData.animatedBonesBuffer.ElementAt(absBoneIndex);
        curPose.rot = rot;
        MarkChildrenWorldPosesAsDirty(boneIndex);
        worldPoseDirtyFlags.Set(boneIndex, true);
    }
    
/////////////////////////////////////////////////////////////////////////////////

    void MarkChildrenWorldPosesAsDirty(int rootBoneIndex)
    {
        for (var i = rootBoneIndex + 1; i < rigBlob.Value.bones.Length; ++i)
        {
            ref var bone = ref rigBlob.Value.bones[i];
            if (bone.parentBoneIndex == rootBoneIndex)
            {
                worldPoseDirtyFlags.Set(i, true);
                MarkChildrenWorldPosesAsDirty(i);
            }
        }
    }

/////////////////////////////////////////////////////////////////////////////////

    void RebuildOutdatedBonePoses(int interestedBoneIndex)
    {
        var endBoneIndex = math.select(rigBlob.Value.bones.Length - 1, interestedBoneIndex, interestedBoneIndex >= 0);
        for (var i = 0; i <= endBoneIndex; ++i)
        {
            var isWorldPoseDirty = worldPoseDirtyFlags.IsSet(i);
            if (!isWorldPoseDirty)
                continue;
            
            var absBoneIndex = boneOffset + i;
            ref var rigBone = ref rigBlob.Value.bones[i];
            var boneLocalPose = runtimeData.animatedBonesBuffer[absBoneIndex];

            var parentBoneWorldPose = BoneTransform.Identity();
            if (rigBone.parentBoneIndex >= 0)
            {
                parentBoneWorldPose = runtimeData.worldSpaceBonesBuffer[boneOffset + rigBone.parentBoneIndex];
            }

            var worldPose = BoneTransform.Multiply(parentBoneWorldPose, boneLocalPose);
            runtimeData.worldSpaceBonesBuffer[absBoneIndex] = worldPose;
        }
        worldPoseDirtyFlags.SetBits(0, false, endBoneIndex + 1);
    }

/////////////////////////////////////////////////////////////////////////////////

    public AnimationTransformFlags GetAnimationTransformFlagsRO()
    {
        return AnimationTransformFlags.CreateFromBufferRO(runtimeData.boneTransformFlagsHolderArr, flagsOffset, rigBlob.Value.bones.Length);
    }
    
/////////////////////////////////////////////////////////////////////////////////

    public AnimationTransformFlags GetAnimationTransformFlagsRW()
    {
        return AnimationTransformFlags.CreateFromBufferRW(runtimeData.boneTransformFlagsHolderArr, flagsOffset, rigBlob.Value.bones.Length);
    }
}
}
