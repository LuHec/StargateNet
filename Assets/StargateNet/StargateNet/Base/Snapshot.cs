namespace StargateNet
{
    public unsafe class Snapshot
    {
        internal int* worldObjectMap; // 标记被占用的NetworkId
        internal int* dirtyObjectMetaMap; // 标记本帧变化的meta
        internal NetworkObjectMeta* worldObjectMeta; // 存储此帧所有物体的meta                 
        internal StargateAllocator networkState; // 存储此帧所有物体的状态

        public Snapshot(int* worldObjectMap, int* worldObjectMeta, int* dirtyObjectMetaMap,
            StargateAllocator networkState)
        {
            this.worldObjectMap = worldObjectMap;
            this.worldObjectMeta = (NetworkObjectMeta*)worldObjectMeta;
            this.dirtyObjectMetaMap = dirtyObjectMetaMap;
            this.networkState = networkState;
            this.networkState.FlushZero(this.worldObjectMap);
            this.networkState.FlushZero(this.worldObjectMeta);
            this.networkState.FlushZero(this.dirtyObjectMetaMap);
        }
    }
}