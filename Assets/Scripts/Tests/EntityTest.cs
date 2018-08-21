// using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
// using Unity.Mathematics;
// using Unity.Rendering;
// using Unity.Jobs;
// using Unity.Burst;
using UnityEngine;
using TestComponentSystem = TestInterface.ComponentSystem;

namespace UTJ {

public struct TimeAlive : IComponentData
{
    public float start_time_;
}

public class EntityTestSystem : TestComponentSystem
{
	struct Group
	{
        public EntityArray entity_list_;
        public ComponentDataArray<TimeAlive> time_alive_list_;
	}
	[Inject] Group group_;

    class Barrier : BarrierSystem {}
    [Inject] Barrier barrier_;
    
	protected override void OnUpdate()
    {
        var cb = barrier_.CreateCommandBuffer();
        for (var i = 0; i < group_.time_alive_list_.Length; ++i) {
            if (group_.time_alive_list_[i].start_time_ + 1f < UnityEngine.Time.time) {
                var entity = group_.entity_list_[i];
                cb.DestroyEntity(entity);
                // var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
                // entity_manager.DestroyEntity(entity);
            }
        }
    }    
}

public class EntityTest : MonoBehaviour {

    EntityArchetype archetype_;

    void initialize()
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        archetype_ = entity_manager.CreateArchetype(typeof(Position)
                                                    , typeof(TimeAlive)
                                                    );
    }

    void finalize()
    {
    }

    void OnEnable()
    {
        initialize();
    }

    void OnDisable()
    {
        finalize();
    }

    void Start()
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        var entities = new Entity[100];
        for (var i = 0; i < entities.Length; ++i) {
            entities[i] = entity_manager.CreateEntity(archetype_);
        }
        {
            var i = 98;
            Debug.LogFormat("i:{0}, v:{1}", entities[i].Index, entities[i].Version);
        }
        entity_manager.DestroyEntity(entities[50]);
        entity_manager.DestroyEntity(entities[51]);
        entity_manager.DestroyEntity(entities[52]);
        Debug.LogFormat("{0} exist? {1}", 50, entity_manager.Exists(entities[50]) ? "yes" : "no");

        {
            var i = 98;
            Debug.LogFormat("i:{0}, v:{1}", entities[i].Index, entities[i].Version);
        }

        var entity = entity_manager.CreateEntity(archetype_);
        {
            Debug.LogFormat("i:{0}, v:{1}", entity.Index, entity.Version);
        }


        // var entity = entity_manager.CreateEntity(archetype_);
        // Debug.LogFormat("i:{0}, v:{1}", entity.Index, entity.Version);
        // entity_manager.SetComponentData(entity, new TimeAlive { start_time_ = UnityEngine.Time.time, });
        
    }

    // void Update()
    // {
    //     var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
    //     var entity = entity_manager.CreateEntity(archetype_);
    //     Debug.LogFormat("i:{0}, v:{1}", entity.Index, entity.Version);
    //     entity_manager.SetComponentData(entity, new TimeAlive { start_time_ = UnityEngine.Time.time, });
	// }
}

} // namespace UTJ {
