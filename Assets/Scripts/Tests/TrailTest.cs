using Unity.Entities;
using Unity.Transforms;
// using System.Collections;
// using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
// using UnityEngine.Assertions;
using Unity.Jobs;
using Unity.Burst;
using TestJobComponentSystem = TestInterface.JobComponentSystem;

namespace UTJ {

[System.Serializable]
public struct TrailTestData : IComponentData
{
    public int dummy_;
}

public class TrailTestSystem : TestJobComponentSystem
{
	struct Group
	{
        public ComponentDataArray<Position> position_list_;
        public ComponentDataArray<Rotation> rotation_list_;
        [ReadOnly] public ComponentDataArray<TrailTestData> trail_test_list_;
	}
	[Inject] Group group_;
    
    [BurstCompile]
    struct Job : IJobParallelFor
    {
        public float time_;
        public Group group_;
        public void Execute(int i)
        {
            var scale = 1.6f;
            var pos = group_.position_list_[i];
            var rot = group_.rotation_list_[i];

            pos.Value.x += math.cos(time_*(8f+(float)i/10f)) * scale;
            pos.Value.y += math.sin(time_*(7f+(float)i/10f)) * scale;
            pos.Value.z += math.sin(time_*(5f+(float)i/10f)) * scale;
            rot.Value = quaternion.euler(time_*0.1f, time_*0.2f, time_*0.3f);

            group_.rotation_list_[i] = rot;
            group_.position_list_[i] = pos;
        }
    }

	protected override JobHandle OnUpdate(JobHandle indep)
    {
        var job = new Job {
            time_ = UnityEngine.Time.time,
            group_ = group_,
        };
        return job.Schedule(group_.position_list_.Length, 8, indep);
    }    
}

public class TrailTest : MonoBehaviour {

    public static TrailTest instance_;
    public static TrailTest Instance { get { if (instance_ == null) { instance_ = GameObject.Find("trail_test").GetComponent<TrailTest>(); } return instance_; } }

    EntityArchetype archetype_;

    void initialize()
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        archetype_ = entity_manager.CreateArchetype(typeof(Position)
                                                    , typeof(LocalToWorld)
                                                    , typeof(TrailData)
                                                    , typeof(TrailPoint)
                                                    , typeof(TrailRenderer)
                                                    );
    }

    void finalize()
    {
    }

    void OnEnable()
    {
        Instance.initialize();
    }
    
    void OnDisable()
    {
        Instance.finalize();
    }

    void spawn(ref float3 position, TrailManager.ColorType coltype, Entity parent)
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<Unity.Entities.EntityManager>();
        var entity = entity_manager.CreateEntity(archetype_);
        entity_manager.SetComponentData(entity, new Position { Value = position, });
        var td = new TrailData();
        td.color_type_ = (int)coltype;
        entity_manager.SetComponentData(entity, td);
        {
            var buffer = entity_manager.GetBuffer<TrailPoint>(entity);
            for (var i = 0; i < buffer.Capacity; ++i) {
                buffer.Add(new TrailPoint { position_ = position, normal_ = new float3(0f, 0f, 1f), });
            }
        }
		var renderer = new TrailRenderer {
            material_ = TrailManager.Instance.getMaterial(),
		};
		entity_manager.SetSharedComponentData<TrailRenderer>(entity, renderer);

        var attach = entity_manager.CreateEntity(typeof(Attach));
        entity_manager.SetComponentData(attach, new Attach { Parent = parent, Child = entity, });
    }

    void Start()
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<Unity.Entities.EntityManager>();
        var entity = entity_manager.CreateEntity(typeof(TrailTestData)
                                                 , typeof(Position)
                                                 , typeof(Rotation)
                                                 , typeof(Destroyable) // if not, it disappears immediately..
                                                 );
        entity_manager.SetComponentData(entity, new Rotation { Value = quaternion.identity, });
        // Debug.Log(entity.Index);
        // Debug.Log(entity.Version);
        entity_manager.SetComponentData(entity, new TrailTestData { dummy_ = 999, });
        for (var i = 0; i < 64; ++i) {
            var pos = (float3)UnityEngine.Random.onUnitSphere*4f;
            spawn(ref pos, TrailManager.ColorType.Red, entity);
        }
        // Debug.Log(entity_manager.Debug.EntityCount);
    }
}

} // namespace UTJ {
