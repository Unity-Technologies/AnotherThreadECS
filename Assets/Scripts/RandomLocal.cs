using Unity.Entities;
using Unity.Mathematics;
using System;

namespace UTJ {

public struct RandomLocal : IComponentData
{
	public struct Xorshift {
		private UInt32 x;
		private UInt32 y;
		private UInt32 z;
		private UInt32 w;
		
		public void setSeed(UInt64 seed)
		{
			x = 521288629u;
			y = (UInt32)(seed >> 32) & 0xFFFFFFFF;
			z = (UInt32)(seed & 0xFFFFFFFF);
			w = x ^ z;
		}
 
		public UInt32 getNext()
		{
			UInt32 t = x ^ (x << 11);
			x = y;
			y = z;
			z = w;
			w = (w ^ (w >> 19)) ^ (t ^ (t >> 8));
			return w;
		}
	}

	Xorshift rand_;
	
	public RandomLocal(UInt64 seed)
	{
		rand_ = new Xorshift();
		rand_.setSeed(seed);
	}

	public void setSeed(UInt64 seed)
	{
		rand_.setSeed(seed);
	}

	public UInt32 get()
	{
		return rand_.getNext();
	}

	public float range(float min, float max)
	{
		int val = (int)(get() & 0xffff);
		float range = max - min;
		return (((float)val*range) / (float)(0xffff)) + min;
	}

	public int range(int min, int max)
	{
		long val = get();
		return (int)((val%(max-min))) + min;
	}

	public bool probabilitySlow(float ratio)
	{
		float v = range(0f, 1f);
		return (v < ratio);
	}

	public bool probability(float ratio) // optimized
	{
		UInt32 v = (rand_.getNext()) & 0xffff;
		UInt32 p = (UInt32)(((float)(1<<16)) * ratio);
		return (v < p);
	}

	public bool probability(float happen_times_per_second, float dt)
	{
		float v = range(0f, 1f);
		return (v < happen_times_per_second * dt);
	}

	public bool probabilityForSecond(float times_for_a_second, float dt)
	{
		float v = range(0f, 1f);
		return (v < times_for_a_second * dt);
	}

	public float3 onSphere(float radius)
	{
		float x = range(-1f, 1f);
		float y = range(-1f, 1f);
		float z = range(-1f, 1f);
		float len2 = x*x + y*y + z*z;
		float rlen = math.rsqrt(len2);
		float v = rlen * radius;
		var point = new float3(x*v, y*v, z*v);
		return point;
	}

	public float3 onCube(float radius)
    {
        return new float3(range(-radius, radius),
                          range(-radius, radius),
                          range(-radius, radius));
    }

    public RandomLocal create()
    {
        return new RandomLocal(get());
    }
}

public static class RandomMaker {

    static RandomLocal base_random_;

    public static void initialize(UInt64 seed)
    {
        base_random_.setSeed(seed);
    }

    public static RandomLocal create()
    {
        return base_random_.create();
    }
}

} // namespace UTJ {
