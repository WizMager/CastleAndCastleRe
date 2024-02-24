using Rukhanka.DebugDrawer;
using Unity.Entities;

/////////////////////////////////////////////////////////////////////////////////

namespace Rukhanka
{

[DisableAutoCreation]
public partial class BoneVisualizationSystem: SystemBase
{
	EntityQuery boneVisualizeQuery;

/////////////////////////////////////////////////////////////////////////////////

	protected override void OnUpdate()
	{
		if (!SystemAPI.TryGetSingleton<RuntimeAnimationData>(out var runtimeData))
			return;
		
		if (!SystemAPI.TryGetSingletonRW<Drawer>(out var dd))
			return;
		
		var renderBonesJob = new RenderBonesJob()
		{
			entityToDataOffsetMap = runtimeData.entityToDataOffsetMap,
			bonePoses = runtimeData.worldSpaceBonesBuffer,
			drawer = dd.ValueRW
		};

		renderBonesJob.ScheduleParallel();
	}
}
}
