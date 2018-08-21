using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
// using Unity.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace UTJ {

public struct MeshInstanceRendererWithTime : ISharedComponentData
{
    public Mesh                 mesh;
    public Material             material;
    public int                  subMesh;
    public ShadowCastingMode    castShadows;
    public bool                 receiveShadows;

    public UnityEngine.Camera   camera;
    public bool                 needDT;
    public bool                 needPrevMatrix;
    public int                  layer;
}

public struct StartTime : IComponentData
{
    public float value_;
}

/// <summary>
/// Renders all Entities containing both MeshInstanceRendererWithTime & TransformMatrix & StartTime components.
/// </summary>
[UnityEngine.ExecuteInEditMode]
public class MeshInstanceRendererWithTimeSystem : ComponentSystem
{
    // Instance renderer takes only batches of 1023
    Matrix4x4[]                 m_MatricesArray = new Matrix4x4[1023];
    float[]                     m_StartTimeArray = new float[1023];
    List<MeshInstanceRendererWithTime>  m_CacheduniqueRendererTypes = new List<MeshInstanceRendererWithTime>(10);
    ComponentGroup              m_InstanceRendererGroup;
    int m_CurrentTimeID;
    int m_DTID;
    int m_StartTimesID;
    int m_PrevInvMatrixID;
    Matrix4x4 m_PrevInvMatrix;

    public unsafe static void CopyMatrices(ComponentDataArray<TransformMatrix> transforms,
                                           int beginIndex,
                                           int length,
                                           Matrix4x4[] outMatrices)
    {
        fixed (Matrix4x4* matricesPtr = outMatrices) {
            Assert.AreEqual(sizeof(Matrix4x4), sizeof(TransformMatrix));
            var matricesSlice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<TransformMatrix>(matricesPtr,
                                                                                                           sizeof(Matrix4x4),
                                                                                                           length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref matricesSlice,
                                                               AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
            transforms.CopyTo(matricesSlice, beginIndex);
        }
    }

    public unsafe static void CopyFloats(ComponentDataArray<StartTime> values,
                                         int beginIndex,
                                         int length,
                                         float[] outFloats)
    {
        fixed (float* ptr = outFloats) {
            Assert.AreEqual(sizeof(float), sizeof(StartTime));
            var slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<StartTime>(ptr, sizeof(float), length);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref slice, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
#endif
            values.CopyTo(slice, beginIndex);
        }
    }

    protected override void OnCreateManager(int capacity)
    {
        m_InstanceRendererGroup = GetComponentGroup(typeof(MeshInstanceRendererWithTime),
                                                    typeof(TransformMatrix),
                                                    typeof(StartTime));
        m_CurrentTimeID = Shader.PropertyToID("_CurrentTime");
        m_StartTimesID = Shader.PropertyToID("_StartTimes");
        m_DTID = Shader.PropertyToID("_DT");
        m_PrevInvMatrixID = Shader.PropertyToID("_PrevInvMatrix");
        m_PrevInvMatrix = Matrix4x4.identity;
    }

    protected override void OnUpdate()
	{
        float current_time = Time.GetCurrent();
        float dt = Time.GetDT();
        EntityManager.GetAllUniqueSharedComponentDatas(m_CacheduniqueRendererTypes);
	    var forEachFilter = m_InstanceRendererGroup.CreateForEachFilter(m_CacheduniqueRendererTypes);

        for (int i = 0; i != m_CacheduniqueRendererTypes.Count; ++i) {
            var renderer = m_CacheduniqueRendererTypes[i];
            var transforms = m_InstanceRendererGroup.GetComponentDataArray<TransformMatrix>(forEachFilter, i);
            var float_values = m_InstanceRendererGroup.GetComponentDataArray<StartTime>(forEachFilter, i);
            if (renderer.material == null)
                continue;
            renderer.material.SetFloat(m_CurrentTimeID, current_time);
            if (renderer.needDT) {
                renderer.material.SetFloat(m_DTID, dt);
            }
            if (renderer.needPrevMatrix) {
                Assert.IsNotNull(renderer.camera);
                var matrix = m_PrevInvMatrix * renderer.camera.cameraToWorldMatrix; // prev-view * inverted-cur-view
                renderer.material.SetMatrix(m_PrevInvMatrixID, matrix);
                m_PrevInvMatrix = renderer.camera.worldToCameraMatrix;
            }
            int beginIndex = 0;
            while (beginIndex < transforms.Length) {
                int length = math.min(m_MatricesArray.Length, transforms.Length - beginIndex);
                CopyMatrices(transforms, beginIndex, length, m_MatricesArray);
                CopyFloats(float_values, beginIndex, length, m_StartTimeArray);
                renderer.material.SetFloatArray(m_StartTimesID, m_StartTimeArray);
                Graphics.DrawMeshInstanced(renderer.mesh,
                                           renderer.subMesh,
                                           renderer.material,
                                           m_MatricesArray,
                                           length,
                                           null /* MaterialPropertyBlock */,
                                           renderer.castShadows,
                                           renderer.receiveShadows,
                                           renderer.layer,
                                           renderer.camera,
                                           LightProbeUsage.Off,
                                           null /* lightProbeProxyVolume */);
                beginIndex += length;
            }
        }

	    m_CacheduniqueRendererTypes.Clear();
	    forEachFilter.Dispose();
	}
}

} // namespace UTJ {
