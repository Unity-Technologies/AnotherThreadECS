using System.Collections.Generic;
// using System.Threading;
// using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using Unity.Jobs;
using Unity.Burst;

namespace UTJ {

public struct TrailConfig {
    public const int BULK_NUM = 256;
    public const int NODE_NUM = 64;
    public const int TOTAL_NUM = BULK_NUM * NODE_NUM;
}

public struct TrailData : IComponentData {
    public int color_type_;
    public int trail_head_index_;
    public float last_update_index_time_;
}

[InternalBufferCapacity(TrailConfig.NODE_NUM)]
public struct TrailPoint : IBufferElementData
{
    public float3 position_;
    public float3 normal_;
}

public struct TrailRenderer : ISharedComponentData {
    public Material material_;
}

[UpdateAfter(typeof(TrailRendererSystem))]
public class TrailSystem : JobComponentSystem
{
	struct Group
	{
        public readonly int Length;
        [ReadOnly] public EntityArray entity_list_;
		[ReadOnly] public ComponentDataArray<LocalToWorld> local_to_world_list_;
        public ComponentDataArray<TrailData> trail_data_list_;
        public BufferArray<TrailPoint> trail_points_list_;
	}
	[Inject] Group group_;

    static List<Vector3> managed_vertices_list_;
    static void ClearVertices() { managed_vertices_list_.Clear(); }
    static void AddVertex(ref Vector3 v) { managed_vertices_list_.Add(v); }
    public static List<Vector3> GetVertices() { return managed_vertices_list_; }
    static List<Vector3> managed_normals_list_;
    static void ClearNormals() { managed_normals_list_.Clear(); }
    static void AddNormal(ref Vector3 v) { managed_normals_list_.Add(v); }
    public static List<Vector3> GetNormals() { return managed_normals_list_; }
    static List<Vector2> managed_uvs_list_;
    static void ClearUVs() { managed_uvs_list_.Clear(); }
    static void AddUV(ref Vector2 v) { managed_uvs_list_.Add(v); }
    public static List<Vector2> GetUVs() { return managed_uvs_list_; }
    static List<int> managed_triangles_list_;
    static void ClearTriangles() { managed_triangles_list_.Clear(); }
    static void AddTriangle(int v0, int v1, int v2) { managed_triangles_list_.Add(v0); managed_triangles_list_.Add(v1); managed_triangles_list_.Add(v2); }
    public static List<int> GetTriangles() { return managed_triangles_list_; }

    static JobHandle handle_;
    public static JobHandle Fence { get { return handle_; } }
    public static void Sync() { handle_.Complete(); }

    protected override void OnCreateManager()
    {
        managed_vertices_list_ = new List<Vector3>(TrailConfig.TOTAL_NUM*2);
        managed_normals_list_ = new List<Vector3>(TrailConfig.TOTAL_NUM*2);
        managed_uvs_list_ = new List<Vector2>(TrailConfig.TOTAL_NUM*2);
        managed_triangles_list_ = new List<int>(TrailConfig.TOTAL_NUM*2*3*2);
    }
    
    protected override void OnDestroyManager()
    {
        managed_triangles_list_ = null;
        managed_uvs_list_ = null;
        managed_normals_list_ = null;
        managed_vertices_list_ = null;
    }

    [BurstCompile]
	struct Job : IJobParallelFor
	{
        public float current_time_;
        public float flow_z_;
        [ReadOnly] public ComponentDataArray<LocalToWorld> local_to_world_list_;
        public ComponentDataArray<TrailData> trail_data_list_;
        public BufferArray<TrailPoint> trail_points_list_;

		public void Execute(int i)
		{
            var pos = local_to_world_list_[i].Value.c3.xyz;
            var td = trail_data_list_[i];
            var trail_points = trail_points_list_[i];
            
            // flow
            for (var j = 0; j < trail_points.Length; ++j) {
                var tp = trail_points[j];
                tp.position_.z += flow_z_;
                trail_points[j] = tp;
            }

            var index = td.trail_head_index_;
            if (current_time_ - td.last_update_index_time_ > 1f/60f) {
                ++index;
                if (index >= TrailConfig.NODE_NUM) {
                    index = 0;
                }
                td.trail_head_index_ = index;
                td.last_update_index_time_ = current_time_;
                trail_data_list_[i] = td;
            }

            var prev_idx = td.trail_head_index_ - 1;
            if (prev_idx < 0) {
                prev_idx = TrailConfig.NODE_NUM-1;
            }
            var prev_pos = trail_points[prev_idx].position_;
            var normal = math.normalize(pos - prev_pos);
            trail_points[index] = new TrailPoint { position_ = pos, normal_ = normal, };
        }        
    }

#if false
    struct PackJob : IJob
    {
        [ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
        [ReadOnly] public BufferArray<TrailPoint> trail_points_list_;

        public void Execute()
        {
            TrailSystem.ClearVertices();
            TrailSystem.ClearNormals();
            TrailSystem.ClearUVs();
            TrailSystem.ClearTriangles();
            for (var i = 0; i < trail_data_list_.Length; ++i) {
                var td = trail_data_list_[i];
                var trail_points = trail_points_list_[i];
                for (var j = 0; j < TrailConfig.NODE_NUM; ++j) {
                    int idx = td.trail_head_index_ - j;
                    if (idx < 0) {
                        idx += TrailConfig.NODE_NUM;
                    }
                    Vector3 v = trail_points[idx].position_;
                    Vector3 n = trail_points[idx].normal_;
                    
                    TrailSystem.AddVertex(ref v);
                    TrailSystem.AddVertex(ref v); // two by two
                    TrailSystem.AddNormal(ref n);
                    TrailSystem.AddNormal(ref n);
                    float alpha = (((float)j)/(float)TrailConfig.NODE_NUM);
                    alpha = (1f - alpha * alpha) * 0.8f;
                    var uv = new Vector2(td.color_type_, alpha);
                    TrailSystem.AddUV(ref uv);
                    TrailSystem.AddUV(ref uv);
                    if (j < TrailConfig.NODE_NUM-1) {
                        TrailSystem.AddTriangle(i*TrailConfig.NODE_NUM*2 + (j+0)*2+0,
                                                i*TrailConfig.NODE_NUM*2 + (j+0)*2+1,
                                                i*TrailConfig.NODE_NUM*2 + (j+1)*2+0);
                        TrailSystem.AddTriangle(i*TrailConfig.NODE_NUM*2 + (j+1)*2+0,
                                                i*TrailConfig.NODE_NUM*2 + (j+0)*2+1,
                                                i*TrailConfig.NODE_NUM*2 + (j+1)*2+1);
                    }
                }
            }
        }
    }
#endif

    struct PackVerticesJob : IJob
    {
        [ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
        [ReadOnly] public BufferArray<TrailPoint> trail_points_list_;
        public void Execute()
        {
            TrailSystem.ClearVertices();
            for (var i = 0; i < trail_data_list_.Length; ++i) {
                var td = trail_data_list_[i];
                var trail_points = trail_points_list_[i];
                for (var j = 0; j < TrailConfig.NODE_NUM; ++j) {
                    int idx = td.trail_head_index_ - j;
                    if (idx < 0) {
                        idx += TrailConfig.NODE_NUM;
                    }
                    Vector3 v = trail_points[idx].position_;
                    TrailSystem.AddVertex(ref v);
                    TrailSystem.AddVertex(ref v); // two by two
                }
            }
        }
    }

    struct PackNormalsJob : IJob
    {
        [ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
        [ReadOnly] public BufferArray<TrailPoint> trail_points_list_;
        public void Execute()
        {
            TrailSystem.ClearNormals();
            for (var i = 0; i < trail_data_list_.Length; ++i) {
                var td = trail_data_list_[i];
                var trail_points = trail_points_list_[i];
                for (var j = 0; j < TrailConfig.NODE_NUM; ++j) {
                    int idx = td.trail_head_index_ - j;
                    if (idx < 0) {
                        idx += TrailConfig.NODE_NUM;
                    }
                    Vector3 n = trail_points[idx].normal_;
                    TrailSystem.AddNormal(ref n);
                    TrailSystem.AddNormal(ref n);
                }
            }
        }
    }

    struct PackUVsJob : IJob
    {
        [ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
        public void Execute()
        {
            TrailSystem.ClearUVs();
            for (var i = 0; i < trail_data_list_.Length; ++i) {
                var td = trail_data_list_[i];
                for (var j = 0; j < TrailConfig.NODE_NUM; ++j) {
                    float alpha = (((float)j)/(float)TrailConfig.NODE_NUM);
                    alpha = (1f - alpha * alpha) * 0.8f;
                    var uv = new Vector2(td.color_type_, alpha);
                    TrailSystem.AddUV(ref uv);
                    TrailSystem.AddUV(ref uv);
                }
            }
        }
    }

    struct PackTrianglesJob : IJob
    {
        public int length;
        public void Execute()
        {
            TrailSystem.ClearTriangles();
            for (var i = 0; i < length; ++i) {
                for (var j = 0; j < TrailConfig.NODE_NUM; ++j) {
                    if (j < TrailConfig.NODE_NUM-1) {
                        TrailSystem.AddTriangle(i*TrailConfig.NODE_NUM*2 + (j+0)*2+0,
                                                i*TrailConfig.NODE_NUM*2 + (j+0)*2+1,
                                                i*TrailConfig.NODE_NUM*2 + (j+1)*2+0);
                        TrailSystem.AddTriangle(i*TrailConfig.NODE_NUM*2 + (j+1)*2+0,
                                                i*TrailConfig.NODE_NUM*2 + (j+0)*2+1,
                                                i*TrailConfig.NODE_NUM*2 + (j+1)*2+1);
                    }
                }
            }
        }
    }

	protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        handle_ = inputDeps;

        if (Time.GetDT() > 0f) {
            var job = new Job() {
                current_time_ = Time.GetCurrent(),
                flow_z_ = CV.FLOW_VELOCITY * Time.GetDT(),
                local_to_world_list_ = group_.local_to_world_list_,
                trail_data_list_ = group_.trail_data_list_,
                trail_points_list_ = group_.trail_points_list_,
            };
            handle_ = job.Schedule(group_.Length, 8, handle_);
        }

        {
            var handle_s = new NativeArray<JobHandle>(8, Allocator.Temp);
            var v_job = new PackVerticesJob {
                trail_data_list_ = group_.trail_data_list_,
                trail_points_list_ = group_.trail_points_list_,
            };
            var n_job = new PackNormalsJob {
                trail_data_list_ = group_.trail_data_list_,
                trail_points_list_ = group_.trail_points_list_,
            };
            var u_job = new PackUVsJob {
                trail_data_list_ = group_.trail_data_list_,
            };
            var t_job = new PackTrianglesJob {
                length = group_.Length,
            };
#if false
            var idx = 0;
            handle_s[idx] = v_job.Schedule(handle_); ++idx;
            handle_s[idx] = n_job.Schedule(handle_); ++idx;
            handle_s[idx] = u_job.Schedule(handle_); ++idx;
            handle_s[idx] = t_job.Schedule(handle_); ++idx;
            handle_ = JobHandle.CombineDependencies(handle_s);
            handle_s.Dispose();
#else
            handle_.Complete();
            v_job.Execute();
            n_job.Execute();
            u_job.Execute();
            t_job.Execute();
            handle_s.Dispose();
#endif
        }

        return handle_;
    }
}

[UpdateAfter(typeof(MeshInstanceRendererSystem))]
public class TrailRendererSystem : ComponentSystem
{
    Mesh mesh_;

	struct Group
	{
        public readonly int Length;
        [ReadOnly] public EntityArray entity_list_;
		[ReadOnly] public ComponentDataArray<TrailData> trail_data_list_;
        [ReadOnly] public BufferArray<TrailPoint> trail_points_list_;
	}
	[Inject] Group group_;

    protected override void OnCreateManager()
    {
        mesh_ = new Mesh();
		mesh_.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999); // invalidate bounds.
    }

    protected override void OnDestroyManager()
    {
        mesh_ = null;
    }

	protected override void OnUpdate()
    {
        if (group_.Length == 0) {
            return;
        }
        TrailSystem.Sync();

        var entity_manager = Unity.Entities.World.Active.GetOrCreateManager<EntityManager>();
        var trail_renderer = entity_manager.GetSharedComponentData<TrailRenderer>(group_.entity_list_[0]);

        Assert.IsTrue(group_.Length <= TrailConfig.BULK_NUM);
		mesh_.triangles = CV.IntArrayEmpty;
		mesh_.normals = CV.Vector3ArrayEmpty;
		mesh_.uv = CV.Vector2ArrayEmpty;
		mesh_.vertices = CV.Vector3ArrayEmpty;
        mesh_.SetVertices(TrailSystem.GetVertices());
        mesh_.SetNormals(TrailSystem.GetNormals());
        mesh_.SetUVs(0 /* channel */, TrailSystem.GetUVs());
        
        mesh_.SetTriangles(TrailSystem.GetTriangles(), 0 /* submesh */, false /* calculateBounds */, 0 /* baseVertex */);

        var material = trail_renderer.material_;
        Graphics.DrawMesh(mesh_,
                          Matrix4x4.identity,
                          material,
                          0 /* layer */,
                          null /* camera */,
                          0 /* submeshIndex */,
                          null /* MaterialPropertyBlock */,
                          false /* castShadows */,
                          false /* receiveShadows */,
                          false /* lightprobe */);
    }
}

public class TrailManager : MonoBehaviour
{
    public static TrailManager instance_;
    public static TrailManager Instance { get { if (instance_ == null) { instance_ = GameObject.Find("trail_manager").GetComponent<TrailManager>(); } return instance_; } }

    public enum ColorType {
        Black,
        Red,
        Blue,
        Magenta,
        Green,
        Yellow,
        Cyan,
        White,
        Max,
    }

    [SerializeField] Material material_;
    Vector4[] color_shared_data_;

    void initialize()
    {
        color_shared_data_ = new Vector4[(int)ColorType.Max];
        color_shared_data_[(int)ColorType.Black] = new Vector4(0f, 0f, 0f, 1f);
        color_shared_data_[(int)ColorType.Red] = new Vector4(1f, 0.2f, 0.2f, 1f);
        color_shared_data_[(int)ColorType.Blue] = new Vector4(0.2f, 0.2f, 1f, 1f);
        color_shared_data_[(int)ColorType.Magenta] = new Vector4(1f, 0.2f, 1f, 1f);
        color_shared_data_[(int)ColorType.Green] = new Vector4(0.2f, 1f, 0.2f, 1f);
        color_shared_data_[(int)ColorType.Yellow] = new Vector4(1f, 1f, 0.2f, 1f);
        color_shared_data_[(int)ColorType.Cyan] = new Vector4(0.2f, 1f, 1f, 1f);
        color_shared_data_[(int)ColorType.White] = new Vector4(1f, 1f, 1f, 1f);
        material_.SetVectorArray("_color_table", color_shared_data_);
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

    public Material getMaterial()
    {
        return material_;
    }
}

} // namespace UTJ {
