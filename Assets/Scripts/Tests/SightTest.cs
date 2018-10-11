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
using TestComponentSystem = TestInterface.ComponentSystem;
// using TestComponentSystem = Unity.Entities.ComponentSystem;

namespace UTJ {

public struct SightTestDummy : IComponentData
{
}

public class SightTestSystem : TestComponentSystem
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

	protected override void OnUpdate()
    {
        if (Random.Range(0, 100) < 2) {
            Assert.AreEqual(parent_group_.Length, 1);
            for (var i = 0; i < parent_group_.Length; ++i) {
                var time = Time.GetCurrent();
                ECSSightManager.spawnSight(PostUpdateCommands,
                                           time,
                                           parent_group_.entity_array_[i]);
            }
        }
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
