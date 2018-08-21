using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine.Jobs;
using UnityEngine;
using TestJobComponentSystem = TestInterface.JobComponentSystem;

namespace UTJ {

public struct SightParent : IComponentData
{
}

public class SightParentComponent : ComponentDataWrapper<SightParent> {}

public class SightParentSystem : TestJobComponentSystem
{
    struct Group
    {
        public readonly int Length;
        [WriteOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<SightParent> sight_parent_list_;
    }
    [Inject] Group group_;

    [BurstCompile]
    struct Job : IJobParallelFor
    {
        public float time_;
        [WriteOnly] public ComponentDataArray<Position> position_list_;

        public void Execute(int i)
        {
            position_list_[i] = new Position { Value = new float3(math.cos(time_*2.5f*4f),
                                                                  math.sin(time_*2f*4f),
                                                                  math.cos(time_*3.5f*4f)), };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var handle = inputDeps;

        var job = new Job {
            time_ = Time.GetCurrent(),
            position_list_ = group_.position_list_,
        };
        handle = job.Schedule(group_.Length, 8, handle);
        
        return handle;
    }
}

public class SightParentApplyTransformSystem : JobComponentSystem
{
    struct Group
    {
        public TransformAccessArray transform_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<SightParent> sight_parent_list_;
    }
    [Inject] Group group_;

    [BurstCompile]
    struct Job : IJobParallelForTransform
    {
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        public void Execute(int i, TransformAccess transform)
        {
            transform.localPosition = (Vector3)position_list_[i].Value;
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

		var job = new Job {
            position_list_ = group_.position_list_,
        };
		handle = job.Schedule(group_.transform_list_, handle);

        return handle;
    }    
}

} // namespace UTJ {
