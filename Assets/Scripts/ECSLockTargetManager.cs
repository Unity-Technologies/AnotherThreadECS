// using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
// using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
// using UnityEngine.Assertions;

namespace UTJ {

public struct LockTarget : IComponentData
{
    public static LockTarget Create(ref AtomicFloat parent_life, bool has_direction = false) {
        return new LockTarget() {
            locked_time_ = CV.MaxValue,
            fired_time_ = CV.MaxValue,
            estimated_time_arrival_ = CV.MaxValue,
            parent_life_ = parent_life,
            has_direction_ = has_direction ? 1 : 0,
        };
    }

    public float locked_time_;
    public float fired_time_;
    public float estimated_time_arrival_;
    public AtomicFloat parent_life_;
    public int has_direction_;
    const float CAN_BE_LOCKED_ON_AFTER_HIT = 0.25f;

    public bool isLocked(float time) { return time >= locked_time_ && estimated_time_arrival_ + CAN_BE_LOCKED_ON_AFTER_HIT > time; }
    public void setLocked(float time) { locked_time_ = time; }
    public bool isFired(float time) { return time >= fired_time_ && estimated_time_arrival_ + CAN_BE_LOCKED_ON_AFTER_HIT > time;; }
    public void setFired(float time) { fired_time_ = time; }
    public void setETA(float time) { estimated_time_arrival_ = time; }
}


public class LockTargetSystem : JobComponentSystem
{
    [Inject] [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;

    struct Group
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<LockTarget> locktarget_list_;
        [ReadOnly] public ComponentDataArray<TransformParent> parent_list_;
        [ReadOnly] public ComponentDataArray<ScreenPosition> screen_position_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;
    }
    [Inject] Group group_;

    [BurstCompile]
    struct CleanupJob : IJobParallelFor
    {
        public float time_;
        [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;
        [ReadOnly] public ComponentDataArray<TransformParent> parent_list_;
        [WriteOnly] public ComponentDataArray<Destroyable> destroyable_list_;

        public void Execute(int i)
        {
            TransformParent parent = parent_list_[i];
            if (!position_list_from_entity_.Exists(parent.Value)) {
                destroyable_list_[i] = Destroyable.Kill;
            }
        }
    }

    [BurstCompile]
    struct LockonMarkJob : IJobParallelFor
	{
        public float time_;
        [ReadOnly] public ComponentDataArray<LockTarget> locktarget_list_;
        [ReadOnly] public ComponentDataArray<ScreenPosition> screen_position_list_;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeBucket<SpriteData>.Concurrent sprite_data_list_;

        public void Execute(int i)
        {
            LockTarget lt = locktarget_list_[i];
            if (lt.isLocked(time_)) {
                var screen_pos = screen_position_list_[i];
                var spd = new SpriteData();
                spd.setType(SpriteManager.Type.target);
                spd.setPosition(ref screen_pos.Value);
                if (lt.isFired(time_)) {
                    spd.setColor(1f, 0.25f, 0.2f, 1f);
                } else {
                    spd.setColor(0.1f, 1f, 0.5f, 1f);
                }
                sprite_data_list_.AddRef(ref spd);
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

        var cleanup_job = new CleanupJob {
            time_ = Time.GetCurrent(),
            position_list_from_entity_ = position_list_from_entity_,
            parent_list_ = group_.parent_list_,
            destroyable_list_ = group_.destroyable_list_,
        };
        handle = cleanup_job.Schedule(group_.Length, 8, handle);

        var lockon_mark_job = new LockonMarkJob {
            time_ = Time.GetCurrent(),
            locktarget_list_ = group_.locktarget_list_,
            screen_position_list_ = group_.screen_position_list_,
            sprite_data_list_ = SpriteRenderer.GetSpriteDataBucket(),
        };
        handle = lockon_mark_job.Schedule(group_.locktarget_list_.Length, 8, handle);
        SpriteRendererSystem.DependOn(handle);

        return handle;
    }
}

public static class LockTargetManager
{
    static EntityArchetype archetype_;

    public static void Init()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
        archetype_ = entity_manager.CreateArchetype(typeof(Destroyable)
                                                    , typeof(LockTarget)
                                                    , typeof(TransformParent)
                                                    , typeof(LocalPosition)
                                                    , typeof(LocalRotation)
                                                    , typeof(Position)
                                                    , typeof(Rotation)
                                                    , typeof(ScreenPosition)
                                                    , typeof(TransformMatrix)
                                                    , typeof(RandomLocal)
                                                    );
    }

    public static Entity CreateEntity(EntityManager entity_manager,
                                      UnityEngine.Vector3 localPos,
                                      UnityEngine.Quaternion localRot,
                                      ref AtomicFloat life,
                                      ref Entity parent_entity,
                                      ref RandomLocal random)
    {
        var entity = entity_manager.CreateEntity(archetype_);
        entity_manager.SetComponentData(entity, LockTarget.Create(ref life));
        entity_manager.SetComponentData(entity, new LocalPosition(localPos));
        entity_manager.SetComponentData(entity, new LocalRotation(localRot));
        entity_manager.SetComponentData(entity, new TransformParent(parent_entity));
        entity_manager.SetComponentData(entity, random.create());
        return entity;
    }
}

} // namespace UTJ {
