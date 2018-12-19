using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

public struct Zako : IComponentData
{
    public float3 target_position_;
}

// [UpdateBefore(typeof(RigidbodyPositionSystem))]
[UpdateAfter(typeof(BulletSpawnSystem))]
public class ZakoSystem : JobComponentSystem
{
    [Inject] [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;

	struct Group
	{
		[ReadOnly] public ComponentDataArray<Enemy> enemy_list_;
		[ReadOnly] public ComponentDataArray<Position> position_list_;
		public ComponentDataArray<RigidbodyPosition> rb_pos_list_;
		[ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
		public ComponentDataArray<RigidbodyRotation> rb_rot_list_;
		public ComponentDataArray<RandomLocal> random_list_;
		[ReadOnly] public ComponentDataArray<Zako> zako_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
	}
	[Inject] Group group_;

    [Inject] RigidbodyPositionSystem rigidbody_position_system_;

    static JobHandle handle_;
    public static JobHandle Fence { get { return handle_; } }
    public static void Sync() { handle_.Complete(); }

    [BurstCompile]
	struct ZakoJob : IJobParallelFor
	{
        public float dt_;
        public float time_;
        public Position player_pos_;
        public Group group_;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeQueue<BulletSpawnData>.Concurrent bullet_spawner_;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeQueue<ExplosionSpawnData>.Concurrent explosion_spawner_;
        
		public void Execute(int i)
		{
            var enemy = group_.enemy_list_[i];
            var pos = group_.position_list_[i];
            if (enemy.life_.get() <= 0f) {
                enemy.life_.Dispose();
                group_.destroyable_list_[i] = Destroyable.Kill;
                explosion_spawner_.Enqueue(new ExplosionSpawnData { position_ = pos.Value, });
                return;
            }
            
            var rb_pos = group_.rb_pos_list_[i];
            var rb_rot = group_.rb_rot_list_[i];
            var zako = group_.zako_list_[i];
            var random = group_.random_list_[i];

            var diff = zako.target_position_ - pos.Value;
            rb_pos.addForce(new float3(diff.x*0.5f, diff.y*0.5f, 10f));
			rb_rot.addTorqueZ(-rb_pos.velocity_.x * 2f);

			if (random.probabilityForSecond(1f, dt_) && math.abs(diff.z) < 32f) {
                var target_pos = player_pos_.Value;
				target_pos.z += random.range(-10f, 10f);
				var vel = math.normalize(target_pos - pos.Value) * 48f;
                bullet_spawner_.Enqueue(new BulletSpawnData {
                        position_ = pos.Value,
                        velocity_ = vel,
                        type_ = 1, // 1: Enemy
                    });
            }

            group_.random_list_[i] = random;
            group_.rb_pos_list_[i] = rb_pos;
            group_.rb_rot_list_[i] = rb_rot;
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        rigidbody_position_system_.Sync();

        handle_ = inputDeps;
        
        Entity player_entity = SystemManager.Instance.getPrimaryPlayer();
		var job = new ZakoJob {
            time_ = Time.GetCurrent(),
            dt_ = Time.GetDT(),
            player_pos_ = position_list_from_entity_[player_entity],
            group_ = group_,
            bullet_spawner_ = ECSBulletManager.GetBulletSpawnDataQueue().ToConcurrent(),
            explosion_spawner_ = ECSExplosionManager.GetExplosionSpawnDataQueue().ToConcurrent(),
		};
        handle_ = job.Schedule(group_.zako_list_.Length, 8, handle_);

        return handle_;
	}

}

public class ECSZakoManager : MonoBehaviour
{
    public static ECSZakoManager instance_;
    public static ECSZakoManager Instance { get { if (instance_ == null) { instance_ = GameObject.Find("zako_manager").GetComponent<ECSZakoManager>(); } return instance_; } }

    [SerializeField] GameObject prefab_;
    Material material_;
    Mesh mesh_;
    Transform[] locktarget_list_;
    EntityArchetype archetype_;
    
	void initialize()
    {
        var mf = prefab_.GetComponent<MeshFilter>();
        mesh_ = mf.sharedMesh;
        var mr = prefab_.GetComponent<MeshRenderer>();
        material_ = mr.sharedMaterial;
        var locktarget_num = prefab_.transform.childCount;
        locktarget_list_ = new Transform[locktarget_num];
        for (var i = 0; i < locktarget_list_.Length; ++i) {
            locktarget_list_[i] = prefab_.transform.GetChild(i);
        }
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        archetype_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                    , typeof(Position)
                                                    , typeof(Rotation)
                                                    , typeof(LocalToWorld)
                                                    , typeof(RigidbodyPosition)
                                                    , typeof(RigidbodyRotation)
                                                    , typeof(RandomLocal)
                                                    , typeof(SphereCollider)
                                                    , typeof(EnemyCollisionInfo)
                                                    , typeof(AlivePeriod)
                                                    , typeof(Enemy)
                                                    , typeof(Zako)
                                                    , typeof(MeshRenderBounds)
                                                    , typeof(WorldMeshRenderBounds)
                                                    , typeof(MeshInstanceRenderer)
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

    public void spawn(ref float3 position,
                      ref quaternion rotation,
                      ref RandomLocal random,
                      ref AtomicFloatResource float_resource)
    {
        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<Unity.Entities.EntityManager>();
        var entity = entity_manager.CreateEntity(archetype_);
        entity_manager.SetComponentData(entity, new Position { Value = position, });
        entity_manager.SetComponentData(entity, new Rotation { Value = rotation, });
        entity_manager.SetComponentData(entity, new RigidbodyRotation(10f /* damper */));
        entity_manager.SetComponentData(entity, random.create());
        entity_manager.SetComponentData(entity, new SphereCollider {
                offset_position_ = new float3(0f, 0f, 0f),
                radius_ = 1f,
            });
        entity_manager.SetComponentData(entity, AlivePeriod.Create(Time.GetCurrent(), 6f /* period */));
        var enemy = Enemy.Create(ref float_resource);
        entity_manager.SetComponentData(entity, enemy);
        entity_manager.SetComponentData(entity, new Zako { target_position_ = new float3(random.range(-20f, 20f), random.range(-20f, 20f), 0f), });
        entity_manager.SetComponentData(entity, new MeshRenderBounds { Center = new float3(0f,0f,0f), Radius = 0.5f, });
		var renderer = new MeshInstanceRenderer {
			mesh = mesh_,
			material = material_,
            subMesh = 0,
            castShadows = UnityEngine.Rendering.ShadowCastingMode.On,
            receiveShadows = true,
		};
		entity_manager.SetSharedComponentData(entity, renderer);

        for (var i = 0; i < locktarget_list_.Length; ++i) {
            LockTargetManager.CreateEntity(entity_manager,
                                           locktarget_list_[i].localPosition,
                                           locktarget_list_[i].localRotation,
                                           ref enemy.life_,
                                           entity,
                                           ref random);
        }
    }
}


} // namespace UTJ {
