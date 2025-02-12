using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace StargateNet
{
    public class UnityAllocator : IMemoryAllocator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void* Malloc(long size, int alignment = 4)
        {
            return UnsafeUtility.Malloc(size, alignment, Allocator.Persistent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Free(void* ptr) => UnsafeUtility.Free(ptr, Allocator.Persistent);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void Clear(void* ptr, long sizeBytes) => UnsafeUtility.MemClear(ptr, sizeBytes);

        /// <summary>
        /// 别用！！！会爆炸！！！
        /// </summary>
        /// <param name="dest"></param>
        /// <param name="source"></param>
        /// <param name="sizeBytes"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [Obsolete("别用！！！会爆炸！！！")]
        public unsafe void Copy(void* dest, void* source, long sizeBytes)
        {
            UnsafeUtility.MemCpy(dest, source, sizeBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe bool Cmp(void* ptr1, void* ptr2, long sizeBytes)
        {
            return UnsafeUtility.MemCmp(ptr1, ptr2, sizeBytes) == 0;
        }
    }
}