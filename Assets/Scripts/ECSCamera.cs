// using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
// using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Jobs;
// using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

public struct Camera : IComponentData
{
    public const float SCREEN_HEIGHT = 180f; // size of FinalCamera
    public const float SCREEN_WIDTH = SCREEN_HEIGHT*(16f/9f);

	public float4x4 projection_matrix_;
	public float4x4 screen_matrix_;

    public static Camera Create(ref Matrix4x4 projection_matrix) {
        return new Camera {
            projection_matrix_ = projection_matrix,
            screen_matrix_ = float4x4.identity,
        };
    }

    public void update(float3 pos, quaternion rot)
    {
        var view_matrix = new float4x4(rot, pos);
        var view_matrix_i = math.inverse(view_matrix);
        // abobe can be optimized.
        screen_matrix_ = math.mul(projection_matrix_, view_matrix_i);
    }

    public float3 getSpritePosition(ref float4x4 world_mat)
    {
        var world_pos = world_mat.c3;
        var spos = math.mul(screen_matrix_, world_pos);
        if (spos.w == 0f) {
            return new float3(0f, 0f, 0f);
        } else {
            var wr = 1f/spos.w;
            spos.x *= wr;
            spos.y *= wr;
            spos.z *= wr;
            float3 pos = new float3(spos.x*(-SCREEN_WIDTH), spos.y*(-SCREEN_HEIGHT), -spos.z);
            return pos;
        }
    }
    public float3 getSpritePosition(ref float3 world_pos)
    {
        var spos = math.mul(screen_matrix_, new float4(world_pos, 1f));
        if (spos.w == 0f) {
            return new float3(0f, 0f, 0f);
        } else {
            var wr = 1f/spos.w;
            spos.x *= wr;
            spos.y *= wr;
            spos.z *= wr;
            float3 pos = new float3(spos.x*(-SCREEN_WIDTH), spos.y*(-SCREEN_HEIGHT), -spos.z);
            return pos;
        }
    }
}

public class CameraUpdateSystem : JobComponentSystem
{
    struct Group
    {
        public readonly int Length;
        public ComponentDataArray<Camera> camera_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
    }
    [Inject] Group group_;
    
    [BurstCompile]
    struct Job : IJobParallelFor
    {
        public ComponentDataArray<Camera> camera_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
        
        public void Execute(int i)
        {
            var camera = camera_list_[i];
            var pos = position_list_[i];
            var rot = rotation_list_[i];
            camera.update(pos.Value, rot.Value);
            camera_list_[i] = camera;
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;
        
		var job = new Job {
            camera_list_ = group_.camera_list_,
            position_list_ = group_.position_list_,
            rotation_list_ = group_.rotation_list_,
        };
		handle = job.Schedule(group_.Length, 8, handle);

        return handle;
    }    
}


public class CameraApplyTransformSystem : JobComponentSystem
{
    struct Group
    {
        public TransformAccessArray transform_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
        [ReadOnly] public ComponentDataArray<Camera> camera_list_;
    }
    [Inject] Group group_;

    [BurstCompile]
    struct Job : IJobParallelForTransform
    {
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
        
        public void Execute(int i, TransformAccess transform)
        {
            transform.localPosition = (Vector3)position_list_[i].Value;
            transform.localRotation = (Quaternion)rotation_list_[i].Value;
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

		var job = new Job {
            position_list_ = group_.position_list_,
            rotation_list_ = group_.rotation_list_,
        };
		handle = job.Schedule(group_.transform_list_, handle);

        return handle;
    }    
}

} // namespace UTJ {
