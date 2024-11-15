namespace StargateNet
{
    public unsafe class Snapshot
    {
        internal Tick snapshotTick;
        internal int* poolIdxMap; // TODO:回放功能需要用到，因为缺失了Entity信息，所以需要额外记录。
        internal int* dirtyObjectMetaMap; // 标记本帧变化的meta
        internal NetworkObjectMeta* worldObjectMeta; // 存储此帧所有物体的meta                 
        internal StargateAllocator networkStates; // 存储此帧所有物体的状态，给回放用
        internal readonly int metaCnt;

        public Snapshot(int* worldObjectMeta, int* dirtyObjectMetaMap,
            StargateAllocator networkStates, int metaCnt)
        {
            this.metaCnt = metaCnt;
            this.snapshotTick = Tick.InvalidTick;
            this.worldObjectMeta = (NetworkObjectMeta*)worldObjectMeta;
            this.dirtyObjectMetaMap = dirtyObjectMetaMap;
            this.networkStates = networkStates;
            this.networkStates.FlushZero(this.worldObjectMeta);
            this.networkStates.FlushZero(this.dirtyObjectMetaMap);
            this.Init(Tick.InvalidTick);
        }

        internal void Init(Tick tick)
        {
            this.snapshotTick = tick;
            for (int i = 0; i < this.metaCnt; i++)
            {
                this.dirtyObjectMetaMap[i] = 0;
                this.worldObjectMeta[i].networkId = -1;
                this.worldObjectMeta[i].prefabId = -1;
                this.worldObjectMeta[i].stateWordSize = -1;
                this.worldObjectMeta[i].destroyed = false;
            }
            this.networkStates.FastRelease();
        }

        internal void CopyStateTo(Snapshot dest)
        {
            this.networkStates.CopyTo(dest.networkStates);
        }
    }
}