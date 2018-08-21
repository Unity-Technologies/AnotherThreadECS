using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
// using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Assertions;

namespace UTJ {

public struct SpriteData : IComponentData {
    // Since this is treat as TransfromMatrix, the size must be the same as float4x4!
    /*
     * Model Matrix (4x4)
     *        0,    1,    2,    3
     * 0:  type,(N/A),(N/A),pos.x
     * 1: (N/A),(N/A),(N/A),pos.y
     * 2: (N/A),(N/A),(N/A),pos.z
     * 3: col.r,col.g,col.b,col.a
     */
    private float4x4 value_;

    public void setType(SpriteManager.Type type) { value_.c0.x = (float)type; }
    public void setPosition(ref float3 pos) { value_.c3.x = pos.x; value_.c3.y = pos.y; value_.c3.z = pos.z; }
    public void setColor(ref float4 col) { value_.c0.w = col.x; value_.c1.w = col.y; value_.c2.w = col.z; value_.c3.w = col.w; }
    public void setColor(float r, float g, float b, float a) { value_.c0.w = r; value_.c1.w = g; value_.c2.w = b; value_.c3.w = a; }
}

public struct SpriteInstanceRenderer : ISharedComponentData {
    public Mesh mesh;
    public Material material;
    public MaterialPropertyBlock material_property_block;
}

public unsafe static class SpriteRenderer
{
    const int BULK_NUM = 1023;
    static Matrix4x4[] managed_matrix_list_ = new Matrix4x4[BULK_NUM];
    static NativeBucket<SpriteData> native_sprite_data_list_;
    static JobHandle last_nativebucket_job_;
    
    static public void initialize()
    {
        native_sprite_data_list_ = new NativeBucket<SpriteData>(BULK_NUM, Allocator.Persistent);
    }

    static public void finalize()
    {
        native_sprite_data_list_.Dispose();
    }

    static public NativeBucket<SpriteData>.Concurrent GetSpriteDataBucket() { return native_sprite_data_list_; }
    
    static public void CompleteLastJob() { last_nativebucket_job_.Complete(); }
    static public void SetJob(JobHandle handle) { last_nativebucket_job_ = handle; }

    static unsafe int CopyMatrices()
    {
        int length = native_sprite_data_list_.Length;
        if (length == 0)
            return 0;
        fixed (Matrix4x4* ptr = managed_matrix_list_)
        {
            Assert.IsTrue(native_sprite_data_list_.Length <= BULK_NUM);
            Assert.AreEqual(sizeof(Matrix4x4), sizeof(SpriteData));
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<SpriteData>(ptr, length, Allocator.Persistent);
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, AtomicSafetyHandle.GetTempUnsafePtrSliceHandle());
            #endif
            native_sprite_data_list_.CopyTo(array);
        }
        return length;
    }

    static public void draw(UnityEngine.Camera camera, Mesh mesh, Material material)
    {
        int length = CopyMatrices();
        if (length == 0)
            return;
        Graphics.DrawMeshInstanced(mesh,
                                   0 /* submesh */,
                                   material,
                                   managed_matrix_list_,
                                   length,
                                   null /* MaterialPropertyBlock */,
                                   UnityEngine.Rendering.ShadowCastingMode.Off,
                                   false /* renderer.receiveShadows */,
                                   9 /* layer */, // ugly 
                                   camera,
                                   UnityEngine.Rendering.LightProbeUsage.Off);
        native_sprite_data_list_.Clear();
    }
}

public class SpriteManager : MonoBehaviour
{
    public enum Type {
        target,
        square,
        cursor,
        Max,
    }

    public static SpriteManager instance_;
    public static SpriteManager Instance { get { if (instance_ == null) { instance_ = GameObject.Find("sprite_manager").GetComponent<SpriteManager>(); } return instance_; } }

    public const int VERTEX_NUM = 4;
    [SerializeField] UnityEngine.U2D.SpriteAtlas sprite_atlas_;
    [SerializeField] Material material_;
    [SerializeField] UnityEngine.Camera final_camera_;
    Material internal_material_;
    Mesh mesh_;
    Sprite[] sprites_;

    Vector4[] sprite_shared_data_;

    void init_sprite(UnityEngine.U2D.SpriteAtlas sprite_atlas,
                     Vector4[] sprite_shared_data,
                     SpriteManager.Type type,
                     string name)
    {
        var sprite = sprite_atlas.GetSprite(name);
        sprite_shared_data[(int)type*2+0] = new Vector4(sprite.uv[0].x,
                                                        sprite.uv[0].y,
                                                        sprite.uv[3].x,
                                                        sprite.uv[3].y);
        sprite_shared_data[(int)type*2+1] = new Vector4(sprite.vertices[3].x - sprite.vertices[0].x,
                                                        sprite.vertices[3].y - sprite.vertices[0].y,
                                                        0f,
                                                        0f);
        
    }
                     

	void initialize()
    {
        Assert.IsNotNull(sprite_atlas_);
        int cnt = sprite_atlas_.spriteCount;
        Assert.IsTrue(cnt > 0);
        Assert.AreEqual(cnt, (int)Type.Max);
        sprites_ = new Sprite[cnt];
        sprite_atlas_.GetSprites(sprites_);
        sprite_shared_data_ = new Vector4[(int)Type.Max*2];
        init_sprite(sprite_atlas_, sprite_shared_data_, Type.square, "square");
        init_sprite(sprite_atlas_, sprite_shared_data_, Type.cursor, "cursor");
        init_sprite(sprite_atlas_, sprite_shared_data_, Type.target, "target");
        
        mesh_ = new Mesh();
        Assert.AreEqual(sprites_[0].vertices.Length, VERTEX_NUM);
        mesh_.vertices = new Vector3[VERTEX_NUM] {
            new Vector3(-1f, 1f, 0f),
            new Vector3( 1f, 1f, 0f),
            new Vector3(-1f,-1f, 0f),
            new Vector3( 1f,-1f, 0f),
        };
        mesh_.triangles = new int[6] {
            0, 1, 2, 2, 1, 3,
        };
		mesh_.bounds = new Bounds(Vector3.zero, Vector3.one * 99999999);

        internal_material_ = new Material(material_);
        internal_material_.SetTexture("_MainTex", sprites_[0].texture);
        internal_material_.enableInstancing = true;
        internal_material_.SetVectorArray("_uvscale_table", sprite_shared_data_);
	}
	
    void finalize()
    {
    }

    public UnityEngine.Camera getFinalCamera() { return final_camera_; }
    public Mesh getMesh() { return mesh_; }
    public Material getMaterial() { return internal_material_; }

    public SpriteInstanceRenderer getRenderer()
    {
		var sprite_renderer = new SpriteInstanceRenderer {
			mesh = mesh_,
			material = internal_material_,
		};
        return sprite_renderer;
    }

    void OnEnable()
    {
        initialize();
    }

    void OnDisable()
    {
        finalize();
    }
}

public class SpriteRendererSystem : ComponentSystem
{
    struct SpriteRendererIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(SpriteRendererIgniter));
		entity_manager.CreateEntity(arche_type);
    }

#pragma warning disable 0169
    struct Group {
        [ReadOnly] ComponentDataArray<SpriteRendererIgniter> dummy_;
    }
    [Inject] Group group_;
#pragma warning restore 0169

    static List<JobHandle> need_to_be_syncronized_handle_list_ = new List<JobHandle>();
    public static void DependOn(JobHandle handle) { need_to_be_syncronized_handle_list_.Add(handle); }

    protected override void OnCreateManager(int capacity)
    {
        base.OnCreateManager(capacity);
        SpriteRenderer.initialize();
    }
    
    protected override void OnDestroyManager()
    {
        SpriteRenderer.finalize();
        base.OnDestroyManager();
    }    

    protected override void OnUpdate()
    {
        foreach (var dep in need_to_be_syncronized_handle_list_) {
            dep.Complete();
        }
        need_to_be_syncronized_handle_list_.Clear();

        var final_camera = SpriteManager.Instance.getFinalCamera();
        var mesh = SpriteManager.Instance.getMesh();
        var material = SpriteManager.Instance.getMaterial();
        SpriteRenderer.draw(final_camera, mesh, material);
    }
}

} // namespace UTJ {
