using System;

namespace StargateNet
{
    public unsafe class Snapshot
    {
        internal Tick snapshotTick;
        private int* _poolIdxMap; // TODO:回放功能需要用到，因为缺失了Entity信息，所以需要额外记录。
        private int* _dirtyObjectMetaMap; // 标记本帧变化的meta
        private NetworkObjectMeta* _worldObjectMeta; // 存储此帧所有物体的meta                 
        internal StargateAllocator NetworkStates { private set; get; } // 存储此帧所有物体的状态，给回放用
        internal readonly int metaCnt;

        public Snapshot(int* worldObjectMeta, int* dirtyObjectMetaMap,
            StargateAllocator networkStates, int metaCnt)
        {
            this.metaCnt = metaCnt;
            this.snapshotTick = Tick.InvalidTick;
            this._worldObjectMeta = (NetworkObjectMeta*)worldObjectMeta;
            this._dirtyObjectMetaMap = dirtyObjectMetaMap;
            this.NetworkStates = networkStates;
            this.NetworkStates.FlushZero(this._worldObjectMeta);
            this.NetworkStates.FlushZero(this._dirtyObjectMetaMap);
            this.Init(Tick.InvalidTick);
        }

        internal void Init(Tick tick)
        {
            this.snapshotTick = tick;
            for (int i = 0; i < this.metaCnt; i++)
            {
                this._dirtyObjectMetaMap[i] = 0;
                this._worldObjectMeta[i] = NetworkObjectMeta.Invalid;
            }
            this.NetworkStates.FastRelease();
        }

        internal void SetWorldObjectMeta(int idx, NetworkObjectMeta meta)
        {
            this._worldObjectMeta[idx] = meta;
        }

        internal NetworkObjectMeta GetWorldObjectMeta(int idx)
        {
            return this._worldObjectMeta[idx];
        }

        internal void InvalidateMeta(int idx)
        {
            if(idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            this._worldObjectMeta[idx] = NetworkObjectMeta.Invalid;
        }

        internal void SetMetaDestroyed(int idx, bool destroyed)
        {
            if (idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            this._worldObjectMeta[idx].destroyed = destroyed;
        }

        internal bool IsObjectDestroyed(int idx)
        {
            if(idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            return this._worldObjectMeta[idx].destroyed;
        }

        internal void MarkMetaDirty(int idx)
        {
            if(idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            this._dirtyObjectMetaMap[idx] = 1;
        }
        
        internal bool IsWorldMetaDirty(int idx)
        {
            if(idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            return this._dirtyObjectMetaMap[idx] == 1;
        }

        internal void CopyStateTo(Snapshot dest)
        {
            this.NetworkStates.CopyTo(dest.NetworkStates);
        }

        /// <summary>
        /// 将snapshot的dirtymap清理干净，此函数一般会在CurrentSnapshot拷贝完毕，进入新的一帧时调用
        /// </summary>
        internal void CleanMap()
        {
            for (int i = 0; i < this.metaCnt; i++)
            {
                this._dirtyObjectMetaMap[i] = 0;
            }
        }
    }
}