using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace UTJ {

public struct PlayerControllerIndex : IComponentData
{
    public int index_;
    public PlayerControllerIndex(int index) { index_ = index; }
}

public struct PlayerController : IComponentData
{
    public float horizontal_;
    public float vertical_;
    public int fire_laser_;
    public int fire_bullet_;
    public int lockon_;
    public int roll_;

    public void clear()
    {
        horizontal_ = 0f;
        vertical_ = 0f;
        fire_laser_ = 0;
        fire_bullet_ = 0;
        lockon_ = 0;
        roll_ = 0;
    }
}

public class PlayerControllerSystem : ComponentSystem
{
	struct Group
	{
        [ReadOnly] public ComponentDataArray<PlayerControllerIndex> player_controller_index_list_;
		[WriteOnly] public ComponentDataArray<PlayerController> player_controller_list_;
	}
	[Inject] Group group_;

	IEnumerator enumerator_;
    PlayerController auto_pc_;

	protected override void OnCreateManager(int capacity)
	{
        base.OnCreateManager(capacity);
		enumerator_ = act();
	}

	protected override void OnDestroyManager()
	{
        base.OnDestroyManager();
    }

	private IEnumerator act()
	{
        var random = RandomMaker.create();
		for (;;) {
			float time = random.range(0.5f, 1f);
			float dir = (float)((int)random.range(0f, 8f)) * Mathf.PI * 0.25f;
            int prev_fire = 0;
			for (var i = new WaitForSeconds(time, Time.GetCurrent()); !i.end(Time.GetCurrent());) {
                auto_pc_.fire_laser_ = 0;
                auto_pc_.fire_bullet_ = 0;
                auto_pc_.lockon_ = 0;
                auto_pc_.horizontal_ = math.cos(dir) * 128f;
                auto_pc_.vertical_ = math.sin(dir) * 128f;
                if (random.probabilityForSecond(1f, Time.GetDT())) { // release
					auto_pc_.fire_laser_ = 1;
                    prev_fire = 0;
                } else {        // press
                    auto_pc_.lockon_ = 1;
                    if (prev_fire == 0) {
                        auto_pc_.fire_bullet_ = 1;
                        prev_fire = 1;
                    }
                }
                if (random.probabilityForSecond(0.6f, Time.GetDT())) {
                    auto_pc_.roll_ = random.range(-1, 2);
                }
                yield return null;
            }
		}
    }

    private PlayerController get_latest(int player_controller_index)
    {
        var pc = new PlayerController();
        pc.horizontal_ = Input.GetAxisRaw("Horizontal") * 128f;
        pc.vertical_ = Input.GetAxisRaw("Vertical") * 128f;
        pc.fire_laser_ = Input.GetButtonUp("Fire1") ? 1 : 0;
        pc.fire_bullet_ = Input.GetButtonDown("Fire1") ? 1 : 0;
        pc.lockon_ = Input.GetButton("Fire1") ? 1 : 0;
        pc.roll_ = (int)(Input.GetAxisRaw("rotate"));
        return pc;
    }

    protected override void OnUpdate()
    {
        for (var i = 0; i < group_.player_controller_list_.Length; ++i) {
            var pci = group_.player_controller_index_list_[i].index_;
            // var pc = get_latest(pci);
            // pc.lockon_ = 0;
            var pc = auto_pc_;
            group_.player_controller_list_[i] = pc;
        }
        if (Input.GetMouseButtonDown(0)) {
            Time.togglePause();
        }
		enumerator_.MoveNext();
    }
}

} // namespace UTJ {
