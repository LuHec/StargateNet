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
        internal readonly int entityBlockSize;        // Networked Field Size 
        internal unsafe int* stateBlockPtr;         // Networked Field memory block base address
        internal int[] bitmap;                       // bit dirtymap

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

        /// <summary>
        /// Set data and make bitmap dirty 
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="address"></param>
        /// <param name="byteSize">byteSize</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public unsafe void SetData(int* newValue, int* address, int byteSize)
        {
            int dataId = (int)(address - stateBlockPtr);
            
            for (int i = 0; i < byteSize; i++)
            {
                stateBlockPtr[dataId + i] = newValue[i];
            }
            
            if (this.engine.IsServer)
            {
                MakeBitmapDirty(dataId);
            }   
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe void MakeBitmapDirty(int dataId)
        {
            int groupId = dataId / sizeof(int);
            int groupOffset = dataId % sizeof(int);
            bitmap[groupId] |= 1 << groupOffset;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DirtifyData(IStargateNetworkScript stargateNetworkScript, int* newValue, int* address, int byteSize)
        {
            stargateNetworkScript.Entity.SetData(newValue, address, byteSize);
        }
    }
    
    
}