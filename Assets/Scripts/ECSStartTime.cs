using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Burst;
// using UnityEngine;
// using Unity.Collections.LowLevel.Unsafe;

namespace UTJ {

public struct StartTime : IComponentData
{
    public float value_;
}

public class StartTimeSystem : JobComponentSystem
{
    struct Group {
        readonly public int Length;
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
        [ReadOnly] public ComponentDataArray<StartTime> start_time_list_;
        [ReadOnly] public ComponentDataArray<Frozen> frozen_list_;
        [WriteOnly] public ComponentDataArray<LocalToWorld> local_to_world_list_;
    }
    [Inject] Group group_;

    struct Job : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<Position> position_list_;
        [ReadOnly] public ComponentDataArray<Rotation> rotation_list_;
        [ReadOnly] public ComponentDataArray<StartTime> start_time_list_;
        [WriteOnly] public ComponentDataArray<LocalToWorld> local_to_world_list_;
        
        public void Execute(int i)
        {
            var pos = position_list_[i].Value;
            var rot = rotation_list_[i].Value;
            var start_time = start_time_list_[i].value_;
            var mat = new float4x4(rot, pos);
            var c0 = mat.c0;
            c0.w = start_time;
            mat.c0 = c0;
            local_to_world_list_[i] = new LocalToWorld { Value = mat, };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDep)
    {
        var handle = inputDep;

        var job = new Job {
            position_list_ = group_.position_list_,
            rotation_list_ = group_.rotation_list_,
            start_time_list_ = group_.start_time_list_,
            local_to_world_list_ = group_.local_to_world_list_,
        };
        handle = job.Schedule(group_.Length, 16, handle);

        return handle;
    }    
}

public class StartTimeRendererSystem : ComponentSystem
{
    EntityArchetypeQuery m_Query;
    UnityEngine.Camera m_Camera;
    UnityEngine.Matrix4x4 m_PrevInvMatrix;
    int m_CurrentTimeID;
    int m_DTID;
    int m_PrevInvMatrixID;

    protected override void OnCreateManager(int capacity)
    {
        m_Query = new EntityArchetypeQuery {
            Any = new ComponentType[] { typeof(MeshInstanceRenderer), typeof(CustomMeshInstanceRenderer), },
            None = System.Array.Empty<ComponentType>(),
            All = new ComponentType[] { typeof(StartTime), },
        };
        m_PrevInvMatrix = UnityEngine.Matrix4x4.identity;

        m_CurrentTimeID = UnityEngine.Shader.PropertyToID("_CurrentTime");
        m_DTID = UnityEngine.Shader.PropertyToID("_DT");
        m_PrevInvMatrixID = UnityEngine.Shader.PropertyToID("_PrevInvMatrix");
    }

    protected override void OnUpdate()
    {
        if (m_Camera == null) {
            var camera = UnityEngine.Camera.main;
            if (camera == null) {
                camera = SystemManager.Instance.getMainCamera();
            }
            m_Camera = camera;
        }
        var foundArchetypes = new NativeList<EntityArchetype>(Allocator.Temp);
        EntityManager.AddMatchingArchetypes(m_Query, foundArchetypes);
        var chunks = EntityManager.CreateArchetypeChunkArray(foundArchetypes, Allocator.Temp);
        var meshInstanceRendererType = GetArchetypeChunkSharedComponentType<MeshInstanceRenderer>();
        var customMeshInstanceRendererType = GetArchetypeChunkSharedComponentType<CustomMeshInstanceRenderer>();
        var matrix = m_PrevInvMatrix * m_Camera.cameraToWorldMatrix; // prev-view * inverted-cur-view
        for (var i = 0; i < chunks.Length; ++i) {
            var chunk = chunks[i];
            var rendererIndex = chunk.GetSharedComponentIndex(meshInstanceRendererType);
            if (rendererIndex >= 0) {
                var renderer = EntityManager.GetSharedComponentData<MeshInstanceRenderer>(rendererIndex);
                var material = renderer.material;
                material.SetFloat(m_CurrentTimeID, Time.GetCurrent());
                material.SetFloat(m_DTID, Time.GetDT());
                material.SetMatrix(m_PrevInvMatrixID, matrix);
            } else {
                rendererIndex = chunk.GetSharedComponentIndex(customMeshInstanceRendererType);
                var renderer = EntityManager.GetSharedComponentData<CustomMeshInstanceRenderer>(rendererIndex);
                var material = renderer.material;
                material.SetFloat(m_CurrentTimeID, Time.GetCurrent());
                material.SetFloat(m_DTID, Time.GetDT());
                material.SetMatrix(m_PrevInvMatrixID, matrix);
            }
        }
        m_PrevInvMatrix = m_Camera.worldToCameraMatrix;
        chunks.Dispose();
        foundArchetypes.Dispose();
    }
}


} // namespace UTJ {
