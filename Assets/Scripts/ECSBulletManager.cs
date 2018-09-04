using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

public struct BulletSpawnData
{
    public float3 position_;
    public float3 velocity_;
    public int type_;                  // 0: Player	1: Enemy
}

[UpdateBefore(typeof(Unity.Rendering.MeshInstanceRendererSystem))] 
public class BulletSpawnSystem : ComponentSystem
{
    struct BulletSpawnIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(BulletSpawnIgniter));
		entity_manager.CreateEntity(arche_type);
    }

#pragma warning disable 0169
    struct Group { ComponentDataArray<BulletSpawnIgniter> dummy_; }
    [Inject] Group group_;
#pragma warning restore 0169

    protected override void OnUpdate()
    {
        PlayerSystem.Sync();
        ZakoSystem.Sync();
        var spawner = ECSBulletManager.GetBulletSpawnDataQueue();
        BulletSpawnData data;
        while (spawner.TryDequeue(out data)) {
            if (data.type_ == 0) {
                ECSBulletManager.spawnBullet(PostUpdateCommands,
                                             Time.GetCurrent(),
                                             ref data.position_,
                                             ref data.velocity_);
            } else {
                ECSBulletManager.spawnEnemyBullet(PostUpdateCommands,
                                                  Time.GetCurrent(),
                                                  ref data.position_,
                                                  ref data.velocity_);
            }
        }
    }    
}

[UpdateAfter(typeof(SparkSpawnSystem))]
public class BulletSystem : JobComponentSystem
{
    struct PlayerBulletGroup
    {
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<PlayerBulletCollisionInfo> player_bullet_collision_info_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
    }
	[Inject] PlayerBulletGroup player_bullet_group_;

    struct EnemyBulletGroup
    {
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<EnemyBulletCollisionInfo> enemy_bullet_collision_info_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
    }
	[Inject] EnemyBulletGroup enemy_bullet_group_;

    static JobHandle handle_;
    public static JobHandle Fence { get { return handle_; } }
    public static void Sync() { handle_.Complete(); }

    [BurstCompile]
	struct PlayerBulletJob : IJobParallelFor
	{
        public float time_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<PlayerBulletCollisionInfo> player_bullet_collision_info_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeQueue<SparkSpawnData>.Concurrent spark_spawner_;
        
        public void Execute(int i)
        {
            bool destroy = false;
            var pos = position_list_[i].Value;
            var player_bullet_collision_info = player_bullet_collision_info_list_[i];
            if (player_bullet_collision_info.hit_ != 0) {
                destroy = true;
            } else {
                float2 p_xy = pos.xy;
                if (math.lengthSquared(p_xy) > CV.MAX_RADIUS_SQR) {
                    destroy = true;
                }
            }
            if (destroy) {
                destroyable_list_[i] = Destroyable.Kill;
                spark_spawner_.Enqueue(new SparkSpawnData {
                        position_ = pos,
                        type_ = 0 /* player bullet */,
                    });
            }
        }
    }

    [BurstCompile]
	struct EnemyBulletJob : IJobParallelFor
	{
        public float time_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<EnemyBulletCollisionInfo> enemy_bullet_collision_info_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeQueue<SparkSpawnData>.Concurrent spark_spawner_;
        
        public void Execute(int i)
        {
            bool destroy = false;
            var pos = position_list_[i].Value;
            var enemy_bullet_collision_info = enemy_bullet_collision_info_list_[i];
            if (enemy_bullet_collision_info.hit_ != 0) {
                destroy = true;
            } else {
                float2 p_xy = pos.xy;
                if (math.lengthSquared(p_xy) > CV.MAX_RADIUS_SQR) {
                    destroy = true;
                }
            }
            if (destroy) {
                destroyable_list_[i] = Destroyable.Kill;
                spark_spawner_.Enqueue(new SparkSpawnData {
                        position_ = pos,
                        type_ = 1 /* enemy bullet */,
                    });
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        handle_ = inputDeps;

        {
            var job = new PlayerBulletJob {
                time_ = Time.GetCurrent(),
                position_list_ = player_bullet_group_.position_list_,
                player_bullet_collision_info_list_ = player_bullet_group_.player_bullet_collision_info_list_,
                destroyable_list_ = player_bullet_group_.destroyable_list_,
                spark_spawner_ = ECSSparkManager.GetSparkSpawnDataQueue(),
            };
            handle_ = job.Schedule(player_bullet_group_.position_list_.Length, 16, handle_);
        }
        {
            var job = new EnemyBulletJob {
                time_ = Time.GetCurrent(),
                position_list_ = enemy_bullet_group_.position_list_,
                enemy_bullet_collision_info_list_ = enemy_bullet_group_.enemy_bullet_collision_info_list_,
                destroyable_list_ = enemy_bullet_group_.destroyable_list_,
                spark_spawner_ = ECSSparkManager.GetSparkSpawnDataQueue(),
            };
            handle_ = job.Schedule(enemy_bullet_group_.position_list_.Length, 16, handle_);
        }

        return handle_;
    }
}

public class ECSBulletManager : MonoBehaviour
{
	static ECSBulletManager instance_;
	public static ECSBulletManager Instance { get { return instance_; } }

	[SerializeField] Material material_;
    NativeQueue<BulletSpawnData> bullet_spawn_data_queue_;
    Material material_bullet_;
    Material material_enemy_bullet_;
    Mesh mesh_;
    EntityArchetype arche_type_bullet_;
    EntityArchetype arche_type_enemy_bullet_;

    static public NativeQueue<BulletSpawnData> GetBulletSpawnDataQueue()
    {
        return Instance.getBulletSpawnDataQueue();
    }

    NativeQueue<BulletSpawnData> getBulletSpawnDataQueue()
    {
        return bullet_spawn_data_queue_;
    }

	void OnEnable()
	{
        bullet_spawn_data_queue_ = new NativeQueue<BulletSpawnData>(Allocator.Persistent);

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		arche_type_bullet_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                            , typeof(Position)
															, typeof(Rotation)
															, typeof(LocalToWorld)
															, typeof(RigidbodyPosition)
															, typeof(SphereCollider)
															, typeof(PlayerBulletCollisionInfo)
															, typeof(AlivePeriod)
															, typeof(MeshInstanceRenderer)
															);
		arche_type_enemy_bullet_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                                  , typeof(Position)
																  , typeof(Rotation)
																  , typeof(LocalToWorld)
																  , typeof(RigidbodyPosition)
																  , typeof(SphereCollider)
                                                                  , typeof(EnemyBulletCollisionInfo)
																  , typeof(AlivePeriod)
																  , typeof(MeshInstanceRenderer)
																  );
		material_bullet_ = new Material(material_.shader);
		material_bullet_.enableInstancing = true;
		material_bullet_.mainTexture = material_.mainTexture;
		material_bullet_.SetColor("_Color", new UnityEngine.Color(0.25f, 1f, 0.5f));
		material_enemy_bullet_ = new Material(material_.shader);
		material_enemy_bullet_.enableInstancing = true;
		material_enemy_bullet_.mainTexture = material_.mainTexture;
		material_enemy_bullet_.SetColor("_Color", new UnityEngine.Color(1f, 0.5f, 0.25f));
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
		instance_ = GameObject.Find("bullet_manager").GetComponent<ECSBulletManager>();
	}

    void OnDisable()
    {
        bullet_spawn_data_queue_.Dispose();
    }

	public static void spawnBullet(EntityCommandBuffer.Concurrent entity_command_buffer,
                                   float time,
                                   ref float3 pos,
                                   ref float3 vel)
	{
		Instance.spawn_bullet_internal(entity_command_buffer,
                                       time,
                                       ref pos,
                                       ref vel);
	}
	public static void spawnEnemyBullet(EntityCommandBuffer.Concurrent entity_command_buffer,
                                        float time,
                                        ref float3 pos,
                                        ref float3 vel)
	{
		Instance.spawn_enemy_bullet_internal(entity_command_buffer,
                                             time,
                                             ref pos,
                                             ref vel);
	}

	public void spawn_bullet_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                      float time,
                                      ref float3 pos,
                                      ref float3 vel)
	{
		spawn_internal(entity_command_buffer,
                       time,
                       ref pos,
                       ref vel,
                       material_bullet_,
                       arche_type_bullet_);
	}
	public void spawn_enemy_bullet_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                            float time,
                                            ref float3 pos,
                                            ref float3 vel)
	{
		spawn_internal(entity_command_buffer,
                       time,
                       ref pos,
                       ref vel,
                       material_enemy_bullet_,
                       arche_type_enemy_bullet_);
	}

	private void spawn_internal(EntityCommandBuffer.Concurrent entity_command_buffer,
                                float time,
								ref float3 pos,
								ref float3 vel,
								Material mat,
								EntityArchetype arche_type)
	{
		entity_command_buffer.CreateEntity(arche_type);
		entity_command_buffer.SetComponent(new Position { Value = pos, });
		entity_command_buffer.SetComponent(new Rotation { Value = quaternion.lookRotation(vel, new float3(0f, 1f, 0f)), });
		entity_command_buffer.SetComponent(new RigidbodyPosition { velocity_ = vel, });
        entity_command_buffer.SetComponent(new SphereCollider {
                offset_position_ = new float3(0f, 0f, 0f),
                radius_ = 0.5f,
            });
		entity_command_buffer.SetComponent(new SphereCollider { radius_ = 1f, });
		entity_command_buffer.SetComponent(new AlivePeriod { start_time_ = time, period_ = 3f, });
		entity_command_buffer.SetSharedComponent(new MeshInstanceRenderer {
                                                     mesh = mesh_,
                                                     material = mat,
                                                     castShadows = UnityEngine.Rendering.ShadowCastingMode.Off,
                                                     receiveShadows = false,
                                                 });
	}
}

} // namespace UTJ {
