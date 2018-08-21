using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
// using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
// using UnityEngine.Assertions;

namespace UTJ {

public struct Sight : IComponentData
{
    public Entity target_entity_;
    public float3 prev_screen_position_;
    public int initialized_;
}

public struct SightSpawnData
{
    public Entity target_entity_;
}

[UpdateBefore(typeof(PlayerSystem))]
[UpdateBefore(typeof(Unity.Rendering.MeshInstanceRendererSystem))] 
public class SightSpawnSystem : ComponentSystem
{
    struct SightSpawnIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(SightSpawnIgniter));
		entity_manager.CreateEntity(arche_type);
    }

#pragma warning disable 0169
    struct Group { ComponentDataArray<SightSpawnIgniter> dummy_; }
    [Inject] Group group_;
#pragma warning restore 0169

    protected override void OnUpdate()
    {
        PlayerSystem.Sync();
        var spawner = ECSSightManager.GetSightSpawnDataQueue();
        SightSpawnData data;
        while (spawner.TryDequeue(out data)) {
            ECSSightManager.spawn(PostUpdateCommands,
                                  Time.GetCurrent(),
                                  ref data);
        }
    }    
}

public class SightSystem : JobComponentSystem
{
    [Inject] [ReadOnly] public ComponentDataFromEntity<ScreenPosition> screen_position_from_entity_;
    struct Group {
        public readonly int Length;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        public ComponentDataArray<Sight> sight_list_;
        [WriteOnly] public ComponentDataArray<TransformMatrix> matrix_list_;
    }
    [Inject] Group group_;

    [BurstCompile]
    struct Job : IJobParallelFor
    {
        [ReadOnly] public ComponentDataFromEntity<ScreenPosition> screen_position_from_entity_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        public ComponentDataArray<Sight> sight_list_;
        [WriteOnly] public ComponentDataArray<TransformMatrix> matrix_list_;

        public void Execute(int i)
        {
            var sight = sight_list_[i];
            if (!screen_position_from_entity_.Exists(sight.target_entity_)) {
                destroyable_list_[i] = Destroyable.Kill;
                return;
            }
            var sp = screen_position_from_entity_[sight.target_entity_];
            var prev_sp = sight.initialized_ != 0 ? sight.prev_screen_position_ : sp.Value;
            matrix_list_[i] = new TransformMatrix {
                Value = new float4x4(new float4(sp.Value, 1f),
                                     new float4(prev_sp, 1f),
                                     new float4(0f, 0f, 0f, 0f), // not in use
                                     new float4(0.1f, 1f, 0.5f, 1f)), // color
            };
            sight.prev_screen_position_ = sp.Value;
            sight.initialized_ = 1;
            sight_list_[i] = sight;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var handle = inputDeps;
        
        var job = new Job {
            screen_position_from_entity_ = screen_position_from_entity_,
            destroyable_list_ = group_.destroyable_list_,
            sight_list_ = group_.sight_list_,
            matrix_list_ = group_.matrix_list_,
        };
        handle = job.Schedule(group_.Length, 8, handle);

        return handle;
    }    
}

public class ECSSightManager : MonoBehaviour
{
	static ECSSightManager instance_;
	public static ECSSightManager Instance { get { return instance_; } }

	[SerializeField] Material material_;
    [SerializeField] UnityEngine.Camera camera_;
    NativeQueue<SightSpawnData> sight_spawn_data_queue_;
    Material material_sight_;
    Mesh mesh_;
    EntityArchetype arche_type_;

    static public NativeQueue<SightSpawnData> GetSightSpawnDataQueue()
    {
        return Instance.getSightSpawnDataQueue();
    }

    NativeQueue<SightSpawnData> getSightSpawnDataQueue()
    {
        return sight_spawn_data_queue_;
    }

	void OnEnable()
	{
        sight_spawn_data_queue_ = new NativeQueue<SightSpawnData>(Allocator.Persistent);

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		arche_type_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                     , typeof(AlivePeriod)
                                                     , typeof(StartTime)
                                                     , typeof(Sight)
                                                     , typeof(TransformMatrix)
                                                     , typeof(MeshInstanceRendererWithTime)
                                                     );
		material_sight_ = new Material(material_.shader);
		material_sight_.enableInstancing = true;

        Vector3[] vertices = new Vector3[8];
        for (var i = 0; i < vertices.Length; ++i) {
            vertices[i] = Vector3.zero;
        }
        int[] triangles = new int[24] {
            6, 7, 2,
            2, 7, 3,
            5, 4, 1,
            1, 4, 0,
            4, 6, 0,
            0, 6, 2,
            7, 5, 3,
            3, 5, 1,
        };

		mesh_ = new Mesh();
		mesh_.vertices = vertices;
        mesh_.triangles = triangles;
		mesh_.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);
		instance_ = GameObject.Find("sight_manager").GetComponent<ECSSightManager>();
	}

    void OnDisable()
    {
        sight_spawn_data_queue_.Dispose();
    }

    public static void spawn(EntityCommandBuffer command_buffer,
                             float current_time,
                             ref SightSpawnData data)
    {
        spawnSight(command_buffer, current_time, data.target_entity_);
    }

	public static void spawnSight(EntityCommandBuffer.Concurrent entity_command_buffer,
                                  float current_time,
                                  Entity target_entity)
	{
        if (Instance == null) return;
		Instance.spawn_sight_internal(entity_command_buffer,
                                      current_time,
                                      target_entity);
	}

	public void spawn_sight_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                     float current_time,
                                     Entity target_entity)
	{
		spawn_internal(entity_command_buffer,
                       current_time,
                       target_entity,
                       material_sight_);
	}

	private void spawn_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                float current_time,
                                Entity target_entity,
								Material mat)
	{
        // Develop.print("yes");
		entity_command_buffer.CreateEntity(arche_type_);
		entity_command_buffer.SetComponent(new AlivePeriod { start_time_ = current_time, period_ = 0.3f, });
		entity_command_buffer.SetComponent(new StartTime { value_ = current_time, });
		entity_command_buffer.SetComponent(new Sight { target_entity_ = target_entity, });
		entity_command_buffer.SetSharedComponent(new MeshInstanceRendererWithTime {
                                                     mesh = mesh_,
                                                     material = mat,
                                                     castShadows = UnityEngine.Rendering.ShadowCastingMode.Off,
                                                     receiveShadows = false,
                                                     camera = camera_,
                                                     needDT = true,
                                                     needPrevMatrix = false,
                                                     layer = 9 /* final */,
                                                 });
	}
}

} // namespace UTJ {
