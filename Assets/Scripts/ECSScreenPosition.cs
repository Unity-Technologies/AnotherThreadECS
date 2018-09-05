// using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
// using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace UTJ {

public struct ScreenPosition : IComponentData
{
    public float3 Value;
}

public class ScreenPositionSystem : JobComponentSystem
{
    struct Group
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<LocalToWorld> local_to_world_list_;
        [WriteOnly] public ComponentDataArray<ScreenPosition> screen_position_list_;
    }
    [Inject] Group group_;

    struct CameraGroup
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Camera> camera_list_;
    }
    [Inject] CameraGroup camera_group_;

    [BurstCompile]
    struct Job : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<Camera> camera_list_;
        [ReadOnly] public ComponentDataArray<LocalToWorld> local_to_world_list_;
        [WriteOnly] public ComponentDataArray<ScreenPosition> screen_position_list_;

        public void Execute(int i)
        {
            var camera = camera_list_[0];
            var pos = local_to_world_list_[i].Value.c3.xyz;
            var screen_pos = camera.getSpritePosition(ref pos);
            screen_position_list_[i] = new ScreenPosition { Value = screen_pos, };
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

        Assert.AreEqual(camera_group_.Length, 1);
        var job = new Job {
            camera_list_ = camera_group_.camera_list_,
            local_to_world_list_ = group_.local_to_world_list_,
            screen_position_list_ = group_.screen_position_list_,
        };
        handle = job.Schedule(group_.Length, 8, handle);

        return handle;
    }
}

} // namespace UTJ {
