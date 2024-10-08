using System.Runtime.CompilerServices;

namespace StargateNet
{
    /// <summary>
    /// A networked object isn't actually a GameObject;
    /// It's merely conceptual, existing to represent an entity within a networking context.
    /// </summary>
    public sealed class Entity
    {
        public SgNetworkEngine engine;
        public INetworkEntity entity;               // A GameObject which implement INetworkEntity
        public readonly int entityBlockSize;        // Networked Field Size 
        internal unsafe int* stateBlockPtr;         // Networked Field memory block base address
        internal int[] bitmap;                       // bit dirtymap

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

        public static unsafe void DirtifyData(INetworkEntityScript networkEntityScript, int* newValue, int* address, int byteSize)
        {
            networkEntityScript.Entity.SetData(newValue, address, byteSize);
        }
    }
    
    
}