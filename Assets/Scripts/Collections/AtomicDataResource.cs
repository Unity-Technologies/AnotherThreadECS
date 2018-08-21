using System;
// using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
// using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.Assertions;

namespace UTJ {

[StructLayout(LayoutKind.Sequential)]
unsafe public struct AtomicFloat
{
    [NativeDisableUnsafePtrRestriction] public byte* m_Data;
    public long m_Version;

    static int GetResourceOffset()
    {
        return (UnsafeUtility.SizeOf<float>());
    }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
    void CheckInitialized()
    {
        if (m_Data == null) {
            throw new NullReferenceException("The object cannot be used without AtomicFloatResource.Create");
        }
    }
#endif

    public AtomicFloat(byte* ptr, long version, float initial_value)
    {
        m_Data = ptr;
        UnsafeUtility.WriteArrayElement<float>(m_Data, 0 /* index */, initial_value);
        m_Version = version;
    }

    public float get()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        CheckInitialized();
#endif
        return UnsafeUtility.ReadArrayElement<float>(m_Data, 0 /* index */);
    }

    public long Version { get { return m_Version; } }

    public void set(float value)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        CheckInitialized();
#endif
        UnsafeUtility.WriteArrayElement<float>(m_Data, 0 /* index */, value);
    }

    /// <summary>Atomic add.</summary>
    /// <param name="value">a value being added.</param>
    /// <returns>added value by this operation.</returns>
    public float add(float value)
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        CheckInitialized();
#endif
        float current = UnsafeUtility.ReadArrayElement<float>(m_Data, 0 /* index */);
        int currenti = math.asint(current);
        for (;;) {
            float next = current + value;
            int nexti = math.asint(next);
            /* Because of [BurstCompile], float(=Single) cannot be used directly */
            int prev = Interlocked.CompareExchange(ref *(int*)m_Data, nexti, currenti);
            if (prev == currenti) {
                return next;
            } else {
                currenti = prev;
                current = math.asfloat(prev);
            }
        }
    }

    public bool Exists()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        CheckInitialized();
#endif
        AtomicFloatResource resource;
        UnsafeUtility.CopyPtrToStructure(m_Data+GetResourceOffset(), out resource);
        return resource.Exists(ref this);
    }

    public void Dispose()
    {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        CheckInitialized();
#endif
        AtomicFloatResource resource;
        UnsafeUtility.CopyPtrToStructure(m_Data+GetResourceOffset(), out resource);
        resource.Destroy(ref this);
    }

    public static implicit operator float(AtomicFloat a) { return a.get(); } 
    public static implicit operator int(AtomicFloat a) { return (int)a.get(); } 
}

[StructLayout(LayoutKind.Sequential)]
unsafe public struct AtomicFloatResource : IDisposable
{
    const long ALIVE_FLG = 0x4000000000000000L;
    struct Table
    {
        public long m_Value;    // both alive flag and version reside here.
    }

    const int ALIGN = 16;
    [NativeDisableUnsafePtrRestriction] int* m_Index;
    [NativeDisableUnsafePtrRestriction] Table* m_Table;
    [NativeDisableUnsafePtrRestriction] byte* m_Data;
    int m_Capacity;
    Allocator m_AllocatorLabel;

    static int GetUnitSize()
    {
        return (UnsafeUtility.SizeOf<float>() + // value
                UnsafeUtility.SizeOf<AtomicFloatResource>());
    }

    public AtomicFloatResource(int capacity, Allocator allocator)
    {
        Assert.IsTrue(UnsafeUtility.SizeOf<float>() <= 8);
        Assert.IsTrue(UnsafeUtility.SizeOf<float>() == UnsafeUtility.SizeOf<int>());
        m_Index = (int*)UnsafeUtility.Malloc(4, ALIGN, allocator);
        long table_size = capacity * UnsafeUtility.SizeOf<Table>();
        m_Table = (Table*)UnsafeUtility.Malloc(table_size, ALIGN, allocator);
        long size = capacity * GetUnitSize();
        m_Data = (byte*)UnsafeUtility.Malloc(size, ALIGN, allocator);
        m_AllocatorLabel = allocator;
        m_Capacity = capacity;

        *m_Index = -1;
        UnsafeUtility.MemClear(m_Table, table_size);
        UnsafeUtility.MemClear(m_Data, size);
    }

    public bool IsCreated { get { return m_Index != null; } }

    public void Dispose()
    {
        if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
            throw new InvalidOperationException("The NativeDataResource can not be Disposed because it was not allocated with a valid allocator.");
        UnsafeUtility.Free(m_Data, m_AllocatorLabel);
        m_Data = null;
        UnsafeUtility.Free(m_Table, m_AllocatorLabel);
        m_Table = null;
        UnsafeUtility.Free(m_Index, m_AllocatorLabel);
        m_Index = null;
    }

    int get_index(ref AtomicFloat afloat)
    {
        long ptrdiff = (long)afloat.m_Data - (long)m_Data;
        long index = ptrdiff / GetUnitSize();
        Assert.AreEqual(index*GetUnitSize(), ptrdiff);
        return (int)index;
    }

    public bool Exists(ref AtomicFloat afloat)
    {
        int index = get_index(ref afloat);
        var value = (m_Table+index)->m_Value;
        return (value & ALIVE_FLG) != 0 && (value & ~ALIVE_FLG) == afloat.Version;
    }

    public AtomicFloat Create(float initial_value)
    {
        int LOOP_MAX = m_Capacity;
        int current_index = -1;
        long current = 0;
        int i = 0;
        for (; i < LOOP_MAX; ++i) {
            current_index = Interlocked.Increment(ref *m_Index) % LOOP_MAX;
            current = (m_Table+current_index)->m_Value;
            var next = ALIVE_FLG | current;
            var prev = Interlocked.CompareExchange(ref (m_Table+current_index)->m_Value, next, current);
            if (prev == current) {
                break;
            }
        }
        Assert.AreNotEqual(current_index, -1, "bug.");
        Assert.AreNotEqual(i, LOOP_MAX, "capacity exceeded.");

        byte* ptr = m_Data+(GetUnitSize()*current_index);
        UnsafeUtility.CopyStructureToPtr(ref this, ptr+UnsafeUtility.SizeOf<float>() /* value */); // embed whole AtomicFloatResource here.
        return new AtomicFloat(ptr, current, initial_value);
    }

    public void Destroy(ref AtomicFloat afloat)
    {
        for (;;) {
            if (!Exists(ref afloat)) {
                return;
            }
            int index = get_index(ref afloat);
            var current = (m_Table+index)->m_Value;
            var next = (current & ~ALIVE_FLG) + 1;
            var prev = Interlocked.CompareExchange(ref (m_Table+index)->m_Value, next, current);
            if (prev == current) {
                return;
            }
        }
    }

    public int getCountInUse()
    {
        int cnt = 0;
        for (var i = 0; i < m_Capacity; ++i) {
            var value = (m_Table+i)->m_Value;
            if ((value & ALIVE_FLG) != 0) {
                ++cnt;
            }
        }
        return cnt;
    }

    public int Capacity { get { return m_Capacity; } }
}

} // namespace UTJ {
