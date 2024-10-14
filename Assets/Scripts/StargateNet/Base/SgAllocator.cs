using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace StargateNet
{
    public unsafe class SgAllocator
    {
        private const int TLSF64_ALIGNMENT = 16;
        private void* _block;

        public SgAllocator(long byteSize)
        {
            if (byteSize < 0) throw new Exception("SgAllocator can't init with negative size!");
            byteSize += sizeof(TLSF64.control_t);
            this._block = UnsafeUtility.Malloc(byteSize, TLSF64_ALIGNMENT, Allocator.Persistent);
            this._block = TLSF64.tlsf_create_with_pool(this._block, (ulong)byteSize);
        }

        // SgAllocator在初始化时实际上就已经分配好了总的大小(sync var)
        // 一个网络id对应一个pool，pool内的内存就是该物体的全部脚本的同步变量所占用的内存
        // SgAllocator的总大小是不会变动的，而TLSF拿和还都是O1的时间复杂度，所以当同步物体发生变化时，不需要重新分配一个Snapshot，直接归还被删除的网络id所占用的内存即可，
        // 所以我决定用字典来保存这个映射关系，平均时间复杂度是o1，且避免了
        public void* Malloc(ulong size)
        {
            return TLSF64.tlsf_malloc(this._block, size);
        }

        public void Free(void* memory)
        {
            TLSF64.tlsf_free(_block, memory);
        }
    }
}