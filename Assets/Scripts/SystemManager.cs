using UnityEngine;
using Unity.Entities;
// using Unity.Mathematics;
using Unity.Collections;
using UnityEngine.Assertions;

namespace UTJ {

public class SystemManagerSystem : ComponentSystem
{
    static AtomicFloatResource float_resource_;
    RandomLocal random_;
    float dfps = 60f;
	public System.Diagnostics.Stopwatch stopwatch_;

	[RuntimeInitializeOnLoadMethod()]
	static void Init()
	{
        /*
         * work around of TOO MUCH stripping.
         */
        new Unity.Rendering.MeshInstanceRendererSystem();
        new Unity.Transforms.EndFrameTransformSystem();
        new Unity.Rendering.MeshRenderBoundsUpdateSystem();
        new Unity.Rendering.RenderingSystemBootstrap();
        /**/
        // TypeManager.GetTypeIndex(typeof(Locked));
        RandomMaker.initialize(12345L);
    }

    struct SystemManagerIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
        entity_manager.EntityCapacity = 4096;
		var arche_type = entity_manager.CreateArchetype(typeof(SystemManagerIgniter));
		entity_manager.CreateEntity(arche_type);
    }
    
#pragma warning disable 0169
    struct Group { ComponentDataArray<SystemManagerIgniter> dummy_; }
    [Inject] Group group_;
#pragma warning restore 0169

    static bool display_info_ = true;
    public static bool DisplayInfo { set { display_info_ = value; } }

    public static AtomicFloatResource FloatResource { get { Assert.IsTrue(float_resource_.IsCreated); return float_resource_; } }

	protected override void OnCreateManager(int capacity)
	{
        base.OnCreateManager(capacity);
        Assert.IsFalse(float_resource_.IsCreated);
        float_resource_ = new AtomicFloatResource(1024, Allocator.Persistent);
        random_ = RandomMaker.create();
		stopwatch_ = new System.Diagnostics.Stopwatch();
		stopwatch_.Start();
	}

	protected override void OnDestroyManager()
	{
        Assert.IsTrue(float_resource_.IsCreated);
        float_resource_.Dispose();
        base.OnDestroyManager();
    }

	protected override void OnUpdate()
	{
        if (display_info_) {
            var elapsed = stopwatch_.Elapsed;
            stopwatch_.Restart();
            {
                // var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
                // var entities = entity_manager.GetAllEntities();
                // int num = entities.Length;
                // entities.Dispose();
                float dt = (float)elapsed.TotalMilliseconds*0.001f;
                float fps = 1f/dt;
                dfps = Mathf.Lerp(dfps, fps, 0.1f);
                // var str = string.Format("{0,4} : {1,3:#.#} fps", num, dfps);
                var str = string.Format("{0,3:#.#} fps", dfps);
                SystemManager.Instance.setDebugText(str);
            }
        }
	}
}

public class SystemManager : MonoBehaviour {

    public static SystemManager instance_;
    public static SystemManager Instance { get { if (instance_ == null) { instance_ = GameObject.Find("system_manager").GetComponent<SystemManager>(); } return instance_; } }

    [SerializeField] UnityEngine.Camera main_camera_;
    [SerializeField] UnityEngine.UI.Text text_;
    Entity primary_player_entity_;

    public void setDebugText(string str)
    {
        text_.text = str;
    }

    public Entity getPrimaryPlayer() { return primary_player_entity_; }
    public UnityEngine.Camera getMainCamera() { return main_camera_; }

    void OnEnable()
    {
        Color.Initialize();
        SystemManagerSystem.Ignite();
        TimeSystem.Ignite();
        SpriteRendererSystem.Ignite();
        BulletSpawnSystem.Ignite();
        SparkSpawnSystem.Ignite();
        ExplosionSpawnSystem.Ignite();
        SightSpawnSystem.Ignite();
        GameManagerSystem.Ignite();
        LockTargetManager.Init();
    }
    
    void Start()
    {
        {
            var pos = new Vector3(0f, 0f, 0f);
            var rot = Quaternion.identity;
            var float_resource = SystemManagerSystem.FloatResource;
            primary_player_entity_ = ECSPlayerManager.Instance.spawn(ref pos, ref rot, ref float_resource);
        }
        {
            var game_camera = main_camera_.GetComponent<GameCameraComponent>();
            game_camera.setup(primary_player_entity_);
        }
    }
}

} // namespace UTJ {
