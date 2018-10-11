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

public struct BulletTestDummy : IComponentData
{
}

public class BulletTestSystem : TestComponentSystem
{
    struct Group {
#pragma warning disable 0169
        [ReadOnly] ComponentDataArray<BulletTestDummy> exists;
#pragma warning restore 0169
        public ComponentDataArray<RandomLocal> random_list_;
    }
    [Inject] Group group_;

	protected override void OnUpdate()
    {
        var time = Time.GetCurrent();
        for (var i = 0; i < group_.random_list_.Length; ++i) {
            var random = group_.random_list_[i];
            var pos = new float3(0f, 0f, 0f);
            var vel = random.onSphere(10f);
            ECSBulletManager.spawnBullet(PostUpdateCommands,
                                         time,
                                         ref pos,
                                         ref vel);
            vel = random.onSphere(10f);
            ECSBulletManager.spawnEnemyBullet(PostUpdateCommands,
                                              time,
                                              ref pos,
                                              ref vel);
            group_.random_list_[i] = random;
        }
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
