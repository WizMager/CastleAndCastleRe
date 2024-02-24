using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{
    
[UpdateInGroup(typeof(RukhankaAnimationInjectionSystemGroup))]
[UpdateAfter(typeof(FABRIKSystem))]
[RequireMatchingQueriesForUpdate]
public partial struct OverrideTransformIKSystem: ISystem
{
    [BurstCompile]
    partial struct OverrideTransformIKJob : IJobEntity
    {
        [ReadOnly]
        public ComponentLookup<RigDefinitionComponent> rigDefLookup;
        [ReadOnly]
        public ComponentLookup<LocalTransform> localTransformLookup;
        [ReadOnly]
        public ComponentLookup<Parent> parentLookup;
        
        [NativeDisableContainerSafetyRestriction]
        public RuntimeAnimationData runtimeData;
    
        void Execute(OverrideTransformIKComponent ikc, AnimatorEntityRefComponent aer)
        {
            var rigDef = rigDefLookup[aer.animatorEntity];
            using var animStream = AnimationStream.Create(runtimeData, aer.animatorEntity, rigDef);

            var targetEntityWorldPose = BoneTransform.Identity();
            IKCommon.GetEntityWorldTransform(ikc.target, ref targetEntityWorldPose, localTransformLookup, parentLookup);
            var bonePose = animStream.GetWorldPose(aer.boneIndexInAnimationRig);

            targetEntityWorldPose.pos = math.lerp(bonePose.pos, targetEntityWorldPose.pos, ikc.positionWeight);
            targetEntityWorldPose.rot = math.slerp(bonePose.rot, targetEntityWorldPose.rot, ikc.rotationWeight);
            targetEntityWorldPose.scale = 1;
            
            animStream.SetWorldPose(aer.boneIndexInAnimationRig, targetEntityWorldPose);
        }
    }

/////////////////////////////////////////////////////////////////////////////////
    
    [BurstCompile]
    public void OnUpdate(ref SystemState ss)
    {
        var rigDefLookup = SystemAPI.GetComponentLookup<RigDefinitionComponent>(true);
        var localTransformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
        ref var runtimeData = ref SystemAPI.GetSingletonRW<RuntimeAnimationData>().ValueRW;
        
        var ikJob = new OverrideTransformIKJob()
        {
            rigDefLookup = rigDefLookup,
            runtimeData = runtimeData,
            localTransformLookup = localTransformLookup,
            parentLookup = parentLookup
        };

        ikJob.ScheduleParallel();
    }
}
}
