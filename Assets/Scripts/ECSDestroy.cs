using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Burst;

namespace UTJ {

public struct Destroyable : IComponentData
{
    public int killed_;
    public static Destroyable Kill { get { return new Destroyable { killed_ = 1, }; } }
}

[UpdateAfter(typeof(TrailRendererSystem))]
public class DestroyableSsytem : JobComponentSystem
{
	struct Group
	{
        public readonly int Length;
        [ReadOnly] public EntityArray entity_list_;
		[ReadOnly] public ComponentDataArray<Destroyable> destroyable_list_;
    }
    [Inject] Group group_;
    
    [Inject] [ReadOnly] ComponentDataFromEntity<Destroyable> destroyable_list_from_entity_;
    struct ChildGroup
    {
        public readonly int Length;
        [ReadOnly] public EntityArray entity_list_;
        [ReadOnly] public ComponentDataArray<Parent> parent_list_;
    }
    [Inject] ChildGroup child_group_;

    [UpdateBefore(typeof(Unity.Rendering.MeshInstanceRendererSystem))] class DestroyBarrier : BarrierSystem {}
    [Inject] DestroyBarrier barrier_;

    [BurstCompile]
    struct Job : IJobParallelFor
    {
        public EntityCommandBuffer.Concurrent command_buffer_;
        [ReadOnly] public EntityArray entity_list_;
		[ReadOnly] public ComponentDataArray<Destroyable> destroyable_list_;

        public void Execute(int i)
        {
            Destroyable d = destroyable_list_[i];
            if (d.killed_ != 0) {
                command_buffer_.DestroyEntity(entity_list_[i]);
            }
        }
    }

    [BurstCompile]
    struct CleanJob : IJobParallelFor
    {
        public EntityCommandBuffer.Concurrent command_buffer_;
        [ReadOnly] public ComponentDataFromEntity<Destroyable> destroyable_list_from_entity_;
        [ReadOnly] public EntityArray entity_list_;
		[ReadOnly] public ComponentDataArray<Parent> parent_list_;

        public void Execute(int i)
        {
            Parent parent = parent_list_[i];
            if (!destroyable_list_from_entity_.Exists(parent.Value) ||
                destroyable_list_from_entity_[parent.Value].killed_ != 0) {
                command_buffer_.DestroyEntity(entity_list_[i]);
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDep)
    {
        var handle = inputDep;

        var job = new Job {
            command_buffer_ = barrier_.CreateCommandBuffer(),
            entity_list_ = group_.entity_list_,
            destroyable_list_ = group_.destroyable_list_,
        };
        handle = job.Schedule(group_.Length, 32, handle);

        var cjob = new CleanJob {
            command_buffer_ = barrier_.CreateCommandBuffer(),
            destroyable_list_from_entity_ = destroyable_list_from_entity_,
            entity_list_ = child_group_.entity_list_,
            parent_list_ = child_group_.parent_list_,
        };
        handle = cjob.Schedule(child_group_.Length, 32, handle);

        return handle;
    }
}

} // namespace UTJ {
