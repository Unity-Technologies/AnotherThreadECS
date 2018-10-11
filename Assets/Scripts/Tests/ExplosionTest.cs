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
using TestComponentSystem = TestInterface.ComponentSystem;
// using TestComponentSystem = Unity.Entities.ComponentSystem;

namespace UTJ {

public struct ExplosionTestDummy : IComponentData
{
}

public class ExplosionTestSystem : TestComponentSystem
{
    struct Group {
#pragma warning disable 0169
        [ReadOnly] ComponentDataArray<ExplosionTestDummy> exists;
#pragma warning restore 0169
        public ComponentDataArray<RandomLocal> random_list_;
    }
    [Inject] Group group_;

	protected override void OnUpdate()
    {
        for (var i = 0; i < group_.random_list_.Length; ++i) {
            const float RANGE = 10f;
            var time = Time.GetCurrent();
            var random = group_.random_list_[i];
            var pos = new float3(random.range(-RANGE, RANGE),
                                 random.range(-RANGE, RANGE),
                                 random.range(-RANGE, RANGE));
            ECSExplosionManager.spawn(PostUpdateCommands,
                                      time,
                                      ref pos,
                                      random.range(-Mathf.PI, Mathf.PI));
            group_.random_list_[i] = random;
        }
    }    
}

public class ExplosionTest : MonoBehaviour
{
    void OnEnable()
    {
        TimeSystem.Ignite();

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(ExplosionTestDummy),
                                                        typeof(RandomLocal));
		var entity = entity_manager.CreateEntity(arche_type);
        entity_manager.SetComponentData(entity, RandomMaker.create());
    }
}

} // namespace UTJ {
