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
using TestComponentSystem = TestInterface.ComponentSystem;
// using TestComponentSystem = Unity.Entities.ComponentSystem;

namespace UTJ {

public struct SparkTestDummy : IComponentData
{
}

public class SparkTestSystem : TestComponentSystem
{
    struct Group {
#pragma warning disable 0169
        [ReadOnly] ComponentDataArray<SparkTestDummy> exists;
#pragma warning restore 0169
        public ComponentDataArray<RandomLocal> random_list_;
    }
    [Inject] Group group_;

	protected override void OnUpdate()
    {
        var time = Time.GetCurrent();
        for (var i = 0; i < group_.random_list_.Length; ++i) {
            var random = group_.random_list_[i];
            var pos = random.onCube(10f);
            var dat0 = new SparkSpawnData {
                position_ = pos,
                type_ = 0,
            };
            ECSSparkManager.spawn(PostUpdateCommands,
                                  time,
                                  ref dat0);
            group_.random_list_[i] = random;
        }
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
