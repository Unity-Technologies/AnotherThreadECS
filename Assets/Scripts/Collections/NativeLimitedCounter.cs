// using System;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
// using Unity.Jobs.LowLevel.Unsafe;

namespace UTJ {

/*
 * Counter which has a limitation.
 */

[StructLayout(LayoutKind.Sequential)]
public unsafe struct NativeLimitedCounter
{
    [NativeDisableUnsafePtrRestriction]
    int* m_Counter;
    Allocator m_AllocatorLabel;

    // public IntPtr getID() { return (IntPtr)m_Counter; }

    public NativeLimitedCounter(Allocator label)
    {
        m_AllocatorLabel = label;
        long size = UnsafeUtility.SizeOf<int>();
        m_Counter = (int*)UnsafeUtility.Malloc(size, 16, label);
        UnsafeUtility.MemClear(m_Counter, size);
    }

    public bool TryIncrement(int inclusive_limit)
    {
        int current = *m_Counter;
        while (current < inclusive_limit) {
            int next = current + 1;
            int prev = Interlocked.CompareExchange(ref *m_Counter, next, current);
            if (prev == current) {
                return true;
            } else {
                current = prev;
            }
        }
        return false;
    }

    public bool TryDecrement(int inclusive_limit)
    {
        int current = *m_Counter;
        while (current > inclusive_limit) {
            int next = current - 1;
            int prev = Interlocked.CompareExchange(ref *m_Counter, next, current);
            if (prev == current) {
                return true;
            } else {
                current = prev;
            }
        }
        return false;
    }

    // public bool TryIncrementDangerous(int inclusive_limit) // not thread safe.
    // {
    //     int current = *m_Counter;
    //     if (current < inclusive_limit) {
    //         *m_Counter = current + 1;
    //         return true;
    //     } else {
    //         return false;
    //     }
    // }

    public int Count
    {
        get
        {
            return *m_Counter;
        }
    }

    public void Clear()
    {
        *m_Counter = 0;
    }

    public bool IsCreated
    {
        get { return m_Counter != null; }
    }

    public void Dispose()
    {
        UnsafeUtility.Free(m_Counter, m_AllocatorLabel);
        m_Counter = null;
    }
}

} // namespace UTJ {
