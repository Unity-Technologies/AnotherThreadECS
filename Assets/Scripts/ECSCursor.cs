// using System;
using Unity.Collections;
using Unity.Entities;
// using Unity.Transforms;
// using Unity.Mathematics;
// using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
// using UnityEngine;
// using UnityEngine.Assertions;
using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

public struct Cursor : IComponentData
{
    public Entity player_entity_;
    public static Cursor Create(Entity player_entity) { return new Cursor { player_entity_ = player_entity, }; }
}

// [UpdateAfter(typeof(PlayerSystem))]
public class CursorSystem : JobComponentSystem
{
    [Inject] [ReadOnly] public ComponentDataFromEntity<Player> player_list_from_entity_;

	struct Group
	{
        public ComponentDataArray<RigidbodyPosition> rb_position_list_;
        [ReadOnly] public ComponentDataArray<Cursor> cursor_list_;
	}
	[Inject] Group group_;
    
    static JobHandle handle_;
    public static JobHandle Fence { get { return handle_; } }
    public static void Sync() { handle_.Complete(); }

    [BurstCompile]
	struct UpdateJob : IJobParallelFor
	{
        [ReadOnly] public ComponentDataFromEntity<Player> player_list_from_entity_;
        [ReadOnly] public ComponentDataArray<Cursor> cursor_list_;
        public ComponentDataArray<RigidbodyPosition> rb_position_list_;

        public void Execute(int i)
        {
            var cursor = cursor_list_[i];
            var player = player_list_from_entity_[cursor.player_entity_];
            {            
                var rb_pos = rb_position_list_[i];
                rb_pos.addForceXY(ref player.cursor_forceXY_);
                rb_position_list_[i] = rb_pos;
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        handle_ = inputDeps;

		var update_job = new UpdateJob {
            player_list_from_entity_ = player_list_from_entity_,
            cursor_list_ = group_.cursor_list_,
            rb_position_list_ = group_.rb_position_list_,
        };
		handle_ = update_job.Schedule(group_.cursor_list_.Length, 8, handle_);

        return handle_;
    }    
}

// [UpdateAfter(typeof(TransformSystem))]
public class CursorRenderSystem : JobComponentSystem
{
	struct Group
	{
        [ReadOnly] public ComponentDataArray<Cursor> cursor_list_;
        [ReadOnly] public ComponentDataArray<ScreenPosition> screen_position_list_;
	}
	[Inject] Group group_;
    
    [BurstCompile]
	struct RenderJob : IJobParallelFor
	{
        [ReadOnly] public ComponentDataArray<ScreenPosition> screen_position_list_;
        [NativeDisableContainerSafetyRestriction] [WriteOnly] public NativeBucket<SpriteData>.Concurrent sprite_data_list_;

        public void Execute(int i)
        {
            var screen_pos = screen_position_list_[i].Value;
            var spd = new SpriteData();
            spd.setType(SpriteManager.Type.cursor);
            spd.setPosition(ref screen_pos);
            spd.setColor(0.1f, 1f, 1f, 1f);
            sprite_data_list_.AddRef(ref spd);
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

		var render_job = new RenderJob {
            screen_position_list_ = group_.screen_position_list_,
            sprite_data_list_ = SpriteRenderer.GetSpriteDataBucket(),
        };
		handle = render_job.Schedule(group_.cursor_list_.Length, 8, handle);
        SpriteRendererSystem.DependOn(handle);

        return handle;
    }
}

} // namespace UTJ {
