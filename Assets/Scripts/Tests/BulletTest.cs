using Unity.Entities;
// using Unity.Transforms;
// using System.Collections;
// using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
// using UnityEngine.Assertions;
using Unity.Jobs;
// using Unity.Burst;
using TestJobComponentSystem = TestInterface.JobComponentSystem;
// using TestJobComponentSystem = Unity.Entities.JobComponentSystem;

namespace UTJ {

public struct BulletTestDummy : IComponentData
{
}

public class BulletTestSystem : TestJobComponentSystem
{
    struct Group {
#pragma warning disable 0169
        [ReadOnly] ComponentDataArray<BulletTestDummy> exists;
#pragma warning restore 0169
        public ComponentDataArray<RandomLocal> random_list_;
    }
    [Inject] Group group_;

    class Barrier : BarrierSystem {}
    [Inject] Barrier barrier_;

    // [BurstCompile]
    struct Job : IJobParallelFor
    {
        public float time_;
        public EntityCommandBuffer.Concurrent command_buffer_;
        public ComponentDataArray<RandomLocal> random_list_;
        
        public void Execute(int i)
        {
            var random = random_list_[i];
            var pos = new float3(0f, 0f, 0f);
            var vel = random.onSphere(10f);
            ECSBulletManager.spawnBullet(command_buffer_,
                                         time_,
                                         ref pos,
                                         ref vel);
            vel = random.onSphere(10f);
            ECSBulletManager.spawnEnemyBullet(command_buffer_,
                                              time_,
                                              ref pos,
                                              ref vel);
            random_list_[i] = random;
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var handle = inputDeps;

        var job = new Job {
            time_ = Time.GetCurrent(),
            command_buffer_ = barrier_.CreateCommandBuffer(),
            random_list_ = group_.random_list_,
        };
        handle = job.Schedule(group_.random_list_.Length, 8, handle);

        return handle;
    }    
}

public class BulletTest : MonoBehaviour
{
    void OnEnable()
    {
        TimeSystem.Ignite();

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(BulletTestDummy),
                                                        typeof(RandomLocal));
		var entity = entity_manager.CreateEntity(arche_type);
        entity_manager.SetComponentData(entity, RandomMaker.create());
    }
}

} // namespace UTJ {
