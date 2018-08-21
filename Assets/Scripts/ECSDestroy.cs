using Unity.Entities;
using Unity.Collections;
using Unity.Jobs;
// using Unity.Burst;

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
    
    [UpdateBefore(typeof(Unity.Rendering.MeshInstanceRendererSystem))] class DestroyBarrier : BarrierSystem {}
    [Inject] DestroyBarrier barrier_;

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

	protected override JobHandle OnUpdate(JobHandle inputDep)
    {
        var handle = inputDep;

        // for (int i = 0; i < group_.destroyable_list_.Length; ++i) {
        //     var destroyable = group_.destroyable_list_[i];
        //     if (destroyable.killed_ != 0) {
        //         PostUpdateCommands.DestroyEntity(group_.entity_list_[i]);
        //     }
        // }
        var job = new Job {
            command_buffer_ = barrier_.CreateCommandBuffer(),
            entity_list_ = group_.entity_list_,
            destroyable_list_ = group_.destroyable_list_,
        };
        handle = job.Schedule(group_.Length, 32, handle);

        return handle;
    }
}

} // namespace UTJ {
