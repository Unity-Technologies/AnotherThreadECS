// using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
// using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Assertions;

namespace UTJ {

public struct Player : IComponentData
{
	public float pitch_acceleration_;
	public float pitch_velocity_;
	public float pitch_;
    public float roll_;
    public float roll_target_;
    public float2 cursor_forceXY_;
    public Entity cursor_entity_;
    public float fire_bullet_time_;
    public float fire_bullet_count_;
    public AtomicFloat life_;

    const float PLAYER_INITIAL_LIFE = 10000f;

    public static Player Create(Entity cursor_entity, ref AtomicFloatResource float_resource) {
        var player = new Player {
            pitch_acceleration_ = 0f,
            pitch_velocity_ = 0f,
            pitch_ = 0f,
            roll_ = 0f,
            roll_target_ = 0f,
            cursor_forceXY_ = new float2(0f, 0f),
            cursor_entity_ = cursor_entity,
            fire_bullet_time_ = CV.MaxValue,
            fire_bullet_count_ = 0,
            life_ = float_resource.Create(PLAYER_INITIAL_LIFE),
        };
        return player;
    }
}

[UpdateBefore(typeof(RigidbodyPositionSystem))]
[UpdateBefore(typeof(RigidbodyRotationSystem))]
[UpdateBefore(typeof(CollisionSystem))]
public class PlayerSystem : JobComponentSystem
{
    const int LASER_MAX = 16;
    NativeLimitedCounter lockon_limit_;

    [Inject] [ReadOnly] public ComponentDataFromEntity<PlayerController> player_controller_list_from_entity_;
    [Inject] [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;

	struct PlayerGroup
	{
        public readonly int Length;
        [ReadOnly] public EntityArray entity_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        public ComponentDataArray<Rotation> rotation_list_;
		public ComponentDataArray<RigidbodyPosition> rb_position_list_;
		public ComponentDataArray<Player> player_list_;
		[ReadOnly] public ComponentDataArray<PlayerController> player_controller_list_;
	}
	[Inject] PlayerGroup player_group_;

    struct LockonGroup
    {
        public readonly int Length;
        [ReadOnly] public EntityArray entity_list_;
        public ComponentDataArray<LockTarget> locktarget_list_;
        [ReadOnly] public ComponentDataArray<LocalToWorld> local_to_world_list_;
        public ComponentDataArray<RandomLocal> random_list_;
    }
    [Inject] LockonGroup lockon_group_;

    static JobHandle handle_;
    public static JobHandle Fence { get { return handle_; } }
    public static void Sync() { handle_.Complete(); }

    // [UpdateBefore(typeof(Unity.Rendering.MeshInstanceRendererSystem))]
    class PlayerBarrier : BarrierSystem {}
    [Inject] PlayerBarrier barrier_;

    [Inject] GameCameraUpdateSystem game_camera_update_system_;

    protected override void OnStopRunning()
    {
        if (lockon_limit_.IsCreated) {
            lockon_limit_.Dispose();
        }
    }

    [BurstCompile]
	struct PlayerJob : IJobParallelFor
	{
        [ReadOnly] public float dt_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        public ComponentDataArray<Rotation> rotation_list_;
        public ComponentDataArray<RigidbodyPosition> rb_position_list_;
        public ComponentDataArray<Player> player_list_;
        [ReadOnly] public ComponentDataArray<PlayerController> player_controller_list_;
        [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;

        const float MAX_RADIUS = 16f;
        const float MAX_RADIUS_SQR = MAX_RADIUS*MAX_RADIUS;
        
		public void Execute(int i)
		{
            float hori, vert;
            int lr;
            {
                var pc = player_controller_list_[i];
                hori = pc.horizontal_;
                vert = pc.vertical_;
                lr = pc.roll_;
            }

            float vx, vy;
            float pitch;
            Position player_pos = position_list_[i];
            Position cursor_pos;
            {
                var player = player_list_[i];
                player.roll_target_ += (float)(lr) * 100f * dt_;
                player.roll_ = math.lerp(player.roll_, player.roll_target_, 0.1f);
                float rad = math.radians(player.roll_);
                float sn, cs;
                math.sincos(rad, out sn, out cs);
                vx = hori * cs - vert * sn;
                vy = hori * sn + vert * cs;
                if (hori < 0) {
                    player.pitch_acceleration_ += 2000f;
                } else if (hori > 0) {
                    player.pitch_acceleration_ += -2000f;
                }

                player.pitch_acceleration_ += -player.pitch_ * 8f; // spring
                player.pitch_acceleration_ += -player.pitch_velocity_ * 4f; // friction
                player.pitch_velocity_ += player.pitch_acceleration_ * dt_;
                player.pitch_ += player.pitch_velocity_ * dt_;
                player.pitch_acceleration_ = 0f;
                pitch = player.pitch_;

                const float CURSOR_FORCE = 32f;
                player.cursor_forceXY_ = new float2(vx*CURSOR_FORCE, vy*CURSOR_FORCE);
                cursor_pos = position_list_from_entity_[player.cursor_entity_];
                {
                    float x = player_pos.Value.x + cursor_pos.Value.x;
                    float y = player_pos.Value.y + cursor_pos.Value.y;
                    float len2 = x*x + y*y;
                    float R = 64f;
                    if (len2 > MAX_RADIUS_SQR) {
                        R = 256f;
                    }
                    player.cursor_forceXY_.x += -cursor_pos.Value.x*R;
                    player.cursor_forceXY_.y += -cursor_pos.Value.y*R;
                }

                player_list_[i] = player;
            }
            {
                var diff = cursor_pos.Value - player_pos.Value;
                quaternion rot = math.mul(quaternion.LookRotation(diff, new float3(0f, 1f, 0f)), quaternion.EulerZXY(0f, 0f, math.radians(pitch)));
                rotation_list_[i] = new Rotation{ Value = rot, };
            }
            {
                const float MOVE_FORCE = 1f;
                var rb_pos = rb_position_list_[i];
                rb_pos.addForceXY(vx*MOVE_FORCE, vy*MOVE_FORCE);
                rb_position_list_[i] = rb_pos;
            }
		}
	}

    [BurstCompile]
	struct SumupJob : IJobParallelFor
	{
        public float time_;
        [ReadOnly] public ComponentDataArray<LockTarget> locktarget_list_;
        public NativeLimitedCounter lockon_limit_;

        public void Execute(int i)
        {
            LockTarget lt = locktarget_list_[i];
            if (lt.isLocked(time_)) {
                lockon_limit_.TryIncrement(LASER_MAX);
            }
        }
    }

    [BurstCompile]
	struct LockonJob : IJobParallelFor
	{
        [ReadOnly] public float time_;
        [ReadOnly] public EntityArray locktarget_entity_list_;
        public ComponentDataArray<LockTarget> locktarget_list_;
        [ReadOnly] public ComponentDataArray<LocalToWorld> locktarget_local_to_world_list_;
        public Position player_position_;
        public float3 player_normal_;
        public NativeLimitedCounter lockon_limit_;
        public const float cone_cos_sqr_ = 0.5f; // cos45*cos45
        public NativeQueue<SightSpawnData>.Concurrent sight_spawner_;

        public void Execute(int i)
        {
            LockTarget lt = locktarget_list_[i];
            if (!lt.isLocked(time_)) {
                bool can_lock = false;
                var locktarget_local_to_world = locktarget_local_to_world_list_[i].Value;
                var diff = locktarget_local_to_world.c3.xyz - player_position_.Value;
                float inner_product = math.dot(diff, player_normal_);
                if (inner_product > 0f) {
                    float diffsqr = Vector3.Dot(diff, diff);
                    if (inner_product*inner_product >= diffsqr * cone_cos_sqr_) {
                        if (lt.has_direction_ != 0) {
                            var fwd = new float4(0f, 0f, 1f, 0f);
                            var vec = math.mul(locktarget_local_to_world, fwd).xyz;
                            if (math.dot(diff, vec) < 0f) {
                                can_lock = true;
                            }
                        } else {
                            can_lock = true;
                        }
                    }
                }
                if (can_lock) {
                    if (lockon_limit_.TryIncrement(LASER_MAX /* inclusive_limit */)) {
                        lt.setLocked(time_);
                        locktarget_list_[i] = lt;
                        sight_spawner_.Enqueue(new SightSpawnData { target_entity_ = locktarget_entity_list_[i], });
                    }
                }
            }
        }
    }

    // [BurstCompile]
	struct FireLaserJob : IJobParallelFor
	{
        public float time_;
        public EntityCommandBuffer.Concurrent command_buffer_;
        [ReadOnly] public EntityArray entity_list_;
        public Position player_position_;
        public Rotation player_rotation_;
        public Player player_;
        public ComponentDataArray<LockTarget> locktarget_list_;
        public ComponentDataArray<RandomLocal> random_list_;

        void spawn(ref LockTarget lt,
                   ref float3 pos,
                   ref quaternion rot,
                   ref RandomLocal random,
                   int i,
                   Entity entity,
                   float arrive_period,
                   int job_index)
        {
            ECSLaserManager.spawn(command_buffer_,
                                  ref lt,
                                  ref pos,
                                  ref rot,
                                  ref random,
                                  i,
                                  entity,
                                  arrive_period,
                                  job_index);
        }

        public void Execute(int i)
        {
            var lt = locktarget_list_[i];
            if (lt.isLocked(time_) && !lt.isFired(time_)) {
                var pos = player_position_.Value;
                var rot = player_rotation_.Value;
                var random = random_list_[i];
                lt.setFired(time_);
                var eta = random.range(0f, 0.2f)+1.2f /* arrive_period */;
                lt.setETA(time_ + eta);
                spawn(ref lt,
                      ref pos,
                      ref rot,
                      ref random,
                      i, entity_list_[i],
                      eta,
                      i /* job_index */);
                random_list_[i] = random;
            }
            locktarget_list_[i] = lt;
        }
    }

	struct FireBulletJob : IJob
	{
        [WriteOnly] public NativeQueue<BulletSpawnData>.Concurrent bullet_spawner_;
        public float current_time_;
        public Position player_position_;
        public Rotation player_rotation_;

        public void Execute()
        {
            var vel = math.mul(player_rotation_.Value, new float3(0f, 0f, 48f));
            var pos0 = math.mul(player_rotation_.Value, new float3(0.3f, 0.1f, 0.8f)) + player_position_.Value;
            bullet_spawner_.Enqueue(new BulletSpawnData {
                    position_ = pos0,
                    velocity_ = vel,
                    type_ = 0, // 0: Player
                });
            var pos1 = math.mul(player_rotation_.Value, new float3(-0.3f, 0.1f, 0.8f)) + player_position_.Value;
            bullet_spawner_.Enqueue(new BulletSpawnData {
                    position_ = pos1,
                    velocity_ = vel,
                    type_ = 0, // 0: Player
                });
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        CollisionSystem.Sync();
        CursorSystem.Sync();
        game_camera_update_system_.Sync();

        handle_ = inputDeps;

        if (lockon_limit_.IsCreated) {
            lockon_limit_.Dispose();
        }
        // for (var i = 0; i < player_group_.Length; ++i) {
        int i = 0;              // only one player now.
        {
            var entity = player_group_.entity_list_[i];
            var player = player_group_.player_list_[i];
            var player_position = player_group_.position_list_[i];
            var player_rotation = player_group_.rotation_list_[i];
            
            // Debug.Log(player.life_.get());

            var pc = player_controller_list_from_entity_[entity];
            if (pc.lockon_ != 0) {
                lockon_limit_ = new NativeLimitedCounter(Allocator.TempJob);

                var sumup_job = new SumupJob {
                    time_ = Time.GetCurrent(),
                    locktarget_list_ = lockon_group_.locktarget_list_,
                    lockon_limit_ = lockon_limit_,
                };
                handle_ = sumup_job.Schedule(lockon_group_.Length, 8, handle_);

                var lockon_job = new LockonJob {
                    time_ = Time.GetCurrent(),
                    locktarget_entity_list_ = lockon_group_.entity_list_,
                    locktarget_list_ = lockon_group_.locktarget_list_,
                    locktarget_local_to_world_list_ = lockon_group_.local_to_world_list_,
                    player_position_ = player_position,
                    player_normal_ = math.mul(player_rotation.Value, new float3(0f, 0f, 1f)),
                    lockon_limit_ = lockon_limit_,
                    sight_spawner_ = ECSSightManager.GetSightSpawnDataQueue().ToConcurrent(),
                };
                handle_ = lockon_job.Schedule(lockon_group_.Length, 8, handle_);
            }
            
            if (pc.fire_laser_ != 0) {
                var fire_laser_job = new FireLaserJob {
                    time_ = Time.GetCurrent(),
                    command_buffer_ = barrier_.CreateCommandBuffer().ToConcurrent(),
                    entity_list_ = lockon_group_.entity_list_,
                    player_position_ = player_position,
                    player_rotation_ = player_rotation,
                    player_ = player,
                    locktarget_list_ = lockon_group_.locktarget_list_,
                    random_list_ = lockon_group_.random_list_,
                };
                handle_ = fire_laser_job.Schedule(lockon_group_.Length, 8, handle_);
            }

            if (pc.fire_bullet_ != 0) {
                player.fire_bullet_time_ = Time.GetCurrent();
                player.fire_bullet_count_ = 0;
            }
            const int RENSYA_NUM = 16;
            if (Time.GetCurrent() - player.fire_bullet_time_ >= 0f && player.fire_bullet_count_ < RENSYA_NUM) {
                var fire_bullet_job = new FireBulletJob {
                    bullet_spawner_ = ECSBulletManager.GetBulletSpawnDataQueue().ToConcurrent(),
                    current_time_ = Time.GetCurrent(),
                    player_position_ = player_position,
                    player_rotation_ = player_rotation,
                };
                handle_ = fire_bullet_job.Schedule(handle_);
                const float INTERVAL = 0.05f;
                player.fire_bullet_time_ = Time.GetCurrent() + INTERVAL;
                ++player.fire_bullet_count_;
            }
            player_group_.player_list_[i] = player;
        }

		var job = new PlayerJob {
            dt_ = Time.GetDT(),
            position_list_ = player_group_.position_list_,
            rotation_list_ = player_group_.rotation_list_,
            rb_position_list_ = player_group_.rb_position_list_,
            player_list_ = player_group_.player_list_,
            player_controller_list_ = player_group_.player_controller_list_,
            position_list_from_entity_ = position_list_from_entity_,
		};
		handle_ = job.Schedule(player_group_.Length, 8, handle_);

        return handle_;
	}
}


public class ECSPlayerManager : MonoBehaviour
{
    public static ECSPlayerManager instance_;
    public static ECSPlayerManager Instance { get { if (instance_ == null) { instance_ = GameObject.Find("player_manager").GetComponent<ECSPlayerManager>(); } return instance_; } }

    [SerializeField] GameObject prefab_;
    Material material_;
    Mesh mesh_;
    EntityArchetype player_archetype_;
    EntityArchetype cursor_archetype_;
    EntityArchetype burner_archetype_;
    
    const int PLAYER_MAX = 1;
    int player_spawn_idx_;

	void initialize()
    {
        var mf = prefab_.GetComponent<MeshFilter>();
        mesh_ = mf.sharedMesh;
        var mr = prefab_.GetComponent<MeshRenderer>();
        material_ = mr.sharedMaterial;
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        player_archetype_ = entity_manager.CreateArchetype(typeof(Position)
                                                           , typeof(Rotation)
                                                           , typeof(LocalToWorld)
                                                           , typeof(Destroyable)
                                                           , typeof(RigidbodyPosition)
                                                           , typeof(SphereCollider)
                                                           , typeof(PlayerCollisionInfo)
                                                           , typeof(Player)
                                                           , typeof(PlayerController)
                                                           , typeof(PlayerControllerIndex)
                                                           , typeof(MeshInstanceRenderer)
                                                           );
        cursor_archetype_ = entity_manager.CreateArchetype(typeof(Position)
                                                           , typeof(Rotation)
                                                           , typeof(ScreenPosition)
                                                           , typeof(LocalToWorld)
                                                           , typeof(RigidbodyPosition)
                                                           , typeof(Cursor)
                                                           );
        burner_archetype_ = entity_manager.CreateArchetype(typeof(Position)
                                                           , typeof(TrailData)
                                                           , typeof(TrailPoint)
                                                           , typeof(TrailRenderer)
                                                           );
        player_spawn_idx_ = 0;
	}
	
    void finalize()
    {
    }

    void Awake()
    {
        initialize();
    }
    
    void OnDestroy()
    {
        finalize();
    }

    public Entity spawn(ref Vector3 position, ref Quaternion rotation, ref AtomicFloatResource float_resource)
    {
        Assert.IsTrue(player_spawn_idx_ < PLAYER_MAX);

        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<Unity.Entities.EntityManager>();

        var player_entity = entity_manager.CreateEntity(player_archetype_);
        entity_manager.SetComponentData(player_entity, new Position { Value = position, });
        entity_manager.SetComponentData(player_entity, new Rotation { Value = rotation, });
        float radius = CV.MAX_RADIUS - CV.PLAYER_WIDTH_HALF;
        entity_manager.SetComponentData(player_entity, new RigidbodyPosition { damper_ = 12f, limit_xy_tube_radius_ = radius, });
        entity_manager.SetComponentData(player_entity, new SphereCollider {
                offset_position_ = new float3(0f, 0f, 0f),
                radius_ = 1f,
            });
        entity_manager.SetComponentData(player_entity, new PlayerControllerIndex(0 /* index */));
		var renderer = new MeshInstanceRenderer {
			mesh = mesh_,
			material = material_,
            subMesh = 0,
            castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true,
            // camera = camera_,
		};
		entity_manager.SetSharedComponentData(player_entity, renderer);

        var cursor_entity = entity_manager.CreateEntity(cursor_archetype_);
        var cursor_position = new Vector3(position.x, position.y, position.z + 32f);
        entity_manager.SetComponentData(cursor_entity, new Position { Value = cursor_position, });
        entity_manager.SetComponentData(cursor_entity, new RigidbodyPosition(32f /* damper */));
        entity_manager.SetComponentData(cursor_entity, new LocalToWorld { Value = float4x4.identity, });
        entity_manager.SetComponentData(cursor_entity, Cursor.Create(player_entity));

        entity_manager.SetComponentData(player_entity, Player.Create(cursor_entity, ref float_resource)); // bidirectional reference.

        {
            var burner_entity = entity_manager.CreateEntity(burner_archetype_);
            entity_manager.SetComponentData(burner_entity, new Position { Value = new float3(0.35f, 0.1f, -0.8f), });
            // entity_manager.SetComponentData(burner_entity, new Rotation { Value = quaternion.identity, });
            entity_manager.SetComponentData(burner_entity, new TrailData { color_type_ = (int)TrailManager.ColorType.Cyan, });
            var buffer = entity_manager.GetBuffer<TrailPoint>(burner_entity);
            for (var i = 0; i < buffer.Capacity; ++i) {
                buffer.Add(new TrailPoint { position_ = position, normal_ = new float3(0f, 0f, 1f), });
            }
            entity_manager.SetSharedComponentData(burner_entity, new TrailRenderer {
                    material_ = TrailManager.Instance.getMaterial(),
                });
            var attach_entity = entity_manager.CreateEntity(typeof(Attach));
            entity_manager.SetComponentData(attach_entity, new Attach { Parent = player_entity, Child = burner_entity, });
        }
        {
            var burner_entity = entity_manager.CreateEntity(burner_archetype_);
            entity_manager.SetComponentData(burner_entity, new Position { Value = new float3(-0.35f, 0.1f, -0.8f), });
            // entity_manager.SetComponentData(burner_entity, new Rotation { Value = quaternion.identity, });
            entity_manager.SetComponentData(burner_entity, new TrailData { color_type_ = (int)TrailManager.ColorType.Cyan, });
            var buffer = entity_manager.GetBuffer<TrailPoint>(burner_entity);
            for (var i = 0; i < buffer.Capacity; ++i) {
                buffer.Add(new TrailPoint { position_ = position, normal_ = new float3(0f, 0f, 1f), });
            }
            entity_manager.SetSharedComponentData(burner_entity, new TrailRenderer {
                    material_ = TrailManager.Instance.getMaterial(),
                });
            var attach_entity = entity_manager.CreateEntity(typeof(Attach));
            entity_manager.SetComponentData(attach_entity, new Attach { Parent = player_entity, Child = burner_entity, });
        }

        ++player_spawn_idx_;

        return player_entity;
    }
}

} // namespace UTJ {
