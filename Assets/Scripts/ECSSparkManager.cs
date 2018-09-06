using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
// using UnityEngine.Assertions;

namespace UTJ {

public struct Spark : IComponentData
{
}

public struct SparkSpawnData
{
    public float3 position_;
    public int type_;                  // 0: Player	1: Enemy
}

[UpdateBefore(typeof(Unity.Rendering.MeshInstanceRendererSystem))] 
public class SparkSpawnSystem : ComponentSystem
{
    struct SparkSpawnIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(SparkSpawnIgniter));
		entity_manager.CreateEntity(arche_type);
    }

#pragma warning disable 0169
    struct Group { ComponentDataArray<SparkSpawnIgniter> dummy_; }
    [Inject] Group group_;
#pragma warning restore 0169

    protected override void OnUpdate()
    {
        BulletSystem.Sync();

        var spawner = ECSSparkManager.GetSparkSpawnDataQueue();
        SparkSpawnData data;
        while (spawner.TryDequeue(out data)) {
            ECSSparkManager.spawn(PostUpdateCommands,
                                  Time.GetCurrent(),
                                  ref data);
        }
    }    
}

public class ECSSparkManager : MonoBehaviour
{
	static ECSSparkManager instance_;
	public static ECSSparkManager Instance { get { return instance_; } }

	[SerializeField] Material material_;
    NativeQueue<SparkSpawnData> spark_spawn_data_queue_;
    Material material_spark_;
    Material material_enemy_spark_;
    Mesh mesh_;
    EntityArchetype arche_type_;

    static public NativeQueue<SparkSpawnData> GetSparkSpawnDataQueue()
    {
        return Instance.getSparkSpawnDataQueue();
    }

    NativeQueue<SparkSpawnData> getSparkSpawnDataQueue()
    {
        return spark_spawn_data_queue_;
    }

	void OnEnable()
	{
        spark_spawn_data_queue_ = new NativeQueue<SparkSpawnData>(Allocator.Persistent);

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		arche_type_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                     , typeof(Position)
                                                     , typeof(Rotation)
                                                     , typeof(LocalToWorld)
                                                     , typeof(Frozen)
                                                     , typeof(AlivePeriod)
                                                     , typeof(StartTime)
                                                     , typeof(Spark)
                                                     , typeof(MeshInstanceRenderer)
                                                     );
		material_spark_ = new Material(material_.shader);
		material_spark_.enableInstancing = true;
		material_spark_.SetColor("_Color", new UnityEngine.Color(0.25f, 1f, 0.5f));
		material_enemy_spark_ = new Material(material_.shader);
		material_enemy_spark_.enableInstancing = true;
		material_enemy_spark_.SetColor("_Color", new UnityEngine.Color(1f, 0.5f, 0.25f));

        const int PARTICLE_NUM = 64;
        Vector3[] vertices = new Vector3[PARTICLE_NUM*2];
        for (var i = 0; i < PARTICLE_NUM; ++i) {
			float x = UnityEngine.Random.Range(-1f, 1f);
			float y = UnityEngine.Random.Range(-1f, 1f);
			float z = UnityEngine.Random.Range(-1f, 1f);
			float len2 = x*x + y*y + z*z;
			float len = Mathf.Sqrt(len2);
			float rlen = 1.0f/len;
			var point = new Vector3(x*rlen, y*rlen, z*rlen);
			vertices[i*2+0] = point;
			vertices[i*2+1] = point;
        }
		var indices = new int[PARTICLE_NUM*2];
		for (var i = 0; i < PARTICLE_NUM*2; ++i) {
			indices[i] = i;
		}

		mesh_ = new Mesh();
		mesh_.vertices = vertices;
		mesh_.SetIndices(indices, MeshTopology.Lines, 0 /* submesh */);
		mesh_.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);
		instance_ = GameObject.Find("spark_manager").GetComponent<ECSSparkManager>();
	}

    void OnDisable()
    {
        spark_spawn_data_queue_.Dispose();
    }

    public static void spawn(EntityCommandBuffer command_buffer,
                             float current_time,
                             ref SparkSpawnData data)
    {
        if (data.type_ == 0) {
            spawnSpark(command_buffer, current_time, ref data.position_);
        } else {
            spawnEnemySpark(command_buffer, current_time, ref data.position_);
        }
    }

	public static void spawnSpark(EntityCommandBuffer.Concurrent entity_command_buffer,
                                  float current_time,
                                  ref float3 pos)
	{
        if (Instance == null) return;
		Instance.spawn_spark_internal(entity_command_buffer,
                                      current_time,
                                      ref pos);
	}
	public static void spawnEnemySpark(EntityCommandBuffer.Concurrent entity_command_buffer,
                                       float current_time,
                                       ref float3 pos)
	{
        if (Instance == null) return;
		Instance.spawn_enemy_spark_internal(entity_command_buffer,
                                            current_time,
                                            ref pos);
	}

	public void spawn_spark_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                     float current_time,
                                     ref float3 pos)
	{
		spawn_internal(entity_command_buffer,
                       current_time,
                       ref pos,
                       material_spark_);
	}
	public void spawn_enemy_spark_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                           float current_time,
                                           ref float3 pos)
	{
		spawn_internal(entity_command_buffer,
                       current_time,
                       ref pos,
                       material_enemy_spark_);
	}

	private void spawn_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                float current_time,
								ref float3 pos,
								Material mat)
	{
		entity_command_buffer.CreateEntity(arche_type_);
		entity_command_buffer.SetComponent(new Position { Value = pos, });
		entity_command_buffer.SetComponent(new Rotation { Value = quaternion.identity, });
		entity_command_buffer.SetComponent(new AlivePeriod { start_time_ = current_time, period_ = 1f, });
		entity_command_buffer.SetComponent(new StartTime { value_ = current_time, });
		entity_command_buffer.SetSharedComponent(new MeshInstanceRenderer {
                                                     mesh = mesh_,
                                                     material = mat,
                                                     castShadows = UnityEngine.Rendering.ShadowCastingMode.Off,
                                                     receiveShadows = false,
                                                 });
	}
}

} // namespace UTJ {
