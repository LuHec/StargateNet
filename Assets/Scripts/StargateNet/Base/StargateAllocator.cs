using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.VisualScripting.YamlDotNet.RepresentationModel;

namespace StargateNet
{
    /// <summary>
    /// Snapshot使用的分配器，总大小是固定的。数量限定在16~64(32)，给服务端使用。
    /// </summary>
    public unsafe class StargateAllocator
    {
        internal Dictionary<int, MemoryPool> pools; // 对于snapshot来说，存储了所有网络物体的syncvar
        private const int TLSF64_ALIGNMENT = 8;
        private readonly void* _block;

        public StargateAllocator(long byteSize)
        {
            if (byteSize < 0) throw new Exception("SgAllocator can't init with negative size!");
            byteSize += sizeof(TLSF64.control_t);
            this._block = MemoryAllocation.Malloc(byteSize, TLSF64_ALIGNMENT);
            this._block = TLSF64.tlsf_create_with_pool(this._block, (ulong)byteSize);
        }

        // SgAllocator在初始化时实际上就已经分配好了总的大小(sync var)
        // 一个网络id对应一个pool，pool内的内存就是该物体的全部脚本的同步变量所占用的内存
        // SgAllocator的总大小是不会变动的，而TLSF拿和还都是O1的时间复杂度，所以当同步物体发生变化时，不需要重新分配一个Snapshot，直接归还被删除的网络id所占用的内存即可，
        // 所以我决定用字典来保存这个映射关系，平均时间复杂度是o1，且避免了netid经过大量回收后混乱的问题(存疑，可能会导致回滚出问题，因为复用了id)
        private void* Malloc(ulong size)
        {
            return TLSF64.tlsf_malloc(this._block, size);
        }

        private void Free(void* memory)
        {
            TLSF64.tlsf_free(_block, memory);
        }

        public bool AddPool(int id, long byteSize)
        {
            if (byteSize < 0) throw new Exception("SgAllocator can't create negative size!");
            void* data = TLSF64.tlsf_malloc(this._block, (ulong)byteSize);
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
                TLSF64.tlsf_free(_block, pool.Value.data);
            }
        }

        public struct MemoryPool
        {
            public void* data;
            public long byteSize;
        }
    }
}