using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;

namespace UTJ {

public struct ExplosionSpawnData
{
    public float3 position_;
}

[UpdateBefore(typeof(Unity.Rendering.MeshInstanceRendererSystem))] 
public class ExplosionSpawnSystem : ComponentSystem
{
    struct ExplosionSpawnIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(ExplosionSpawnIgniter));
		entity_manager.CreateEntity(arche_type);
    }

#pragma warning disable 0169
    struct Group { ComponentDataArray<ExplosionSpawnIgniter> dummy_; }
    [Inject] Group group_;
#pragma warning restore 0169

    protected override void OnUpdate()
    {
        LaserSystem.Sync();
        ZakoSystem.Sync();

        var spawner = ECSExplosionManager.GetExplosionSpawnDataQueue();
        ExplosionSpawnData data;
        while (spawner.TryDequeue(out data)) {
            ECSExplosionManager.spawn(PostUpdateCommands,
                                      Time.GetCurrent(),
                                      ref data.position_,
                                      0f /* rotation1 */);
        }
    }    
}


public class ECSExplosionManager : MonoBehaviour
{
	static ECSExplosionManager instance_;
    public static ECSExplosionManager Instance { get { if (instance_ == null) { instance_ = GameObject.Find("explosion_manager").GetComponent<ECSExplosionManager>(); } return instance_; } }

    NativeQueue<ExplosionSpawnData> explosion_spawn_data_queue_;
	[SerializeField] Material material_;
    Mesh mesh_;
    EntityArchetype arche_type_;

    static public NativeQueue<ExplosionSpawnData> GetExplosionSpawnDataQueue()
    {
        return Instance.getExplosionSpawnDataQueue();
    }

    NativeQueue<ExplosionSpawnData> getExplosionSpawnDataQueue()
    {
        return explosion_spawn_data_queue_;
    }

	void OnEnable()
	{
        explosion_spawn_data_queue_ = new NativeQueue<ExplosionSpawnData>(Allocator.Persistent);

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		arche_type_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                     , typeof(Position)
													 , typeof(Rotation)
													 , typeof(LocalToWorld)
													 , typeof(AlivePeriod)
													 , typeof(StartTime)
													 , typeof(MeshInstanceRenderer)
													 );
        Vector3[] vertices = new Vector3[4];
        Vector2[] uvs = {
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
        };
        int[] triangles = {
            0, 1, 2,
            2, 1, 3,
        };
        mesh_ = new Mesh();
		mesh_.vertices = vertices;
		mesh_.uv = uvs;
		mesh_.triangles = triangles;
		mesh_.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);

        instance_ = GameObject.Find("explosion_manager").GetComponent<ECSExplosionManager>();
	}

    void OnDisable()
    {
        explosion_spawn_data_queue_.Dispose();
    }

	public static void spawn(EntityCommandBuffer.Concurrent entity_command_buffer,
                             float current_time,
                             ref float3 position,
                             float rotation1)
	{
		Instance.spawn_internal(entity_command_buffer, current_time, ref position, rotation1);
	}

	private void spawn_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                float current_time,
                                ref float3 position,
                                float rotation1)
	{
		entity_command_buffer.CreateEntity(arche_type_);
		entity_command_buffer.SetComponent(new Position { Value = position, });
		entity_command_buffer.SetComponent(new Rotation { Value = quaternion.axisAngle(new float3(0f, 0f, 1f), rotation1), });
		entity_command_buffer.SetComponent(new AlivePeriod {
                start_time_ = current_time,
                period_ = 0.8f,
            });
		entity_command_buffer.SetComponent(new StartTime { value_ = current_time, });
		var renderer = new MeshInstanceRenderer {
			mesh = mesh_,
			material = material_,
            subMesh = 0,
            castShadows = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = true,
		};
		entity_command_buffer.SetSharedComponent(renderer);
	}
}

} // namespace UTJ {
