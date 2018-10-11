// using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;

namespace UTJ {

public struct RigidbodyPosition : IComponentData
{
	public float3 velocity_;
	public float3 acceleration_;
	public float damper_;
    public float limit_xy_tube_radius_;
    public int hit_tube_;

    public RigidbodyPosition(float damper = 0f) {
        velocity_ = new float3(0, 0, 0);
        acceleration_ = new float3(0, 0, 0);
        damper_ = damper;
        limit_xy_tube_radius_ = 0f;
        hit_tube_ = 0;
    }
	public void addForceX(float v) { acceleration_.x += v; }
	public void addForceY(float v) { acceleration_.y += v; }
	public void addForceZ(float v) { acceleration_.z += v; }
	public void addForceXY(float x, float y) { acceleration_.x += x; acceleration_.y += y; }
	public void addForceXY(ref float2 xy) { acceleration_.x += xy.x; acceleration_.y += xy.y; }
	public void addForce(float3 v) { acceleration_ += v; }
	public void resetForce() { acceleration_ = new float3(0f, 0f, 0f); }
	public void resetVelocity() { velocity_ = new float3(0f, 0f, 0f); }
	public void resetVelocity(float x, float y, float z) { velocity_ = new float3(x, y, z); }

	public void cancelUpdateForTube(float dt, ref float3 pos, float radius)
	{
		// cancel
		pos.x -= velocity_.x * dt;
		pos.y -= velocity_.y * dt;
		pos.z -= velocity_.z * dt;
		// recalculate
		float len2 = pos.x * pos.x + pos.y * pos.y;
		float len = math.sqrt(len2);
		float rlen = 1f / len;
		float dx = -pos.y * rlen;
		float dy = pos.x * rlen;
		float norm = dx * velocity_.x + dy * velocity_.y;
		velocity_.x = dx * norm;
		velocity_.y = dy * norm;
		pos.x += velocity_.x * dt;
		pos.y += velocity_.y * dt;
		float ratio = rlen * radius;
		pos.x *= ratio;
		pos.y *= ratio;
	}
}


public struct RigidbodyRotation : IComponentData
{
	public float3 omega_;
	public float3 angular_acceleration_;
	public float damper_;

    public RigidbodyRotation(float damper = 0f) { omega_ = new float3(0, 0, 0); angular_acceleration_ = new float3(0, 0, 0); damper_ = damper; }
	public void addTorqueX(float v) { angular_acceleration_.x += v; }
	public void addTorqueY(float v) { angular_acceleration_.y += v; }
	public void addTorqueZ(float v) { angular_acceleration_.z += v; }
	public void addTorque(ref float3 v) { angular_acceleration_ += v; }
    public void addSpringTorque(ref float3 target,
                                ref Position position,
                                ref Rotation rotation,
                                float level)
    {
        var diff = target - position.Value;
        var up = new float3(0f, 1f, 0f);
        var q = quaternion.LookRotation(diff, up);
        addSpringTorque(ref q, ref rotation, level);
    }
    public void addSpringTorque(ref quaternion target,
                                ref Rotation rotation,
                                float level)
    {
        /**
         * Quaternion(x, y, z, w)
         * q.x = sin(theta/2)*ax
         * q.y = sin(theta/2)*ay
         * q.z = sin(theta/2)*az
         * q.w = cos(theta/2)
         */
        // var q = target * Utility.Inverse(ref transform_.rotation_);
        var inv_rot = math.inverse(rotation.Value);
        var q = math.mul(target, inv_rot);
        if (q.value.w < 0f) {            // over 180 degree, take shorter way!
            q.value.x = -q.value.x;
            q.value.y = -q.value.y;
            q.value.z = -q.value.z;
            q.value.w = -q.value.w;
        }
        var torque = new float3(q.value.x * level, q.value.y * level, q.value.z * level);
        addTorque(ref torque);
    }
}


// [UpdateBefore(typeof(TransformSystem))]
public class RigidbodyPositionSystem : JobComponentSystem
{
    JobHandle handle_;
    public JobHandle Fence { get { return handle_; } }
    public void Sync() { handle_.Complete(); }

    [BurstCompile]
	struct RigidbodyPositionJob : IJobProcessComponentData<Position, RigidbodyPosition>
	{
        public float dt_;

		public void Execute(ref Position position, ref RigidbodyPosition rb)
		{
			rb.velocity_ += rb.acceleration_ * dt_;
			rb.velocity_ -= rb.velocity_ * (rb.damper_ * dt_);
			position.Value += rb.velocity_ * dt_;
			rb.acceleration_ = new float3(0f, 0f, 0f);

            var radius = rb.limit_xy_tube_radius_;
            if (radius > 0f && math.lengthsq(position.Value.xy) >= radius*radius) {
                rb.hit_tube_ = 1;
                rb.cancelUpdateForTube(dt_, ref position.Value, radius);
            } else {
                rb.hit_tube_ = 0;
            }
		}
	}

	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        handle_ = inputDeps;

		var job = new RigidbodyPositionJob {
            dt_ = Time.GetDT(),
		};
		handle_ = job.Schedule(this, handle_);

		return handle_;
	}
}

// [UpdateBefore(typeof(TransformSystem))]
public class RigidbodyRotationSystem : JobComponentSystem
{
	[BurstCompile]
	struct RigidbodyRotationJob : IJobProcessComponentData<Rotation, RigidbodyRotation>
	{
        public float dt_;

		public void Execute(ref Rotation rotation, ref RigidbodyRotation rb)
		{
			var rot = rotation.Value;
			rb.omega_ += rb.angular_acceleration_ * dt_;
			rb.angular_acceleration_ = new float3(0f, 0f, 0f);
			rb.omega_ -= rb.omega_ * (rb.damper_ * dt_);
			float3 n = rb.omega_ * dt_;
			var len2 = math.dot(n, n); // sin^2
			var w = math.sqrt(1f - len2); // cos
			var q = new quaternion(n.x, n.y, n.z, w);
			rot = math.mul(q, rot);
			var v2 = math.dot(rot.value, rot.value);
			if (v2 == 0) {
				rot.value.w = 1f;
			} else {
				float inv = math.rsqrt(v2);
				rot.value.x *= inv;
				rot.value.y *= inv;
				rot.value.z *= inv;
				rot.value.w *= inv;
			}
			rotation.Value = rot;
		}
	}
    
	protected override JobHandle OnUpdate(JobHandle inputDeps)
	{
        var handle = inputDeps;

		var job = new RigidbodyRotationJob {
            dt_ = Time.GetDT(),
		};
		handle = job.Schedule(this, handle);

        return handle;
	}
}

} // namespace UTJ {
