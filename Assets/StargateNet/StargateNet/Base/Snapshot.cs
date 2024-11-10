namespace StargateNet
{
    public unsafe class Snapshot
    {
        internal int* networkObjectMap; // 标记被占用的NetworkId
        internal int* worldMeta; // 存储此帧所有物体的meta                 
        internal StargateAllocator networkState; // 存储此帧所有物体的状态

        public Snapshot(int* networkObjectMap, int* worldMeta, StargateAllocator networkState)
        {
            this.networkObjectMap = networkObjectMap;
            this.worldMeta = worldMeta;
            this.networkState = networkState;
            this.networkState.FlushZero(this.networkObjectMap);
            this.networkState.FlushZero(this.worldMeta);
        }
    }
}