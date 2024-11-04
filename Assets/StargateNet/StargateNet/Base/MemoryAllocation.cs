using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

namespace StargateNet
{
    public static class MemoryAllocation
    {
        internal static IMemoryAllocator Allocator;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void* Malloc(long size, int alignment = 8)
        {
            return MemoryAllocation.Allocator.Malloc(size, alignment);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Free(void* ptr) => MemoryAllocation.Allocator.Free(ptr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Clear(void* ptr, long sizeBytes)
        {
            MemoryAllocation.Allocator.Clear(ptr, sizeBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Copy(void* dest, void* source, long sizeBytes)
        {
            MemoryAllocation.Allocator.Copy(dest, source, sizeBytes);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe bool Cmp(void* ptr1, void* ptr2, long sizeBytes)
        {
            return MemoryAllocation.Allocator.Cmp(ptr1, ptr2, sizeBytes);
        }
    }
}