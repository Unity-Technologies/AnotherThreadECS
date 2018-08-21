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
using UnityEngine.Assertions;
using TestJobComponentSystem = TestInterface.JobComponentSystem;

namespace UTJ {

public struct SightTestDummy : IComponentData
{
}

public class SightTestSystem : TestJobComponentSystem
{
    struct Group {
        public readonly int Length;
#pragma warning disable 0169
        [ReadOnly] ComponentDataArray<SightTestDummy> exists;
#pragma warning restore 0169
    }
    [Inject] Group group_;

    struct ParentGroup
    {
        public readonly int Length;
        [ReadOnly] public EntityArray entity_array_;
        [ReadOnly] public ComponentDataArray<SightParent> sight_parent_list_;
    }
    [Inject] ParentGroup parent_group_;

    class Barrier : BarrierSystem {}
    [Inject] Barrier barrier_;

    // [BurstCompile]
    struct Job : IJobParallelFor
    {
        public float time_;
        public EntityCommandBuffer.Concurrent command_buffer_;
        [ReadOnly] public EntityArray sight_parent_entity_array_;
        
        public void Execute(int i)
        {
            ECSSightManager.spawnSight(command_buffer_,
                                       time_,
                                       sight_parent_entity_array_[0]);
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var handle = inputDeps;

        if (Random.Range(0, 100) < 2) {
            Assert.AreEqual(parent_group_.Length, 1);
            var job = new Job {
                time_ = Time.GetCurrent(),
                command_buffer_ = barrier_.CreateCommandBuffer(),
                sight_parent_entity_array_ = parent_group_.entity_array_,
            };
            handle = job.Schedule(group_.Length, 8, handle);
        }

        return handle;
    }    
}

public class SightTest : MonoBehaviour
{
    void OnEnable()
    {
        TimeSystem.Ignite();

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(SightTestDummy),
                                                        typeof(RandomLocal));
		var entity = entity_manager.CreateEntity(arche_type);
        entity_manager.SetComponentData(entity, RandomMaker.create());
    }
}

} // namespace UTJ {
