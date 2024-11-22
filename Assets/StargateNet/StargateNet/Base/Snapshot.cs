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
                this.worldObjectMeta[i] = NetworkObjectMeta.Invalid;
            }
            this.networkStates.FastRelease();
        }

        internal void CopyStateTo(Snapshot dest)
        {
            this.networkStates.CopyTo(dest.networkStates);
        }

        /// <summary>
        /// 将snapshot的dirtymap清理干净，此函数一般会在CurrentSnapshot拷贝完毕，进入新的一帧时调用
        /// </summary>
        internal void CleanMap()
        {
            for (int i = 0; i < this.metaCnt; i++)
            {
                this.dirtyObjectMetaMap[i] = 0;
            }
        }
    }
}