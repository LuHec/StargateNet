using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace StargateNet
{
    /// <summary>
    /// Snapshot使用的分配器，总大小是固定的。数量限定在16~64(32)，给服务端使用。
    /// </summary>
    public unsafe class StargateAllocator
    {
        internal Dictionary<int, MemoryPool> pools = new(32); // 对于snapshot来说，存储了所有网络物体的syncvar
        internal Monitor monitor;
        private const int TLSF64_ALIGNMENT = 8;
        private readonly void* _entireBlock;

        public StargateAllocator(long byteSize, Monitor monitor)
        {
            if (byteSize < 0) throw new Exception("SgAllocator can't init with negative size!");
            this.monitor = monitor;
            byteSize += sizeof(TLSF64.control_t);
            this._entireBlock = MemoryAllocation.Malloc(byteSize, TLSF64_ALIGNMENT);
            this._entireBlock = TLSF64.tlsf_create_with_pool(this._entireBlock, (ulong)byteSize);

            if (monitor != null)
                monitor.unmanagedMemeory += (ulong)byteSize;
        }

        // SgAllocator在初始化时实际上就已经分配好了总的大小(sync var)
        // 一个网络id对应一个pool，pool内的内存就是该物体的全部脚本的同步变量所占用的内存
        // SgAllocator的总大小是不会变动的，而TLSF拿和还都是O1的时间复杂度，所以当同步物体发生变化时，不需要重新分配一个Snapshot，直接归还被删除的网络id所占用的内存即可，
        // 所以我决定用字典来保存这个映射关系，平均时间复杂度是o1，且避免了netid经过大量回收后混乱的问题(存疑，可能会导致回滚出问题，因为复用了id)
        public void* Malloc(ulong size)
        {
            void* block = TLSF64.tlsf_malloc(this._entireBlock, size);
            this.monitor.unmanagedMemeoryInuse += TLSF64.tlsf_block_size(block);
            return block;
        }

        public void Free(void* block)
        {
            this.monitor.unmanagedMemeoryInuse -= TLSF64.tlsf_block_size(block);
            TLSF64.tlsf_free(_entireBlock, block);
        }

        public bool AddPool(int id, long byteSize)
        {
            if (byteSize < 0) throw new Exception("SgAllocator can't create negative size!");
            void* data = TLSF64.tlsf_malloc(this._entireBlock, (ulong)byteSize);
            return data != null && pools.TryAdd(id, new MemoryPool() { data = data, byteSize = byteSize });
        }

        public bool ReleasePool(int id)
        {
            if (!pools.ContainsKey(id)) return false;
            return pools.Remove(id);
        }

        /// <summary>
        /// 将所有内存归还
        /// </summary>
        public void FastRelease()
        {
            foreach (var pool in pools)
            {
                TLSF64.tlsf_free(_entireBlock, pool.Value.data);
            }
        }

        public struct MemoryPool
        {
            public void* data;
            public long byteSize;
        }
    }
}