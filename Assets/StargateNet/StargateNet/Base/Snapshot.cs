namespace StargateNet
{
    public unsafe class Snapshot
    {
        internal Tick snapshotTick;
        internal int* dirtyObjectMetaMap; // 标记本帧变化的meta
        internal NetworkObjectMeta* worldObjectMeta; // 存储此帧所有物体的meta                 
        internal StargateAllocator networkState; // 存储此帧所有物体的状态
        internal readonly int metaCnt;

        public Snapshot(int* worldObjectMeta, int* dirtyObjectMetaMap,
            StargateAllocator networkState, int metaCnt)
        {
            this.metaCnt = metaCnt;
            this.snapshotTick = Tick.InvalidTick;
            this.worldObjectMeta = (NetworkObjectMeta*)worldObjectMeta;
            this.dirtyObjectMetaMap = dirtyObjectMetaMap;
            this.networkState = networkState;
            this.networkState.FlushZero(this.worldObjectMeta);
            this.networkState.FlushZero(this.dirtyObjectMetaMap);
            this.Init();
        }

        internal void Init()
        {
            for (int i = 0; i < this.metaCnt; i++)
            {
                this.worldObjectMeta[i].networkId = -1;
                this.worldObjectMeta[i].prefabId = -1;
                this.worldObjectMeta[i].stateWordSize = -1;
                this.worldObjectMeta[i].destroyed = false;
            }
        }
    }
}