using Unity.Entities;
using Unity.Transforms;
// using System.Collections;
// using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using TestJobComponentSystem = TestInterface.JobComponentSystem;

namespace UTJ {

public struct SpriteTestDummy : IComponentData
{
}

[UpdateAfter(typeof(TransformSystem))]
public class SpriteTestSystem : TestJobComponentSystem
{
    struct Group {
        public readonly int Length;
#pragma warning disable 0169
        [ReadOnly] public ComponentDataArray<SpriteTestDummy> exists;
#pragma warning restore 0169
        public ComponentDataArray<TransformMatrix> transform_matrix_list_;
    }
    [Inject] Group group_;

    struct CameraGroup
    {
        public readonly int Length;
        [ReadOnly] public ComponentDataArray<SpriteTestDummy> exists;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
        [ReadOnly] public ComponentDataArray<Camera> camera_list_;
    }
    [Inject] CameraGroup camera_group_;

    [BurstCompile]
    struct Job : IJobParallelFor
    {
        public float time_;
        [ReadOnly] public ComponentDataArray<Camera> camera_list_;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
        [ReadOnly] public ComponentDataArray<TransformMatrix> transform_matrix_list_;
        [NativeDisableContainerSafetyRestriction] public NativeBucket<SpriteData>.Concurrent sprite_data_list_;
        
        public void Execute(int i)
        {
            var camera = camera_list_[0];
            camera.update(position_list_[0].Value,
                          rotation_list_[0].Value);
            var world_mat = transform_matrix_list_[i];
            var screen_pos = camera.getSpritePosition(ref world_mat.Value);
            var spd = new SpriteData();
            spd.setType(SpriteManager.Type.target);
            spd.setPosition(ref screen_pos);
            spd.setColor(1f, 0f, 1f, 1f);
            sprite_data_list_.Add(spd);
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var handle = inputDeps;

        Assert.AreEqual(camera_group_.Length, 1);
        var job = new Job {
            time_ = Time.GetCurrent(),
            camera_list_ = camera_group_.camera_list_,
            position_list_ = camera_group_.position_list_,
            rotation_list_ = camera_group_.rotation_list_,
            transform_matrix_list_ = group_.transform_matrix_list_,
            sprite_data_list_ = SpriteRenderer.GetSpriteDataBucket(),
        };
        handle = job.Schedule(group_.Length, 8, handle);

        return handle;
    }    
}

public class SpriteTest : MonoBehaviour
{
    void OnEnable()
    {
        Color.Initialize();
        SpriteRendererSystem.Ignite();
        TimeSystem.Ignite();

        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(SpriteTestDummy)
                                                        , typeof(Position)
                                                        , typeof(Rotation)
                                                        , typeof(TransformMatrix)
                                                        );
		var entity = entity_manager.CreateEntity(arche_type);
		entity_manager.SetComponentData(entity, new Position { Value = new float3(1f, 2f, 10f), });
    }
}

} // namespace UTJ {
