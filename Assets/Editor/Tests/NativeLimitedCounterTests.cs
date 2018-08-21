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

public class NativeLimitedCounterTests {

	[Test]
	public void NativeLimitedCounter_Allocate_Deallocate_Read_Write()
	{
		var c = new NativeLimitedCounter(Allocator.Persistent);

        int cnt = 0;
        for (var i = 0; i < 100; ++i) {
            c.TryIncrement(100 /* max */);
            ++cnt;
        }
		Assert.AreEqual(cnt, c.Count);

		c.Dispose();
	}

    struct Job0 : IJobParallelFor
    {
        public int maximum;
        public NativeLimitedCounter counter;
        public void Execute(int i)
        {
            bool success = counter.TryIncrement(maximum);
            Assert.IsTrue(success);
        }
    }
	[Test]
    public void NativeLimitedCounter_JustTheNumber()
    {
        const int MAX = 1000;
        var counter = new NativeLimitedCounter(Allocator.Persistent);
        var job = new Job0();
        job.counter = counter;
        job.maximum = MAX;
        job.Schedule(MAX, 32).Complete();

        Assert.AreEqual(MAX, counter.Count);
        counter.Dispose();
    }


    struct Job1 : IJobParallelFor
    {
        public int maximum;
        public NativeLimitedCounter counter;
        public void Execute(int i)
        {
            counter.TryIncrement(maximum);
        }
    }
	[Test]
    public void NativeLimitedCounter_OverTheNumber()
    {
        const int MAX = 1000;
        var counter = new NativeLimitedCounter(Allocator.Persistent);
        var job = new Job1();
        job.counter = counter;
        job.maximum = MAX;
        job.Schedule(MAX*2, 32).Complete();

        Assert.AreEqual(MAX, counter.Count);
        counter.Dispose();
    }

    struct Job2 : IJobParallelFor
    {
        public int maximum;
        public NativeLimitedCounter counter;
        public void Execute(int i)
        {
            if (i%2 > 0) {
                counter.TryIncrement(int.MaxValue);
            } else {
                counter.TryDecrement(int.MinValue);
            }
        }
    }
	[Test]
    public void NativeLimitedCounter_IncrementDecrement()
    {
        const int MAX = 1000;
        var counter = new NativeLimitedCounter(Allocator.Persistent);
        for (var i = 0; i < MAX/2; ++i) {
            counter.TryIncrement(MAX);
        }
        var job = new Job2();
        job.counter = counter;
        job.Schedule(MAX, 32).Complete();

        Assert.AreEqual(MAX/2, counter.Count);
        counter.Dispose();
    }

    struct Job3 : IJobParallelFor
    {
        public int maximum;
        public NativeLimitedCounter counter;
        public void Execute(int i)
        {
            if ((i/32)%2 > 0) {
                counter.TryIncrement(int.MaxValue);
            } else {
                counter.TryDecrement(int.MinValue);
            }
        }
    }
	[Test]
    public void NativeLimitedCounter_IncrementDecrement_MoreStrict()
    {
        const int MAX = 32*32;
        var counter = new NativeLimitedCounter(Allocator.Persistent);
        for (var i = 0; i < MAX/2; ++i) {
            counter.TryIncrement(MAX);
        }
        var job = new Job3();
        job.counter = counter;
        job.Schedule(MAX, 32).Complete();

        Assert.AreEqual(MAX/2, counter.Count);
        counter.Dispose();
    }

}
