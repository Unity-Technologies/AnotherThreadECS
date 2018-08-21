using Unity.Entities;
using System.Runtime.CompilerServices;

namespace UTJ {

public static class Time
{
    static double time_ = 0f;
    const float GAME_DT = 1f/60f;
    static float dt_ = GAME_DT;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetDT() { return dt_; }
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float GetCurrent() { return (float)time_; }
    public static void UpdateFrame() { time_ += (double)dt_; }

    public static void setPause(bool flg)
    {
        if (flg) {
            dt_ = 0f;
        } else {
            dt_ = GAME_DT;
        }
    }
    public static void togglePause()
    {
        setPause(dt_ != 0f) ;
    }
}

public class TimeSystem : ComponentSystem
{
    struct TimeIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(TimeIgniter));
		entity_manager.CreateEntity(arche_type);
    }
#pragma warning disable 0169
    struct Group { ComponentDataArray<TimeIgniter> dummy_; }
    [Inject] Group group_;
#pragma warning restore 0169

	protected override void OnUpdate()
	{
        Time.UpdateFrame();
    }
}

} // namespace UTJ {
