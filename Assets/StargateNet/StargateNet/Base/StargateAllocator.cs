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
        internal List<MemoryPool> pools = new(32); // 对于snapshot来说，存储了所有网络物体的syncvar
        internal Monitor monitor;
        internal long Size => size;
        private const int TLSF64_ALIGNMENT = 8;
        private readonly void* _entireBlock;
        private Queue<int> _recycledPoolId = new(32);
        private readonly long size;

        /// <summary>
        /// 由于control_t和block_header的存在，申请大小必须大于实际需要的内存大小
        /// </summary>
        /// <param name="byteSize"></param>
        /// <param name="monitor"></param>
        /// <exception cref="Exception"></exception>
        public StargateAllocator(long byteSize, Monitor monitor)
        {
            if (byteSize < 0) throw new Exception("SgAllocator can't init with negative size!");
            this.monitor = monitor;
            byteSize += sizeof(TLSF64.control_t);
            this._entireBlock = MemoryAllocation.Malloc(byteSize, TLSF64_ALIGNMENT);
            this._entireBlock = TLSF64.tlsf_create_with_pool(this._entireBlock, (ulong)byteSize);
            this.size = byteSize;
            if (monitor != null)
                monitor.unmanagedMemeory += (ulong)byteSize;
        }

        ~StargateAllocator()
        {
            MemoryAllocation.Free(this._entireBlock);    
        }

        /// <summary>
        /// 手动归还内存
        /// </summary>
        public void HandledRelease()
        {
            MemoryAllocation.Free(this._entireBlock);    
        }

        // SgAllocator在初始化时实际上就已经分配好了总的大小(sync var)
        // 一个网络id对应一个pool，pool内的内存就是该物体的全部脚本的同步变量所占用的内存
        // SgAllocator的总大小是不会变动的，而TLSF拿和还都是O1的时间复杂度，所以当同步物体发生变化时，不需要重新分配一个Snapshot，直接归还被删除的网络id所占用的内存即可，
        // 所以我决定用字典来保存这个映射关系，平均时间复杂度是o1，且避免了netid经过大量回收后混乱的问题(存疑，可能会导致回滚出问题，因为复用了id)
        public void* Malloc(long byteSize)
        {
            void* block = TLSF64.tlsf_malloc(this._entireBlock, (ulong)byteSize);
            this.monitor.unmanagedMemeoryInuse += TLSF64.tlsf_block_size(block);
            // 这里之前转成int然后用byteSize / 4去算了，在byteSize不是4的倍数下是错的
            for (int i = 0; i < byteSize; i++)
            {
                ((byte*)block)[i] = 0;
            }
            return block;
        }

        public void FlushZero(void* block)
        {
            UnsafeUtility.MemSet(block, 0, (long)TLSF64.tlsf_block_size(block));
        }

        public void Free(void* block)
        {
            this.monitor.unmanagedMemeoryInuse -= TLSF64.tlsf_block_size(block);
            TLSF64.tlsf_free(_entireBlock, block);
        }

        public void AddPool(long byteSize, out int poolId)
        {
            if (byteSize < 0) throw new Exception("SgAllocator can't create negative size!");
            void* data = this.Malloc(byteSize);
            MemoryPool pool;
            pool.dataPtr = data;
            pool.used = true;
            pool.byteSize = byteSize;
            if (this._recycledPoolId.Count > 0)
            {
                poolId = this._recycledPoolId.Dequeue();
                if(this.pools[poolId].used) throw new Exception($"Pool {poolId} already used!");
                this.pools[poolId] = pool;
            }
            else
            {
                this.pools.Add(pool);
                poolId = this.pools.Count - 1;
            }
        }

        public unsafe void ReleasePool(int id)
        {
            if (id > this.pools.Count) throw new Exception("PoolId out of range!");
            MemoryPool pool = this.pools[id];
            if (!pool.used) return;
            this.Free(pool.dataPtr);
            pool.byteSize = -1;
            pool.dataPtr = null;
            pool.used = false;
            this.pools[id] = pool;
            this._recycledPoolId.Enqueue(id);
        }

        /// <summary>
        /// 将池中的内存拷贝到目标
        /// </summary>
        /// <param name="dest"></param>
        public void CopyTo(StargateAllocator dest)
        {
            if (dest.Size < this.Size) throw new Exception("Dest allocator size is too small!");
            dest.FastRelease();
            for (int poolIdx = 0; poolIdx < this.pools.Count; poolIdx++)
            {
                MemoryPool thisMemoryPool = this.pools[poolIdx];
                MemoryPool destMemoryPool;
                destMemoryPool.used = thisMemoryPool.used;
                destMemoryPool.byteSize = thisMemoryPool.used ? thisMemoryPool.byteSize : -1;
                destMemoryPool.dataPtr = thisMemoryPool.used ? dest.Malloc(thisMemoryPool.byteSize) : null;
                if (thisMemoryPool.used)
                {
                    for (int byteIdx = 0; byteIdx < thisMemoryPool.byteSize; byteIdx++)
                    {
                        ((byte*)destMemoryPool.dataPtr)[byteIdx] = ((byte*)thisMemoryPool.dataPtr)[byteIdx];
                    }
                }

                dest.pools.Add(destMemoryPool);
            }
        }


        /// <summary>
        /// 将所有内存归还
        /// </summary>
        public void FastRelease()
        {
            for (int i = 0; i < pools.Count; i++)
            {
                MemoryPool pool = pools[i];
                if (!pool.used) continue;
                this.Free(pool.dataPtr);
                pool.byteSize = -1;
                pool.dataPtr = null;
                pools[i] = pool;
                _recycledPoolId.Enqueue(i);
            }

            this._recycledPoolId.Clear();
            this.pools.Clear();
        }

        public struct MemoryPool
        {
            public bool used;
            public void* dataPtr;
            /// <summary>
            /// 这个不是给state用的！！！！包含了tlsf的尾部合并信息！！！！不要用这个！！！！
            /// </summary>
            public long byteSize; 
        }
    }
}