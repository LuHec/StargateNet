using System.Runtime.CompilerServices;

namespace StargateNet
{
    /// <summary>
    /// A networked object isn't actually a GameObject;
    /// It's merely conceptual, existing to represent an entity within a networking context.
    /// </summary>
    public sealed class Entity
    {
        internal NetworkObjectRef networkId; // 客户端服务端一定是一致的
        internal int poolId = -1; // 内存的idx，客户端和服务端不一定一致
        internal int worldMetaId = -1; // meta的idx，客户端服务端一定是一致的
        internal StargateEngine engine;
        internal INetworkEntity entity; // Truly Object
        internal int entityBlockWordSize; // Networked Field Size, 不包括bitmap大小(两者大小一致) 
        internal unsafe int* stateBlock; // Networked Field memory block base address
        internal unsafe int* dirtyMap; // bit dirtymap
        internal bool dirty = false;


        /// <summary>
        /// 初始化脚本等.获取大小等等
        /// </summary>
        /// <param name="networkId"></param>
        /// <param name="engine"></param>
        /// <param name="entity">真正的实体对象</param>
        public Entity(NetworkObjectRef networkId, StargateEngine engine, INetworkEntity entity)
        {
            this.networkId = networkId;
            this.engine = engine;
            this.entity = entity;
        }

        public unsafe void Initialize(int* stateBlockPtr, int* bitmapPtr, int blockWordSize, int poolId,
            int worldMetaId, NetworkBehavior[] networkBehaviors)
        {
            this.stateBlock = stateBlockPtr;
            this.dirtyMap = bitmapPtr;
            this.entityBlockWordSize = blockWordSize;
            this.poolId = poolId;
            this.worldMetaId = worldMetaId;
            int wordOffset = 0;
            for (int i = 0; i < networkBehaviors.Length; i++)
            {
                networkBehaviors[i].StateBlock = this.stateBlock + wordOffset;
                wordOffset += networkBehaviors[i].StateBlockSize / 4; // 懒得改ilprocessor，所以暂时用字节数
            }
        }

        /// <summary>
        /// 每个Tick后都清理
        /// </summary>
        public unsafe void TickReset()
        {
            this.dirty = false;
            for (int i = 0; i < entityBlockWordSize; i++)
            {
                this.dirtyMap[i] = 0;
            }
        }

        public unsafe void Reset()
        {
            this.stateBlock = null;
            this.dirtyMap = null;
            this.entityBlockWordSize = 0;
            this.poolId = -1;
            this.worldMetaId = -1;
            this.networkId = NetworkObjectRef.InvalidNetworkObjectRef;
        }

        /// <summary>
        /// Set data and make bitmap dirty 
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="address"></param>
        /// <param name="wordSize">一个word长度为4字节</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetData(int* newValue, int* address, int wordSize)
        {
            // 内存大小不超过INT_MAX
            int dataId = (int)(address - stateBlock);

            bool needsUpdate = false;
            for (int i = 0; i < wordSize; i++)
            {
                if (stateBlock[dataId + i] != newValue[i])
                {
                    needsUpdate = true;
                    stateBlock[dataId + i] = newValue[i];
                }
            }

            if (this.engine.IsServer && needsUpdate)
            {
                MakeBitmapDirty(dataId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void MakeBitmapDirty(int dataId)
        {
            this.dirtyMap[dataId] = 1;
            this.dirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DirtifyData(IStargateNetworkScript stargateNetworkScript, int* newValue, int* address,
            int wordSize)
        {
            stargateNetworkScript.Entity.SetData(newValue, address, wordSize);
        }
    }
}