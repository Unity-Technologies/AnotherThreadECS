// using UnityEngine;
using Unity.Entities;
using Unity.Collections;
// using Unity.Transforms;
// using Unity.Mathematics;
// using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;

namespace UTJ {

public struct Enemy : IComponentData
{
    public AtomicFloat life_;
    public static Enemy Create(ref AtomicFloatResource float_resource)
    {
        return new Enemy {
            life_ = float_resource.Create(5f),
        };
    }
}

public class EnemySystem : JobComponentSystem
{
	struct Group
	{
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<AlivePeriod> alive_period_list_;
		[ReadOnly] public ComponentDataArray<Enemy> enemy_list_;
		[WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
	}
	[Inject] Group group_;

    [BurstCompile]
	struct EnemyJob : IJobParallelFor
	{
        public float time_;
        [ReadOnly] public ComponentDataArray<AlivePeriod> alive_period_list_;
		[ReadOnly] public ComponentDataArray<Enemy> enemy_list_;
		[WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        
		public void Execute(int i)
		{
            bool destroy = false;
            var enemy = enemy_list_[i];
            {
                var ap = alive_period_list_[i];
                if (ap.period_ < time_ - ap.start_time_) {
                    destroy = true;
                }
            }
            if (destroy) {
                enemy.life_.Dispose();
                destroyable_list_[i] = Destroyable.Kill;
            }
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;
        
        Entity player_entity = SystemManager.Instance.getPrimaryPlayer();
		var job = new EnemyJob {
            time_ = Time.GetCurrent(),
            alive_period_list_ = group_.alive_period_list_,
            enemy_list_ = group_.enemy_list_,
            destroyable_list_ = group_.destroyable_list_,
		};
        handle = job.Schedule(group_.Length, 16, handle);
        return handle;
	}
}

} // namespace UTJ {
