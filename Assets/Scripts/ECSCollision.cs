using Unity.Entities;
using Unity.Collections;
// using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

[UpdateAfter(typeof(ColliderUpdateSystem))]
public class CollisionSystem : JobComponentSystem
{
    struct PlayerGroup
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Player> player_list_;
        [ReadOnly] public ComponentDataArray<SphereCollider> sphere_collider_list_;
        [WriteOnly] public ComponentDataArray<PlayerCollisionInfo> collision_info_list_;
    }
    [Inject] PlayerGroup player_group_;

    struct EnemyGroup
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<Enemy> enemy_list_;
        [ReadOnly] public ComponentDataArray<SphereCollider> sphere_collider_list_;
        [WriteOnly] public ComponentDataArray<EnemyCollisionInfo> collision_info_list_;
    }
    [Inject] EnemyGroup enemy_group_;

    struct PlayerBulletGroup
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<SphereCollider> sphere_collider_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        [WriteOnly] public ComponentDataArray<PlayerBulletCollisionInfo> collision_info_list_;
    }
    [Inject] PlayerBulletGroup player_bullet_group_;

    struct EnemyBulletGroup
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<SphereCollider> sphere_collider_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        [WriteOnly] public ComponentDataArray<EnemyBulletCollisionInfo> collision_info_list_;
    }
    [Inject] EnemyBulletGroup enemy_bullet_group_;

    static JobHandle handle_;
    public static JobHandle Fence { get { return handle_; } }
    public static void Sync() { handle_.Complete(); }

    NativeArray<float3> offsets_;
    NativeArray<SphereCollider> player_colliders_;
    NativeArray<SphereCollider> enemy_colliders_;
    NativeArray<SphereCollider> player_bullet_colliders_;
    NativeArray<SphereCollider> enemy_bullet_colliders_;
    NativeMultiHashMap<int, int> player_hashmap_;
    NativeMultiHashMap<int, int> enemy_hashmap_;
    NativeMultiHashMap<int, int> player_bullet_hashmap_;
    NativeMultiHashMap<int, int> enemy_bullet_hashmap_;

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
        offsets_ = new NativeArray<float3>(27-1, Allocator.Persistent);
        offsets_[ 0] = new float3( 1f,  1f,  1f);
        offsets_[ 1] = new float3( 1f,  1f,  0f);
        offsets_[ 2] = new float3( 1f,  1f, -1f);
        offsets_[ 3] = new float3( 1f,  0f,  1f);
        offsets_[ 4] = new float3( 1f,  0f,  0f);
        offsets_[ 5] = new float3( 1f,  0f, -1f);
        offsets_[ 6] = new float3( 1f, -1f,  1f);
        offsets_[ 7] = new float3( 1f, -1f,  0f);
        offsets_[ 8] = new float3( 1f, -1f, -1f);

        offsets_[ 9] = new float3( 0f,  1f,  1f);
        offsets_[10] = new float3( 0f,  1f,  0f);
        offsets_[11] = new float3( 0f,  1f, -1f);
        offsets_[12] = new float3( 0f,  0f,  1f);
        // removed center
        offsets_[13] = new float3( 0f,  0f, -1f);
        offsets_[14] = new float3( 0f, -1f,  1f);
        offsets_[15] = new float3( 0f, -1f,  0f);
        offsets_[16] = new float3( 0f, -1f, -1f);

        offsets_[17] = new float3(-1f,  1f,  1f);
        offsets_[18] = new float3(-1f,  1f,  0f);
        offsets_[19] = new float3(-1f,  1f, -1f);
        offsets_[20] = new float3(-1f,  0f,  1f);
        offsets_[21] = new float3(-1f,  0f,  0f);
        offsets_[22] = new float3(-1f,  0f, -1f);
        offsets_[23] = new float3(-1f, -1f,  1f);
        offsets_[24] = new float3(-1f, -1f,  0f);
        offsets_[25] = new float3(-1f, -1f, -1f);
    }

    protected override void OnDestroyManager()
    {
        offsets_.Dispose();
        base.OnDestroyManager();
    }
    
    [BurstCompile]
    struct HashPositions : IJobParallelFor
    {
        public float cell_radius_;
        [ReadOnly] public ComponentDataArray<SphereCollider> sphere_collider_list_;
        [WriteOnly] public NativeMultiHashMap<int, int>.Concurrent hashmap_;
        [ReadOnly] public NativeArray<float3> offsets_;
        
        public void Execute(int i)
        {
            var sc = sphere_collider_list_[i];
            var center = sc.position_;
            var hashCenter = GridHash.Hash(ref center, cell_radius_);
            hashmap_.Add(hashCenter, i);
            for (var j = 0; j < offsets_.Length; ++j) {
                var offset = center + offsets_[j] * sc.radius_;
                var hash = GridHash.Hash(ref offset, cell_radius_);
                if (hash != hashCenter) {
                    hashmap_.Add(hash, i);
                }
            }
        }
    }

    [BurstCompile]
    struct CheckEnemyToPlayer : IJobParallelFor
    {
        public float cell_radius_;
        [ReadOnly] public NativeArray<SphereCollider> enemy_colliders_;
        [ReadOnly] public NativeArray<SphereCollider> player_colliders_;
        [ReadOnly] public ComponentDataArray<Enemy> enemy_list_;
        [ReadOnly] public ComponentDataArray<Player> player_list_;
        [ReadOnly] public NativeMultiHashMap<int, int> player_hashmap_;

        public void Execute(int i)
        {
            var coll0 = enemy_colliders_[i];
            int hash = GridHash.Hash(ref coll0.position_, cell_radius_);
            int index;
            NativeMultiHashMapIterator<int> it;
            for (bool success = player_hashmap_.TryGetFirstValue(hash, out index, out it);
                 success;
                 success = player_hashmap_.TryGetNextValue(out index, ref it)) {
                var coll1 = player_colliders_[index];
                if (coll0.intersect(ref coll1)) {
                    var enemy = enemy_list_[i];
                    enemy.life_.add(CV.MinValue); // kill
                    var player = player_list_[index];
                    player.life_.add(-99f); // damage
                    break;
                }
            }
        }
    }

    [BurstCompile]
    struct CheckEnemyBulletToPlayer : IJobParallelFor
    {
        public float time_;
        public float cell_radius_;
        [ReadOnly] public NativeArray<SphereCollider> enemy_bullet_colliders_;
        [ReadOnly] public NativeArray<SphereCollider> player_colliders_;
        [ReadOnly] public NativeMultiHashMap<int, int> player_hashmap_;
        [ReadOnly] public ComponentDataArray<Player> player_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        [WriteOnly] public ComponentDataArray<EnemyBulletCollisionInfo> enemy_bullet_collision_info_list_;

        public void Execute(int i)
        {
            var coll0 = enemy_bullet_colliders_[i];
            int hash = GridHash.Hash(ref coll0.position_, cell_radius_);
            int index;
            NativeMultiHashMapIterator<int> it;
            for (bool success = player_hashmap_.TryGetFirstValue(hash, out index, out it);
                 success;
                 success = player_hashmap_.TryGetNextValue(out index, ref it)) {
                var coll1 = player_colliders_[index];
                if (coll0.intersect(ref coll1)) {
                    var mid_pos = (coll0.position_ + coll1.position_)*0.5f;
                    enemy_bullet_collision_info_list_[i] = EnemyBulletCollisionInfo.Hit(ref mid_pos);
                    var player = player_list_[index];
                    player.life_.add(-10f); // damage
                    break;
                }
            }
        }
    }

    [BurstCompile]
    struct CheckPlayerBulletToEnemy : IJobParallelFor
    {
        public float time_;
        public float cell_radius_;
        [ReadOnly] public NativeArray<SphereCollider> player_bullet_colliders_;
        [ReadOnly] public NativeArray<SphereCollider> enemy_colliders_;
        [ReadOnly] public NativeMultiHashMap<int, int> enemy_hashmap_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
        [ReadOnly] public ComponentDataArray<Enemy> enemy_list_;
        [WriteOnly] public ComponentDataArray<PlayerBulletCollisionInfo> player_bullet_collision_info_list_;

        public void Execute(int i)
        {
            var coll0 = player_bullet_colliders_[i];
            int hash = GridHash.Hash(ref coll0.position_, cell_radius_);
            int index;
            NativeMultiHashMapIterator<int> it;
            for (bool success = enemy_hashmap_.TryGetFirstValue(hash, out index, out it);
                 success;
                 success = enemy_hashmap_.TryGetNextValue(out index, ref it)) {
                var coll1 = enemy_colliders_[index];
                if (coll0.intersect(ref coll1)) {
                    var mid_pos = (coll0.position_ + coll1.position_)*0.5f;
                    player_bullet_collision_info_list_[i] = PlayerBulletCollisionInfo.Hit(ref mid_pos);
                    var enemy = enemy_list_[index];
                    enemy.life_.add(-5f);
                    break;
                }
            }
        }
    }

    void dispose_resources()
    {
        if (enemy_bullet_hashmap_.IsCreated)
            enemy_bullet_hashmap_.Dispose();
        if (player_bullet_hashmap_.IsCreated)
            player_bullet_hashmap_.Dispose();
        if (enemy_hashmap_.IsCreated)
            enemy_hashmap_.Dispose();
        if (player_hashmap_.IsCreated)
            player_hashmap_.Dispose();
        if (enemy_bullet_colliders_.IsCreated)
            enemy_bullet_colliders_.Dispose();
        if (player_bullet_colliders_.IsCreated)
            player_bullet_colliders_.Dispose();
        if (enemy_colliders_.IsCreated)
            enemy_colliders_.Dispose();
        if (player_colliders_.IsCreated)
            player_colliders_.Dispose();
    }

    protected override void OnStopRunning()
    {
        dispose_resources();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        dispose_resources();

        handle_ = inputDeps;

        player_colliders_ = new NativeArray<SphereCollider>(player_group_.Length,
                                                            Allocator.TempJob,
                                                            NativeArrayOptions.UninitializedMemory);
        enemy_colliders_ = new  NativeArray<SphereCollider>(enemy_group_.Length,
                                                            Allocator.TempJob,
                                                            NativeArrayOptions.UninitializedMemory);
        player_bullet_colliders_ = new  NativeArray<SphereCollider>(player_bullet_group_.Length,
                                                                    Allocator.TempJob,
                                                                    NativeArrayOptions.UninitializedMemory);
        enemy_bullet_colliders_ = new  NativeArray<SphereCollider>(enemy_bullet_group_.Length,
                                                                   Allocator.TempJob,
                                                                   NativeArrayOptions.UninitializedMemory);
        player_hashmap_ = new NativeMultiHashMap<int, int>(player_group_.Length*27 /* expanded */,
                                                           Allocator.TempJob);
        enemy_hashmap_ = new NativeMultiHashMap<int, int>(enemy_group_.Length*27 /* expanded */,
                                                          Allocator.TempJob);
        player_bullet_hashmap_ = new NativeMultiHashMap<int, int>(player_bullet_group_.Length*27 /* expand */,
                                                                  Allocator.TempJob);
        enemy_bullet_hashmap_ = new NativeMultiHashMap<int, int>(enemy_bullet_group_.Length*27 /* expand */,
                                                                 Allocator.TempJob);

        var initial_player_collider_job = new CopyComponentData<SphereCollider> {
            Source  = player_group_.sphere_collider_list_,
            Results = player_colliders_,
        };
        var initial_player_collider_handle_ = initial_player_collider_job.Schedule(player_group_.Length, 32, handle_);

        var initial_enemy_collider_job = new CopyComponentData<SphereCollider> {
            Source  = enemy_group_.sphere_collider_list_,
            Results = enemy_colliders_,
        };
        var initial_enemy_collider_handle_ = initial_enemy_collider_job.Schedule(enemy_group_.Length, 32, handle_);

        var initial_player_bullet_collider_job = new CopyComponentData<SphereCollider> {
            Source  = player_bullet_group_.sphere_collider_list_,
            Results = player_bullet_colliders_,
        };
        var initial_player_bullet_collider_handle_ = initial_player_bullet_collider_job.Schedule(player_bullet_group_.Length,
                                                                                                 32, handle_);

        var initial_enemy_bullet_collider_job = new CopyComponentData<SphereCollider> {
            Source  = enemy_bullet_group_.sphere_collider_list_,
            Results = enemy_bullet_colliders_,
        };
        var initial_enemy_bullet_collider_handle_ = initial_enemy_bullet_collider_job.Schedule(enemy_bullet_group_.Length,
                                                                                               32, handle_);

        const float CELL_RADIUS = 8f;
        var initial_player_hashmap_job = new HashPositions {
            cell_radius_ = CELL_RADIUS,
            offsets_ = offsets_,
            sphere_collider_list_ = player_group_.sphere_collider_list_,
            hashmap_ = player_hashmap_,
        };
        var initial_player_hashmap_handle_ = initial_player_hashmap_job.Schedule(player_group_.Length, 32, handle_);

        var initial_enemy_hashmap_job = new HashPositions {
            cell_radius_ = CELL_RADIUS,
            offsets_ = offsets_,
            sphere_collider_list_ = enemy_group_.sphere_collider_list_,
            hashmap_ = enemy_hashmap_,
        };
        var initial_enemy_hashmap_handle_ = initial_enemy_hashmap_job.Schedule(enemy_group_.Length, 32, handle_);

        var initial_player_bullet_hashmap_job = new HashPositions {
            cell_radius_ = CELL_RADIUS,
            offsets_ = offsets_,
            sphere_collider_list_ = player_bullet_group_.sphere_collider_list_,
            hashmap_ = player_bullet_hashmap_,
        };
        var initial_player_bullet_hashmap_handle_ = initial_player_bullet_hashmap_job.Schedule(player_bullet_group_.Length,
                                                                                               32, handle_);
        var initial_enemy_bullet_hashmap_job = new HashPositions {
            cell_radius_ = CELL_RADIUS,
            offsets_ = offsets_,
            sphere_collider_list_ = enemy_bullet_group_.sphere_collider_list_,
            hashmap_ = enemy_bullet_hashmap_,
        };
        var initial_enemy_bullet_hashmap_handle_ = initial_enemy_bullet_hashmap_job.Schedule(enemy_bullet_group_.Length,
                                                                                             32, handle_);

        {
            var handle_s = new NativeArray<JobHandle>(8, Allocator.Temp);
            var idx = 0;
            handle_s[idx] = initial_player_collider_handle_; ++idx;
            handle_s[idx] = initial_enemy_collider_handle_; ++idx;
            handle_s[idx] = initial_player_bullet_collider_handle_; ++idx;
            handle_s[idx] = initial_enemy_bullet_collider_handle_; ++idx;
            handle_s[idx] = initial_player_hashmap_handle_; ++idx;
            handle_s[idx] = initial_enemy_hashmap_handle_; ++idx;
            handle_s[idx] = initial_player_bullet_hashmap_handle_; ++idx;
            handle_s[idx] = initial_enemy_bullet_hashmap_handle_; ++idx;
            handle_ = JobHandle.CombineDependencies(handle_s);
            handle_s.Dispose();
        }
        
        {
            var job = new CheckEnemyToPlayer {
                cell_radius_ = CELL_RADIUS,
                enemy_colliders_ = enemy_colliders_,
                player_colliders_ = player_colliders_,
                player_hashmap_ = player_hashmap_,
                enemy_list_ = enemy_group_.enemy_list_,
                player_list_ = player_group_.player_list_,
            };
            handle_ = job.Schedule(enemy_group_.Length, 32, handle_);
        }
        {
            var job = new CheckEnemyBulletToPlayer {
                time_ = Time.GetCurrent(),
                cell_radius_ = CELL_RADIUS,
                enemy_bullet_colliders_ = enemy_bullet_colliders_,
                player_colliders_ = player_colliders_,
                player_hashmap_ = player_hashmap_,
                player_list_ = player_group_.player_list_,
                destroyable_list_ = enemy_bullet_group_.destroyable_list_,
                enemy_bullet_collision_info_list_ = enemy_bullet_group_.collision_info_list_,
            };
            handle_ = job.Schedule(enemy_bullet_group_.Length, 32, handle_);
        }
        {
            var job = new CheckPlayerBulletToEnemy {
                time_ = Time.GetCurrent(),
                cell_radius_ = CELL_RADIUS,
                player_bullet_colliders_ = player_bullet_colliders_,
                enemy_colliders_ = enemy_colliders_,
                enemy_hashmap_ = enemy_hashmap_,
                destroyable_list_ = player_bullet_group_.destroyable_list_,
                enemy_list_ = enemy_group_.enemy_list_,
                player_bullet_collision_info_list_ = player_bullet_group_.collision_info_list_,
            };
            handle_ = job.Schedule(player_bullet_group_.Length, 32, handle_);
        }

        return handle_;
    }

}

} // namespace UTJ {

