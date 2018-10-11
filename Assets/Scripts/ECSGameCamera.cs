// using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
// using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
// using UnityEngine;
// using UnityEngine.Assertions;
using UnityEngine.Jobs;
// using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

[System.Serializable]
public struct GameCamera : IComponentData
{
    public Entity player_entity_;
    public float3 look_target_;

    public static GameCamera Create(Entity player_entity) {
        return new GameCamera {
            player_entity_ = player_entity,
            look_target_ = new float3(0f, 0f, 0f),
        };
    }
}

[UpdateAfter(typeof(PlayerSystem))]
public class GameCameraUpdateSystem : JobComponentSystem
{
    [Inject] [ReadOnly] public ComponentDataFromEntity<Player> player_list_from_entity_;
    [Inject] [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;

    struct Group
    {
        public readonly int Length;
        public ComponentDataArray<GameCamera> game_camera_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [WriteOnly] public ComponentDataArray<Rotation> rotation_list_;
    }
    [Inject] Group group_;
    
    JobHandle handle_;
    public JobHandle Fence { get { return handle_; } }
    public void Sync() { handle_.Complete(); }

    [BurstCompile]
    struct Job : IJobParallelFor
    {
        public ComponentDataArray<GameCamera> game_camera_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [WriteOnly] public ComponentDataArray<Rotation> rotation_list_;
        [ReadOnly] public ComponentDataFromEntity<Player> player_list_from_entity_;
        [ReadOnly] public ComponentDataFromEntity<Position> position_list_from_entity_;
        
        public void Execute(int i)
        {
            var game_camera = game_camera_list_[i];
            var player = player_list_from_entity_[game_camera.player_entity_];
            var player_pos = position_list_from_entity_[game_camera.player_entity_];
            var pos = position_list_[i];

            var x = player_pos.Value.x;
            var y = player_pos.Value.y;
            var z = player_pos.Value.z;
            float roll = math.radians(player.roll_);
            float sn, cs;
            math.sincos(-roll, out sn, out cs);
            float3 pos0 = new float3(cs * x - sn * y,
                                     sn * x + cs * y,
                                     z);
            var diff = pos0 - pos.Value;
            diff.x = diff.x * 0.5f;
            diff.y = diff.y * 0.5f;
            game_camera.look_target_ = math.lerp(game_camera.look_target_, diff, 0.2f);
            var rot = math.mul(quaternion.EulerZXY(0f, 0f, roll), quaternion.LookRotation(game_camera.look_target_, new float3(0f, 1f, 0f)));
            
            rotation_list_[i] = new Rotation { Value = rot, };
            game_camera_list_[i] = game_camera;
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        handle_ = inputDeps;
        
		var job = new Job {
            game_camera_list_ = group_.game_camera_list_,
            position_list_ = group_.position_list_,
            rotation_list_ = group_.rotation_list_,
            player_list_from_entity_ = player_list_from_entity_,
            position_list_from_entity_ = position_list_from_entity_,
        };
		handle_ = job.Schedule(group_.Length, 8, handle_);

        return handle_;
    }    
}

} // namespace UTJ {
