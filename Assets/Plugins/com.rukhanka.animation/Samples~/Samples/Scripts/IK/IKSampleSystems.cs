using Unity.Entities;
using UnityEngine;

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka.Samples
{
[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateBefore(typeof(RukhankaAnimationSystemGroup))]
[RequireMatchingQueriesForUpdate]
public partial class IKControlsSystem: SystemBase
{
	IKSampleConf sampleConf;
	
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	protected override void OnStartRunning()
	{
		base.OnStartRunning();
		sampleConf = GameObject.FindObjectOfType<IKSampleConf>();
	}

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

	protected override void OnUpdate()
	{
		if (sampleConf == null)
			return;
		
		foreach (var ikc in SystemAPI.Query<RefRW<AimIKComponent>>())
		{
			ikc.ValueRW.weight = sampleConf.aimIKWeightSlider.value;
		}	
		
		foreach (var ikc in SystemAPI.Query<RefRW<OverrideTransformIKComponent>>())
		{
			ikc.ValueRW.positionWeight = sampleConf.overrideIKPosWeightSlider.value;
			ikc.ValueRW.rotationWeight = sampleConf.overrideIKRotWeightSlider.value;
		}	
		
		foreach (var (ikc, _) in SystemAPI.Query<RefRW<FABRIKComponent>, SnakeTag>())
		{
			ikc.ValueRW.weight = sampleConf.fabrikSnakeWeightSlider.value;
		}	
		
		foreach (var (ikc, _) in SystemAPI.Query<RefRW<FABRIKComponent>, EllenLeftLegTag>())
		{
			ikc.ValueRW.weight = sampleConf.fabrikLeftLegWeightSlider.value;
		}	
		
		foreach (var (ikc, _) in SystemAPI.Query<RefRW<FABRIKComponent>, EllenRightHandTag>())
		{
			ikc.ValueRW.weight = sampleConf.fabrikRightHandWeightSlider.value;
		}	
		
		foreach (var (ikc, _) in SystemAPI.Query<RefRW<TwoBoneIKComponent>, EllenLeftLegTag>())
		{
			ikc.ValueRW.weight = sampleConf.twoBoneLeftLegWeightSlider.value;
		}	
		
		foreach (var (ikc, _) in SystemAPI.Query<RefRW<TwoBoneIKComponent>, EllenRightLegTag>())
		{
			ikc.ValueRW.weight = sampleConf.twoBoneRightLegWeightSlider.value;
		}	
	}
}
}

