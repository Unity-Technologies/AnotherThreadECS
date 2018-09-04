using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

public struct LaserData : IComponentData
{
    public float arrive_period_;
    public float end_time_;
    public Entity target_entity_;
    public LockTarget lock_target_;

    public bool isEnd() { return end_time_ != CV.MaxValue; }
    public void setEnd(float time) { end_time_ = time; }
    public float endElapsed(float time) { return time - end_time_; }
}

public class LaserSystem : JobComponentSystem
{
    [Inject] [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;

	struct Group
	{
        [ReadOnly] public EntityArray entity_list_;
		[ReadOnly] public ComponentDataArray<Position> position_list_;
		public ComponentDataArray<RigidbodyPosition> rb_position_list_;
        public ComponentDataArray<LaserData> laser_list_;
        [ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
	}
	[Inject] Group group_;

    static JobHandle handle_;
    public static JobHandle Fence { get { return handle_; } }
    public static void Sync() { handle_.Complete(); }

    [BurstCompile]
	struct Job : IJobParallelFor
	{
        public float time_;
        public float dt_;
        public float flow_z_;
        [ReadOnly] public EntityArray entity_list_;
        public ComponentDataArray<LaserData> laser_list_;
		[ReadOnly] public ComponentDataArray<Position> position_list_;
        public ComponentDataArray<RigidbodyPosition> rb_position_list_;
        [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;
        [ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeQueue<ExplosionSpawnData>.Concurrent explosion_spawner_;

		public void Execute(int i)
		{
            var ld = laser_list_[i];
            if (ld.endElapsed(time_) < 1f) { // not end or still remained.
                var rb = rb_position_list_[i];
                if (ld.isEnd()) {
                    rb.resetForce();
                    rb.resetVelocity(0f, 0f, flow_z_);
                } else {
                    if (!position_list_from_entity_.Exists(ld.target_entity_)) { // target vanished.
                        ld.setEnd(time_);
                        rb.resetVelocity(0f, 0f, flow_z_);
                    } else if (0f < ld.arrive_period_) {
                        var pos = position_list_[i];
                        if (ld.arrive_period_ <= 1f) {
                            var target_pos = position_list_from_entity_[ld.target_entity_];
                            var diff = target_pos.Value - pos.Value;
                            var t = ld.arrive_period_;
                            var t2 = t * t;
                            var a = ((diff - rb.velocity_ * t) * 2f)/t2;
                            rb.addForce(a);
                        }
                        ld.arrive_period_ -= dt_;
                        if (ld.arrive_period_ <= 0f) { // the moment of hit
                            ld.setEnd(time_);
                            ld.lock_target_.parent_life_.add(-10f);
                            rb.resetForce();
                            rb.resetVelocity(0f, 0f, 0f);
                            explosion_spawner_.Enqueue(new ExplosionSpawnData { position_ = pos.Value, });
                        }
                    }
                }
                laser_list_[i] = ld;
                rb_position_list_[i] = rb;
            } else {
                destroyable_list_[i] = Destroyable.Kill;
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        handle_ = inputDeps;

        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        var job = new Job() {
            time_ = Time.GetCurrent(),
            dt_ = Time.GetDT(),
            flow_z_ = CV.FLOW_VELOCITY,
            entity_list_ = group_.entity_list_,
            laser_list_ = group_.laser_list_,
            position_list_ = group_.position_list_,
            rb_position_list_ = group_.rb_position_list_,
            position_list_from_entity_ = position_list_from_entity_,
            trail_data_list_ = group_.trail_data_list_,
            destroyable_list_ = group_.destroyable_list_,
            explosion_spawner_ = ECSExplosionManager.GetExplosionSpawnDataQueue(),
        };
        handle_ = job.Schedule(group_.rb_position_list_.Length, 8, handle_);

        return handle_;
    }
}

public class ECSLaserManager : MonoBehaviour
{
    public static ECSLaserManager instance_;
    public static ECSLaserManager Instance { get { if (instance_ == null) { instance_ = GameObject.Find("laser_manager").GetComponent<ECSLaserManager>(); } Assert.IsTrue(instance_ != null); return instance_; } }

    EntityArchetype archetype_;

    void initialize()
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        archetype_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                    , typeof(Position)
                                                    , typeof(LocalToWorld)
                                                    , typeof(RigidbodyPosition)
                                                    , typeof(TrailData)
                                                    , typeof(TrailPoint)
                                                    , typeof(LaserData)
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

    public static void spawn(EntityCommandBuffer.Concurrent command_buffer,
                             ref LockTarget lt,
                             ref float3 position,
                             ref quaternion rotation,
                             ref RandomLocal random,
                             int index,
                             Entity target,
                             float arrive_period)
    {
        Instance.spawn_internal(command_buffer,
                                ref lt,
                                ref position,
                                ref rotation,
                                ref random,
                                index,
                                target,
                                arrive_period);
    }

    public void spawn_internal(EntityCommandBuffer.Concurrent command_buffer,
                               ref LockTarget lt,
                               ref float3 position,
                               ref quaternion rotation,
                               ref RandomLocal random,
                               int index,
                               Entity target,
                               float arrive_period)
    {
        command_buffer.CreateEntity(archetype_);
        command_buffer.SetComponent(new Position { Value = position, });
        command_buffer.SetComponent(new LaserData {
                end_time_ = CV.MaxValue,
                target_entity_ = target,
                arrive_period_ = arrive_period,
                lock_target_ = lt,
            });
        var rb = new RigidbodyPosition(0f /* damper */);
        const float FIRE_VEL = 8f;
        var fire_velocity = new float3(index%2 == 0 ? FIRE_VEL*2f : -FIRE_VEL*2f,
                                       random.range(-FIRE_VEL, FIRE_VEL),
                                       random.range(-FIRE_VEL, FIRE_VEL));
        rb.velocity_ = math.mul(rotation, fire_velocity);
        command_buffer.SetComponent(rb);
        var td = new TrailData {
            color_type_ = (int)TrailManager.ColorType.Green,
        };
        command_buffer.SetComponent(td);

        {
            var buffer = command_buffer.SetBuffer<TrailPoint>();
            for (var i = 0; i < buffer.Capacity; ++i) {
                buffer.Add(new TrailPoint { position_ = position, normal_ = new float3(0f, 0f, 1f), });
            }
        }

        var material = TrailManager.Instance.getMaterial();
		var renderer = new TrailRenderer {
			material_ = material,
		};
		command_buffer.SetSharedComponent<TrailRenderer>(renderer);
    }
}

} // namespace UTJ {
