using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace StargateNet
{
    public unsafe class SgAllocator
    {
        private const int Alignment = 10;
        // private TLSF64 _tlsf64;
        
        public SgAllocator()
        {
            
        }

        public void* Alloc(long size, int alignment = 8)
        {
            return UnsafeUtility.Malloc(size, alignment, Allocator.Persistent);
        }
    }
}