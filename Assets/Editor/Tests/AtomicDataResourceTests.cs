using NUnit.Framework;
using Unity.Jobs;
// using UnityEngine;
// using UnityEditor;
// using UnityEngine.TestTools;
// using System.Collections;
// using System;
using Unity.Collections;
// using UnityEngine.Assertions;
using Assert = NUnit.Framework.Assert;
// using UnityEngine;
using UTJ;

public class AtomicDataResourceTests {

	[Test]
	public void AtomicDataResource_Uninitialized_Exception()
	{
        var afloat = new AtomicFloat();
		Assert.Throws<System.NullReferenceException>(()=> { afloat.get(); });
		Assert.Throws<System.NullReferenceException>(()=> { afloat.set(1f); });
		Assert.Throws<System.NullReferenceException>(()=> { afloat.add(1f); });
		Assert.Throws<System.NullReferenceException>(()=> { afloat.Dispose(); });
	}

	[Test]
	public void AtomicDataResource_Allocate_Deallocate()
	{
        const int NUM = 128;
		var c = new AtomicFloatResource(NUM, Allocator.Persistent);
        var list = new AtomicFloat[NUM];

        for (var i = 0; i < NUM; ++i) {
            list[i] = c.Create(0f);
        }
        Assert.AreEqual(NUM, c.getCountInUse());
        for (var i = 0; i < NUM; ++i) {
            c.Destroy(ref list[i]);
        }
        Assert.AreEqual(0, c.getCountInUse());

		c.Dispose();
	}

    struct Job0 : IJobParallelFor
    {
        public AtomicFloatResource resource;
        public NativeArray<AtomicFloat> holder;
        public void Execute(int i)
        {
            holder[i] = resource.Create(0f);
        }
    }

    struct Job1 : IJobParallelFor
    {
        public AtomicFloatResource resource;
        public NativeArray<AtomicFloat> holder;
        public void Execute(int i)
        {
            var data = holder[i];
            resource.Destroy(ref data);
        }
    }

	[Test]
    public void AtomicDataResource_Allocate_Deallocate_InJob()
    {
        const int MAX = 5000;
        var resource = new AtomicFloatResource(MAX, Allocator.Persistent);
        var holder = new NativeArray<AtomicFloat>(MAX, Allocator.Persistent);
        var job0 = new Job0() {
            resource = resource,
            holder = holder,
        };
        job0.Schedule(MAX, 32).Complete();
        Assert.AreEqual(MAX, resource.getCountInUse());

        var job1 = new Job1() {
            resource = resource,
            holder = holder,
        };
        job1.Schedule(MAX, 32).Complete();
        Assert.AreEqual(0, resource.getCountInUse());

        holder.Dispose();
        resource.Dispose();
    }


    struct Job2 : IJobParallelFor
    {
        public AtomicFloat afloat;
        public void Execute(int i)
        {
            afloat.add(1f);
        }
    }

	[Test]
    public void AtomicDataResource_Atomic_Add()
    {
        const int MAX = 5000;
        var resource = new AtomicFloatResource(MAX, Allocator.Persistent);
        var afloat = resource.Create(0f);
        var job2 = new Job2() {
            afloat = afloat,
        };
        job2.Schedule(MAX, 32).Complete();
        Assert.AreEqual((float)afloat, (float)MAX);
        resource.Dispose();
    }

    struct Job3 : IJobParallelFor
    {
        public NativeArray<AtomicFloat> holder;
        public void Execute(int i)
        {
            var data = holder[i];
            data.Dispose();
        }
    }

	[Test]
    public void AtomicDataResource_Atomic_Self_Dispose()
    {
        const int MAX = 5000;
        var resource = new AtomicFloatResource(MAX, Allocator.Persistent);
        var holder = new NativeArray<AtomicFloat>(MAX, Allocator.Persistent);
        var job0 = new Job0() {
            resource = resource,
            holder = holder,
        };
        job0.Schedule(MAX, 32).Complete();
        Assert.AreEqual(MAX, resource.getCountInUse());
        
        for (var i = 0; i < MAX; ++i) {
            var afloat = holder[i];
            Assert.IsTrue(afloat.Exists());
        }
        
        var job3 = new Job3() {
            holder = holder,
        };
        job3.Schedule(MAX, 32).Complete();
        Assert.AreEqual(0, resource.getCountInUse());
        
        for (var i = 0; i < MAX; ++i) {
            var afloat = holder[i];
            Assert.IsFalse(afloat.Exists());
        }

        holder.Dispose();
        resource.Dispose();
    }

	[Test]
    public void AtomicDataResource_Atomic_Self_Dispose_Repeating()
    {
        const int MAX = 5000;
        var resource = new AtomicFloatResource(MAX, Allocator.Persistent);
        var holder = new NativeArray<AtomicFloat>(MAX, Allocator.Persistent);
        AtomicFloat special_afloat = new AtomicFloat();
        for (var k = 0; k < 8; ++k) {
            var job0 = new Job0() {
                resource = resource,
                holder = holder,
            };
            job0.Schedule(MAX, 32).Complete();
            Assert.AreEqual(MAX, resource.getCountInUse());
            
            if (k == 0) {
                special_afloat = holder[MAX/3];
            }

            for (var i = 0; i < MAX; ++i) {
                var afloat = holder[i];
                Assert.IsTrue(afloat.Exists());
            }
            
            var job3 = new Job3() {
                holder = holder,
            };
            job3.Schedule(MAX, 32).Complete();
            Assert.AreEqual(0, resource.getCountInUse());
            
            for (var i = 0; i < MAX; ++i) {
                var afloat = holder[i];
                Assert.IsFalse(afloat.Exists());
            }
        }
        {
            var job0 = new Job0() {
                resource = resource,
                holder = holder,
            };
            job0.Schedule(MAX, 32).Complete();
            Assert.AreEqual(MAX, resource.getCountInUse());                       // all slots are in use.

            Assert.IsFalse(special_afloat.Exists()); // too old.
        }

        holder.Dispose();
        resource.Dispose();
    }
}
