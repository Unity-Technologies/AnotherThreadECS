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

public struct TrailTestData : IComponentData
{
}

public class TrailTestSystem : TestJobComponentSystem
{
	struct Group
	{
        public ComponentDataArray<Position> position_list_;
		[ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
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
            var scale = 16f;
            var pos = group_.position_list_[i];
            pos.Value.x += math.cos(time_*(8f+(float)i/10f)) * scale;
            pos.Value.y += math.sin(time_*(7f+(float)i/10f)) * scale;
            pos.Value.z += math.sin(time_*(5f+(float)i/10f)) * scale;
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
                                                    , typeof(RigidbodyPosition)
                                                    , typeof(TrailData)
                                                    , typeof(TrailRenderer)
                                                    , typeof(TrailTestData)
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

    void spawn(ref float3 position, int coltype)
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<Unity.Entities.EntityManager>();
        var entity = entity_manager.CreateEntity(archetype_);
        entity_manager.SetComponentData(entity, new Position { Value = position, });
        entity_manager.SetComponentData(entity, new RigidbodyPosition(0f /* damper */));
        // var trail_buffer = TrailManager.Instance.getTrailBuffer();
        var td = new TrailData();
        // td.trail_id_ = trail_buffer.allocate(ref position);
        td.color_type_ = coltype;
        entity_manager.SetComponentData(entity, td);
        var material = TrailManager.Instance.getMaterial();
		var renderer = new TrailRenderer {
			material_ = material,
            // trail_buffer_ = trail_buffer,
		};
		entity_manager.SetSharedComponentData<TrailRenderer>(entity, renderer);
    }

    void Start()
    {
        var pos = (Unity.Mathematics.float3)Vector3.zero;
        for (var i = 0; i < 256; ++i) {
            spawn(ref pos, UnityEngine.Random.Range(0, (int)TrailManager.ColorType.Max));
        }
    }
}

} // namespace UTJ {
