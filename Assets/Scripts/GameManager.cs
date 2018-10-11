using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
// using Unity.Jobs;
// using UnityEngine;

namespace UTJ {

public class GameManagerSystem : ComponentSystem
{
    struct GameManagerIgniter : IComponentData {}
    public static void Ignite()
    {
        var entity_manager = World.Active.GetOrCreateManager<EntityManager>();
		var arche_type = entity_manager.CreateArchetype(typeof(GameManagerIgniter));
		entity_manager.CreateEntity(arche_type);
    }
#pragma warning disable 0169
    struct Group { ComponentDataArray<GameManagerIgniter> dummy_; }
    [Inject] Group group_;
#pragma warning restore 0169

	IEnumerator enumerator_;
    RandomLocal random_;

	private IEnumerator act()
	{
        yield return null;
        for (;;) {
			for (var i = new WaitForSeconds(0.25f, Time.GetCurrent()); !i.end(Time.GetCurrent());) { yield return null; }
            for (var i = 0; i < 16; ++i) {
				var pos = new float3(random_.range(-15f, 15f), random_.range(-15f, 15f), -10f);
				var rot = quaternion.identity;
                var float_resource = SystemManagerSystem.FloatResource;
				ECSZakoManager.Instance.spawn(ref pos, ref rot, ref random_, ref float_resource);
			}
			yield return null;
		}
        // for (;;) yield return null;
	}

	protected override void OnCreateManager()
	{
        base.OnCreateManager();
        random_ = RandomMaker.create();
		enumerator_ = act();
	}

	protected override void OnDestroyManager()
	{
        base.OnDestroyManager();
    }

	protected override void OnUpdate()
	{
		enumerator_.MoveNext();
	}
}

} // namespace UTJ {
