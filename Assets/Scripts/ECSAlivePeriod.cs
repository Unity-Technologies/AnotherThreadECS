using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;

namespace UTJ {

public struct AlivePeriod  : IComponentData
{
	public float start_time_;
	public float period_;
    public static AlivePeriod Create(float time, float period) {
        return new AlivePeriod { start_time_ = time, period_ = period, };
    }
}

public class AlivePeriodSystem : JobComponentSystem
{
	struct Group
	{
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<AlivePeriod> alive_period_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
#pragma warning disable 0169
        SubtractiveComponent<Enemy> missing;
#pragma warning restore 0169
	}
	[Inject] Group group_;

    [BurstCompile]
	struct Job : IJobParallelFor
	{
        public float time_;
        [ReadOnly] public ComponentDataArray<AlivePeriod> alive_period_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;

        public void Execute(int i)
        {
            var ap = alive_period_list_[i];
            if (ap.period_ < time_ - ap.start_time_) {
                destroyable_list_[i] = Destroyable.Kill;
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

		var job = new Job {
            time_ = Time.GetCurrent(),
            alive_period_list_ = group_.alive_period_list_,
            destroyable_list_ = group_.destroyable_list_,
        };
		handle = job.Schedule(group_.Length, 8, handle);

        return handle;
    }    
}

} // namespace UTJ {
