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
using UTJ;

public class NativeBucketTests {

	[Test]
	public void NativeBucket_Allocate_Deallocate_Read_Write()
	{
		var list = new NativeBucket<int>(100, Allocator.Persistent);
        var listC = (NativeBucket<int>.Concurrent)list;
		listC.Add(1);
		listC.Add(2);

        var array = list.ToArray();
		Assert.AreEqual(2, array.Length);
		Assert.AreEqual(1, array[0]);
		Assert.AreEqual(2, array[1]);

		list.Dispose();
	}

	[Test]
	public void NativeBucket_Copy_Add()
	{
		var list = new NativeBucket<int>(100, Allocator.Persistent);
        var listC = (NativeBucket<int>.Concurrent)list;
		listC.Add(1);
		listC.Add(2);

        var listC2 = listC;
        listC2.Add(3);
        listC2.Add(4);

        var array = list.ToArray();
		Assert.AreEqual(4, array.Length);
		Assert.AreEqual(1, array[0]);
		Assert.AreEqual(2, array[1]);
		Assert.AreEqual(3, array[2]);
		Assert.AreEqual(4, array[3]);

		list.Dispose();
	}

	[Test]
	public void NativeBucket_Many_Add()
	{
        const int NUM = 1000;
		var list = new NativeBucket<int>(NUM, Allocator.Persistent);
        var listC = (NativeBucket<int>.Concurrent)list;

        for (var i = 0; i < NUM; ++i)
        {
            listC.Add(i);
        }

        var array = list.ToArray();
        Assert.AreEqual(list.Length, array.Length);
        for (var i = 0; i < NUM; ++i)
        {
            Assert.AreEqual(i, array[i]);
        }

		list.Dispose();
	}

	[Test]
	public void NativeBucket_Copy_Add_Clear()
	{
		var list = new NativeBucket<int>(100, Allocator.Persistent);
        var listC = (NativeBucket<int>.Concurrent)list;
		listC.Add(1);
		listC.Add(2);

        var listC2 = listC;
        listC2.Add(3);
        listC2.Add(4);

        list.Clear();
        Assert.AreEqual(0, list.Length);

		list.Dispose();
	}

	[Test]
	public void NativeBucket_Copy_Add_Clear_Add()
	{
		var list = new NativeBucket<int>(100, Allocator.Persistent);
		var listC = (NativeBucket<int>.Concurrent)list;
		listC.Add(1);
		listC.Add(2);

        var listC2 = listC;
        listC2.Add(3);
        listC2.Add(4);

        list.Clear();
        Assert.AreEqual(0, list.Length);

        listC.Add(10);
        listC.Add(20);
        listC2.Add(30);
        listC2.Add(40);

        var array = list.ToArray();
		Assert.AreEqual(4, array.Length);
		Assert.AreEqual(10, array[0]);
		Assert.AreEqual(20, array[1]);
		Assert.AreEqual(30, array[2]);
		Assert.AreEqual(40, array[3]);

		list.Dispose();
	}

	[Test]
	public void NativeBucket_Check_Length()
	{
		var bucket = new NativeBucket<int>(16, Allocator.Persistent);
		var bucketC = (NativeBucket<int>.Concurrent)bucket;
        for (var i = 0; i < 10; ++i)
            bucketC.Add(1);
		Assert.AreEqual(10, bucket.Length);
		bucket.Dispose();
	}


	[Test]
	public void NativeBucket_OutOfRangeExceptionOnAdd()
	{
		var bucket = new NativeBucket<int>(1, Allocator.Persistent);
		var bucketC = (NativeBucket<int>.Concurrent)bucket;
		bucketC.Add(1);
		Assert.Throws<System.ArgumentOutOfRangeException>(()=> { bucketC.Add(2); });
		bucket.Dispose();
	}

	[Test]
	public void NativeBucket_CopyTo_OutOfRangeException()
	{
		var bucket = new NativeBucket<int>(100, Allocator.Persistent);
		var bucketC = (NativeBucket<int>.Concurrent)bucket;
        for (var i = 0; i < 100; ++i) {
            bucketC.Add(i);
        }
        var array = new int[100-1];
		Assert.Throws<System.ArgumentOutOfRangeException>(()=> { bucket.CopyTo(array); });
        bucket.Dispose();
    }

	[Test]
	public void NativeBucket_Allocate_Deallocate_int()
	{
		var bucket = new NativeBucket<int>(100, Allocator.Persistent);
		var bucketC = (NativeBucket<int>.Concurrent)bucket;
        bucketC.Add(100);
        bucketC.Add(200);
        var array = new int[100];
        bucket.CopyTo(array);
        Assert.AreEqual(array[0], 100);
        Assert.AreEqual(array[1], 200);
        bucket.Dispose();
    }

    struct A12 {
        public int a, b, c;
    }
	[Test]
	public void NativeBucket_Allocate_Deallocate_struct()
	{
		var bucket = new NativeBucket<A12>(100, Allocator.Persistent);
		var bucketC = (NativeBucket<A12>.Concurrent)bucket;
        bucketC.Add(new A12 { a=10, b=20, c=30, });
        bucketC.Add(new A12 { a=100, b=200, c=300, });
        var array = new A12[100];
        bucket.CopyTo(array);
        Assert.AreEqual(array[0].a, 10);
        Assert.AreEqual(array[1].b, 200);
        bucket.Dispose();
    }

    struct A13 {
        public int a, b, c, d, e;
    }
	[Test]
	public void NativeBucket_Allocate_Deallocate_struct_ref()
	{
		var bucket = new NativeBucket<A13>(100, Allocator.Persistent);
		var bucketC = (NativeBucket<A13>.Concurrent)bucket;
        var a13 = new A13 { a=10, b=20, c=30, };
        bucketC.AddRef(ref a13);
        bucketC.Add(new A13 { a=100, b=200, c=300, });
        var array = new A13[100];
        bucket.CopyTo(array);
        Assert.AreEqual(array[0].a, 10);
        Assert.AreEqual(array[1].b, 200);
        bucket.Dispose();
    }

    struct A33 {
        public int a, b, c, d, e, f, g, h;
        public byte a0;
    }
	[Test]
	public void NativeBucket_Allocate_Deallocate_struct2()
	{
		var bucket = new NativeBucket<A33>(10, Allocator.Persistent);
		var bucketC = (NativeBucket<A33>.Concurrent)bucket;
        bucketC.Add(new A33 { a=10, b=20, c=30, });
        bucketC.Add(new A33 { a=100, b=200, c=300, a0 = 111, });
        var array = new A33[10];
        bucket.CopyTo(array);
        Assert.AreEqual(array[0].a, 10);
        Assert.AreEqual(array[1].b, 200);
        Assert.AreEqual(array[1].a0, (byte)111);
        bucket.Dispose();
    }

	[Test]
	public void NativeList_Allocate_Deallocate_Read_Write()
	{
		var list = new NativeList<int>(Allocator.Persistent);

		list.Add(1);
		list.Add(2);

		Assert.AreEqual(2, list.Length);
		Assert.AreEqual(1, list[0]);
		Assert.AreEqual(2, list[1]);

		list.Dispose();
	}

    struct Job0 : IJobParallelFor
    {
        public NativeBucket<int>.Concurrent bucket;
        public void Execute(int i)
        {
            bucket.Add(i);
        }
    }

	[Test]
    public void NativeBucket_Job0()
    {
        int NUM = 1024;
        var bucket = new NativeBucket<int>(NUM, Allocator.Persistent);
        var job = new Job0();
        job.bucket = bucket;
        job.Schedule(NUM, 8).Complete();
        Assert.AreEqual(bucket.Length, NUM);
        bucket.Dispose();
    }

	[Test]
    public void NativeBucket_Job0_many()
    {
        int NUM = 1024;
        for (var i = 0; i < NUM; ++i)
            NativeBucket_Job0();
    }
}
