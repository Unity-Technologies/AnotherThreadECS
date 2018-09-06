using Unity.Entities;
// using Unity.Transforms;
// using System.Collections;
// using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
// using Unity.Mathematics;
// using UnityEngine.Assertions;
using Unity.Jobs;
// using Unity.Burst;
using TestJobComponentSystem = TestInterface.JobComponentSystem;

namespace UTJ {

public struct SparkTestDummy : IComponentData
{
}

public class SparkTestSystem : TestJobComponentSystem
{
    struct Group {
#pragma warning disable 0169
        [ReadOnly] ComponentDataArray<SparkTestDummy> exists;
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
            var pos = random.onCube(10f);
            ECSSparkManager.spawnSpark(command_buffer_,
                                       time_,
                                       ref pos);
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

public class SparkTest : MonoBehaviour
{
    void OnEnable()
    {
        TimeSystem.Ignite();

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(SparkTestDummy),
                                                        typeof(RandomLocal));
		var entity = entity_manager.CreateEntity(arche_type);
        entity_manager.SetComponentData(entity, RandomMaker.create());
    }
}

} // namespace UTJ {
