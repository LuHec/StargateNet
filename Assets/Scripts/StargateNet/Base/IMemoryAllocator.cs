using System.Runtime.CompilerServices;

namespace StargateNet
{
    public interface IMemoryAllocator
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void* Malloc(long size, int alignment = 4);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void Free(void* ptr);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void Clear(void* ptr, long sizeBytes);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe void Copy(void* dest, void* source, long sizeBytes);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        unsafe bool Cmp(void* ptr1, void* ptr2, long sizeBytes);
    }
}