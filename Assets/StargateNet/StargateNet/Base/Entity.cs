using System.Runtime.CompilerServices;

namespace StargateNet
{
    /// <summary>
    /// A networked object isn't actually a GameObject;
    /// It's merely conceptual, existing to represent an entity within a networking context.
    /// </summary>
    public sealed class Entity
    {
        internal NetworkObjectRef networkId;                       // networked entity unique id
        internal StargateEngine engine;
        internal INetworkEntity entity;               // Truly Object
        internal int entityBlockByteSize;        // Networked Field Size 
        internal unsafe int* stateBlock;         // Networked Field memory block base address
        internal unsafe int* bitmap;                       // bit dirtymap

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

        public unsafe void Initialize(int* stateBlockPtr, int* bitmapPtr, int blockByteSize)
        {
            this.stateBlock = stateBlock;
            this.bitmap = bitmapPtr;
            this.entityBlockByteSize = blockByteSize;
        }

        /// <summary>
        /// Set data and make bitmap dirty 
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="address"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetData(int* newValue, int* address, int size)
        {
            // 内存大小不超过INT_MAX
            int dataId = (int)(address - stateBlock);
            
            // size是以int为单位的
            for (int i = 0; i < size; i++)
            {
                stateBlock[dataId + i] = newValue[i];
            }
            
            if (this.engine.IsServer)
            {
                MakeBitmapDirty(dataId);
            }   
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void MakeBitmapDirty(int dataId)
        {
            bitmap[dataId] = 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DirtifyData(IStargateNetworkScript stargateNetworkScript, int* newValue, int* address, int size)
        {
            stargateNetworkScript.Entity.SetData(newValue, address, size);
        }
    }
    
    
}