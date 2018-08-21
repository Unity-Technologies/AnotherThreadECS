using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Collections;
// using Debug = UnityEngine.Debug;

namespace UTJ {

    struct NativeBucketHeader
    {
        public IntPtr block;
        public int unitSize;
        public int itemsInBlock;
    }

    [StructLayout(LayoutKind.Sequential)]
    [NativeContainer]
    [DebuggerDisplay("Length = {Length}")]
    [DebuggerTypeProxy(typeof(NativeBucketDebugView < >))]
    public unsafe struct NativeBucket<T> : IDisposable where T : struct
    {
        byte*                    m_HeaderBlock;
        int                      m_BlockNumCapacity;
        int                      m_BlockSize;
        Allocator                m_AllocatorLabel;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
        AtomicSafetyHandle       m_Safety;
        [NativeSetClassTypeToNullOnSchedule]
        DisposeSentinel          m_DisposeSentinel;
#endif
        const int ALIGN = 16;     // must be power of two.

        static unsafe NativeBucketHeader* GetHeaderBlock(byte* headerBlock, int i)
        {
            return (NativeBucketHeader*)(headerBlock+(JobsUtility.CacheLineSize * i));
        }

        public NativeBucket(int blockNumCapacity,
                              Allocator allocator)
        {
            if (UnsafeUtility.SizeOf<NativeBucketHeader>() > JobsUtility.CacheLineSize)
                throw new ArgumentException("CacheLineSize is too small.");

            int headerBlockSize = JobsUtility.CacheLineSize * JobsUtility.MaxJobThreadCount;
            int unitSize = UnsafeUtility.SizeOf<T>();
            int blockSize = unitSize * blockNumCapacity;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Native allocation is only valid for Temp, Job and Persistent.
            if (allocator <= Allocator.None)
                throw new ArgumentException("Allocator must be Temp, TempJob or Persistent", nameof(allocator));
            if (blockNumCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(blockNumCapacity), "capacity must be >= 0");
            if (!UnsafeUtility.IsBlittable<T>())
                throw new ArgumentException(string.Format("{0} used in NativeBucket<{0}> must be blittable", typeof(T)));
            if (blockSize > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(blockNumCapacity), $"Length * sizeof(T) cannot exceed {int.MaxValue} bytes");
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0, allocator);
#else
			DisposeSentinel.Create(out m_Safety, out m_DisposeSentinel, 0);
#endif
#endif
            m_HeaderBlock = (byte*)UnsafeUtility.Malloc(headerBlockSize, ALIGN, allocator);
            m_BlockNumCapacity = blockNumCapacity;
            m_BlockSize = blockSize;
            m_AllocatorLabel = allocator;
            
            UnsafeUtility.MemClear(m_HeaderBlock, (long)headerBlockSize);
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            {
                NativeBucketHeader* headerBlock = GetHeaderBlock(m_HeaderBlock, i);
                headerBlock->block = IntPtr.Zero;
                headerBlock->unitSize = unitSize;
                headerBlock->itemsInBlock = 0;
            }
        }

        public void Dispose()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS

            if (!UnsafeUtility.IsValidAllocator(m_AllocatorLabel))
                throw new InvalidOperationException("The NativeBucket can not be Disposed because it was not allocated with a valid allocator.");
#if UNITY_2018_3_OR_NEWER
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
#else
            DisposeSentinel.Dispose(m_Safety, ref m_DisposeSentinel);
#endif
#endif
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            {
                NativeBucketHeader* headerBlock = GetHeaderBlock(m_HeaderBlock, i);
                if (headerBlock->block != IntPtr.Zero)
                    UnsafeUtility.Free((void*)headerBlock->block, m_AllocatorLabel);
            }
            UnsafeUtility.Free(m_HeaderBlock, m_AllocatorLabel);
            m_HeaderBlock = null;
        }

        public void Clear()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            {
                NativeBucketHeader* headerBlock = GetHeaderBlock(m_HeaderBlock, i);
                headerBlock->itemsInBlock = 0;
            }
        }

        public unsafe int Length
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
				AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
                int count = 0;
                for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
                {
                    NativeBucketHeader* headerBlock = GetHeaderBlock(m_HeaderBlock, i);
                    count += headerBlock->itemsInBlock;
                }    
                return count;
            }
        }

        public static void Copy(NativeBucket<T> src, NativeArray<T> dst)
        {
            int copied_num = 0;
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            {
                NativeBucketHeader* headerBlock = GetHeaderBlock(src.m_HeaderBlock, i);
                if (copied_num + headerBlock->itemsInBlock > dst.Length)
                    throw new ArgumentOutOfRangeException("Size of destination NativeArray is smaller than NativeBucket.");
                    
                UnsafeUtility.MemCpy((byte*)dst.GetUnsafePtr() + UnsafeUtility.SizeOf<T>() * copied_num,
                                     (byte*)headerBlock->block,
                                     headerBlock->unitSize * headerBlock->itemsInBlock);
                copied_num += headerBlock->itemsInBlock;
            }
        }
        public static void Copy(NativeBucket<T> src, T[] dst)
        {
            int copied_num = 0;
            var handle = GCHandle.Alloc(dst, GCHandleType.Pinned);
            var addr = handle.AddrOfPinnedObject();
            for (int i = 0; i < JobsUtility.MaxJobThreadCount; ++i)
            {
                NativeBucketHeader* headerBlock = GetHeaderBlock(src.m_HeaderBlock, i);
                if (copied_num + headerBlock->itemsInBlock > dst.Length)
                    throw new ArgumentOutOfRangeException("Size of destination NativeArray is smaller than NativeBucket.");
                    
                UnsafeUtility.MemCpy((byte*)addr + UnsafeUtility.SizeOf<T>() * copied_num,
                                     (byte*)headerBlock->block,
                                     headerBlock->unitSize * headerBlock->itemsInBlock);
                copied_num += headerBlock->itemsInBlock;
            }
            handle.Free();
        }

        public void CopyTo(NativeArray<T> dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            Copy(this, dst);
        }

        public void CopyTo(T[] dst)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            Copy(this, dst);
        }

        public NativeArray<T> ToNativeArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var array = new NativeArray<T>(Length, m_AllocatorLabel);
            Copy(this, array);
            return array;
        }

        public T[] ToArray()
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.CheckReadAndThrow(m_Safety);
#endif
            var array = new T[Length];
            Copy(this, array);
            return array;
        }

        [NativeContainer]
        [NativeContainerIsAtomicWriteOnly]
        public unsafe struct Concurrent
        {
            [NativeDisableUnsafePtrRestriction]
            byte*                    m_HeaderBlock;
            int                      m_BlockNumCapacity;
            int                      m_BlockSize;
            Allocator                m_AllocatorLabel;
            #if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle       m_Safety;
            #endif

            [NativeSetThreadIndex]
            int m_ThreadIndex;

            public unsafe static implicit operator NativeBucket<T>.Concurrent (NativeBucket<T> bucket)
            {
                NativeBucket<T>.Concurrent concurrent;
                #if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(bucket.m_Safety);
                concurrent.m_Safety = bucket.m_Safety;
                AtomicSafetyHandle.UseSecondaryVersion(ref concurrent.m_Safety);
                #endif
                
                concurrent.m_HeaderBlock = bucket.m_HeaderBlock;
                concurrent.m_BlockNumCapacity = bucket.m_BlockNumCapacity;
                concurrent.m_BlockSize = bucket.m_BlockSize;
                concurrent.m_AllocatorLabel = bucket.m_AllocatorLabel;
                concurrent.m_ThreadIndex = 0;
                return concurrent;
            }

            void CheckAndAllocateBlock(NativeBucketHeader* header)
            {
                IntPtr block0 = header->block;
                if (block0 == IntPtr.Zero)
                {
                    IntPtr prev = Interlocked.CompareExchange(ref header->block, (IntPtr)(-1), IntPtr.Zero);
                    if (prev == IntPtr.Zero)
                    {
                        byte* block = (byte*)UnsafeUtility.Malloc(m_BlockSize, 16, m_AllocatorLabel);
                        // Interlocked.Exchange(ref header->block, (IntPtr)block);
                        header->block = (IntPtr)block;
                    }
                    else
                    {
                        while (header->block == (IntPtr)(-1)) {}
                    }
                }
                else if (block0 == (IntPtr)(-1))
                {
                    while (header->block == (IntPtr)(-1)) {}
                }
            }

            public void Add(T data)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                int tlsIdx = m_ThreadIndex;
                NativeBucketHeader* header = (NativeBucketHeader*)GetHeaderBlock(m_HeaderBlock, tlsIdx);
                CheckAndAllocateBlock(header);
                int incNum = Interlocked.Increment(ref header->itemsInBlock);
                if (incNum > m_BlockNumCapacity)
                    throw new ArgumentOutOfRangeException(nameof(m_BlockNumCapacity), "exceeded.");
                UnsafeUtility.WriteArrayElementWithStride((void*)header->block,
                                                          incNum-1,
                                                          header->unitSize,
                                                          data);
            }
        
            public void AddRef(ref T data)
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                AtomicSafetyHandle.CheckWriteAndThrow(m_Safety);
#endif
                int tlsIdx = m_ThreadIndex;
                NativeBucketHeader* header = (NativeBucketHeader*)GetHeaderBlock(m_HeaderBlock, tlsIdx);
                CheckAndAllocateBlock(header);
                int incNum = Interlocked.Increment(ref header->itemsInBlock);
                if (incNum > m_BlockNumCapacity)
                    throw new ArgumentOutOfRangeException(nameof(m_BlockNumCapacity), "exceeded.");
                UnsafeUtility.WriteArrayElementWithStride((void*)header->block,
                                                          incNum-1,
                                                          header->unitSize,
                                                          data);
            }
        }
    }

    sealed class NativeBucketDebugView<T> where T : struct
    {
        NativeBucket<T> m_Bucket;
        public NativeBucketDebugView(NativeBucket<T> bucket)
        {
            m_Bucket = bucket;
        }

        public T[] Items => m_Bucket.ToArray();
    }


} // namespace UTJ {
