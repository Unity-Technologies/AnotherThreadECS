using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;

namespace UTJ {

public struct PlayerCollisionInfo : IComponentData
{
    public int hit_;
    public float3 contact_position_;
    public static PlayerCollisionInfo Hit(ref float3 contact_position) { return new PlayerCollisionInfo { hit_ = 1, contact_position_ = contact_position, }; }
}

public struct PlayerBulletCollisionInfo : IComponentData
{
    public int hit_;
    public float3 contact_position_;
    public static PlayerBulletCollisionInfo Hit(ref float3 contact_position) { return new PlayerBulletCollisionInfo { hit_ = 1, contact_position_ = contact_position, }; }
}

public struct EnemyCollisionInfo : IComponentData
{
    public int hit_;
    public float3 contact_position_;
    public static EnemyCollisionInfo Hit(ref float3 contact_position) { return new EnemyCollisionInfo { hit_ = 1, contact_position_ = contact_position, }; }
}

public struct EnemyBulletCollisionInfo : IComponentData
{
    public int hit_;
    public float3 contact_position_;
    public static EnemyBulletCollisionInfo Hit(ref float3 contact_position) { return new EnemyBulletCollisionInfo { hit_ = 1, contact_position_ = contact_position, }; }
}

public struct SphereCollider  : IComponentData
{
	public float3 offset_position_;
	public float3 position_;
	public float radius_;
    public int updated_;        // boolean

    public bool intersect(ref SphereCollider another)
    {
        if (updated_ == 0) return false;
        var diff = another.position_ - position_;
        var dist2 = math.lengthsq(diff);
        var rad = radius_ + another.radius_;
        var rad2 = rad * rad;
        return (dist2 < rad2);
    }
}

[UpdateAfter(typeof(Unity.Rendering.MeshInstanceRendererSystem))]
public class ColliderUpdateSystem : JobComponentSystem
{
	public struct ColliderUpdateJob : IJobProcessComponentData<Position, Rotation, SphereCollider>
	{
		public void Execute([ReadOnly] ref Unity.Transforms.Position pos,
                            [ReadOnly] ref Unity.Transforms.Rotation rot,
                            ref SphereCollider sphere_collider)
		{
			sphere_collider.position_ = math.mul(rot.Value, sphere_collider.offset_position_) + pos.Value;
            sphere_collider.updated_ = 1; // true
		}
	}	

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

		var job = new ColliderUpdateJob();
        handle = job.Schedule(this, handle);

		return handle;
	}
}

} // namespace UTJ {
