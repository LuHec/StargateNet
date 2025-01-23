using System;
using System.Collections.Generic;

namespace StargateNet
{
    public unsafe class Snapshot
    {
        public Tick snapshotTick;

        // private int* _poolIdxMap; // TODO:回放功能需要用到，因为缺失了Entity信息，所以需要额外记录。暂时先注释掉
        private int* _dirtyObjectMetaMap; // 标记本帧变化的meta
        private NetworkObjectMeta* _worldObjectMeta; // 存储此帧所有物体的meta                 
        private StargateAllocator _miscAllocator;
        public StargateAllocator NetworkStates { private set; get; } // 存储此帧所有物体的状态，给回放用
        internal readonly int metaCnt;
        private readonly int _metaPoolId;
        private readonly int _dirtyPoolId;
        

        public Snapshot(long stateByteSize, int metaCnt, Monitor monitor)
        {
            long worldMetaByteSize = sizeof(NetworkObjectMeta) * metaCnt;
            long dirtyMapByteSize = sizeof(int) * metaCnt;
            this.metaCnt = metaCnt;
            this.snapshotTick = Tick.InvalidTick;
            this.NetworkStates = new StargateAllocator(stateByteSize, monitor);
            this._miscAllocator = new StargateAllocator(worldMetaByteSize + dirtyMapByteSize, monitor);
            this._miscAllocator.AddPool(worldMetaByteSize, out int metaPoolId);
            this._miscAllocator.AddPool(dirtyMapByteSize, out int dirtyPoolId);
            this._worldObjectMeta = (NetworkObjectMeta*)this._miscAllocator.pools[metaPoolId].dataPtr;
            this._dirtyObjectMetaMap = (int*)this._miscAllocator.pools[dirtyPoolId].dataPtr;
            this._metaPoolId = metaPoolId;
            this._dirtyPoolId = dirtyPoolId;
        }
        
        // public Snapshot(int* worldObjectMeta, int* dirtyObjectMetaMap, StargateAllocator networkStates, int metaCnt)
        // {
        //     this.metaCnt = metaCnt;
        //     this.snapshotTick = Tick.InvalidTick;
        //     this._worldObjectMeta = (NetworkObjectMeta*)worldObjectMeta;
        //     this._dirtyObjectMetaMap = dirtyObjectMetaMap;
        //     this.NetworkStates = networkStates;
        //     this.NetworkStates.FlushZero(this._worldObjectMeta);
        //     this.NetworkStates.FlushZero(this._dirtyObjectMetaMap);
        //     this.Init(Tick.InvalidTick);
        // }

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

        public NetworkObjectMeta GetWorldObjectMeta(int idx)
        {
            return this._worldObjectMeta[idx];
        }

        internal void InvalidateMeta(int idx)
        {
            if (idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            this._worldObjectMeta[idx] = NetworkObjectMeta.Invalid;
        }

        internal void SetMetaDestroyed(int idx, bool destroyed)
        {
            if (idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            this._worldObjectMeta[idx].destroyed = destroyed;
        }

        internal bool IsObjectDestroyed(int idx)
        {
            if (idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            return this._worldObjectMeta[idx].destroyed;
        }

        internal void MarkMetaDirty(int idx)
        {
            if (idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            this._dirtyObjectMetaMap[idx] = 1;
        }

        internal bool IsWorldMetaDirty(int idx)
        {
            if (idx >= this.metaCnt) throw new Exception("meta idx is out of range");
            return this._dirtyObjectMetaMap[idx] == 1;
        }


        /// <summary>
        /// 拷贝状态
        /// </summary>
        /// <param name="dest"></param>
        internal void CopyStateTo(Snapshot dest)
        {
            this.NetworkStates.CopyTo(dest.NetworkStates);
        }

        internal void CopyMetaTo(Snapshot dest)
        {
            for (int i = 0; i < this.metaCnt; i++)
            {
                dest._worldObjectMeta[i] = this._worldObjectMeta[i];
            }
        }

        internal void CopyDirtyMapTo(Snapshot dest)
        {
            for (int i = 0; i < this.metaCnt; i++)
            {
                dest._dirtyObjectMetaMap[i] = this._dirtyObjectMetaMap[i];
            }
        }

        /// <summary>
        /// 将Snapshot整个拷贝到目标
        /// </summary>
        /// <param name="dest"></param>
        internal void CopyTo(Snapshot dest)
        {
            this.CopyDirtyMapTo(dest);
            this.CopyMetaTo(dest);
            this.CopyStateTo(dest);
            dest.snapshotTick = this.snapshotTick;
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