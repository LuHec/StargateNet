using System.Collections.Generic;
using Riptide;

namespace StargateNet
{
    public class ClientConnection
    {
        internal bool connected = false;
        internal ClientData clientData;
        internal Connection connection;
        internal Tick lastAckTick = Tick.InvalidTick;
        internal List<InterestGroup> interestGroup = new(1);
        internal StargateEngine engine;
        private Queue<Snapshot> _cachedSnapshots = new(32);
        private bool[] _cachedDirtyIds;

        public ClientConnection(StargateEngine engine)
        {
            this.engine = engine;
            this._cachedDirtyIds = new bool[engine.ConfigData.maxNetworkObjects];
        }

        public void Reset()
        {
            this.connected = false;
            this.clientData = null;
            this.connection = null;
            this.lastAckTick = Tick.InvalidTick;
        }

        /// <summary>
        /// 服务端默认发上一帧的delta，只有客户端表示丢包了才会发多个差分包
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="cachedMeta"></param>
        /// <param name="isMultiPak"></param>
        public unsafe bool WriteMeta(Message msg, List<int> cachedMeta, bool isMultiPak)
        {
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;
            Snapshot curSnapshot = worldState.CurrentSnapshot;
            bool isFullPak = false;
            if (isMultiPak)
            {
                isFullPak = this.HandleMultiPacketMeta(msg, curSnapshot);
            }
            else
            {
                this.WriteCachedMeta(msg, curSnapshot, cachedMeta);
            }

            msg.AddInt(-1); // meta写入终止符号
            return isFullPak;
        }

        /// <summary>
        /// 处理多包差分元数据逻辑
        /// </summary>
        private bool HandleMultiPacketMeta(Message msg, Snapshot curSnapshot)
        {
            this._cachedSnapshots.Clear();
            int hisTickCount = this.engine.SimTick.tickValue - this.lastAckTick.tickValue;
            bool isMissingTooManyFrames =
                hisTickCount > this.engine.WorldState.HistoryCount || this.clientData.isFirstPak;
            msg.AddBool(isMissingTooManyFrames); // 全量标识

            // 发送全量数据
            if (isMissingTooManyFrames)
            {
                this.WriteFullMeta(msg, curSnapshot);
            }
            else
            {
                // 处理部分丢包逻辑
                while (hisTickCount > 0)
                {
                    Snapshot snapshot = this.engine.WorldState.GetHistoryTick(hisTickCount - 1);
                    if (snapshot == null) break;
                    this._cachedSnapshots.Enqueue(snapshot);
                    hisTickCount--;
                }

                this.WriteDeltaMeta(msg, curSnapshot);
            }

            return isMissingTooManyFrames;
        }

        /// <summary>
        /// 处理全量元数据，只发送没被销毁的物体
        /// </summary>
        private void WriteFullMeta(Message msg, Snapshot curSnapshot)
        {
            for (int id = 0; id < this.engine.ConfigData.maxNetworkObjects; id++)
            {
                NetworkObjectMeta meta = curSnapshot.GetWorldObjectMeta(id);
                if (!meta.destroyed) // 销毁对象无需发送
                {
                    this.AddNetworkObjectMeta(msg, id, meta);
                }
            }
        }

        /// <summary>
        /// 处理差分元数据，发送所有dirty物体。TODO:似乎还能进一步优化？On的复杂度有点烂了
        /// </summary>
        private void WriteDeltaMeta(Message msg, Snapshot curSnapshot)
        {
            while (this._cachedSnapshots.Count > 0)
            {
                Snapshot hisSnapshot = this._cachedSnapshots.Dequeue();
                for (int id = 0; id < this.engine.ConfigData.maxNetworkObjects; id++)
                {
                    this._cachedDirtyIds[id] |= hisSnapshot.IsWorldMetaDirty(id);
                }
            }

            for (int id = 0; id < this.engine.ConfigData.maxNetworkObjects; id++)
            {
                this._cachedDirtyIds[id] |= curSnapshot.IsWorldMetaDirty(id);
                if (this._cachedDirtyIds[id])
                {
                    NetworkObjectMeta meta = curSnapshot.GetWorldObjectMeta(id);
                    AddNetworkObjectMeta(msg, id, meta);
                }
            }
        }

        /// <summary>
        /// 写入缓存元数据
        /// </summary>
        private void WriteCachedMeta(Message msg, Snapshot curSnapshot, List<int> cachedMeta)
        {
            msg.AddBool(false); // 全量标识
            foreach (int id in cachedMeta)
            {
                NetworkObjectMeta meta = curSnapshot.GetWorldObjectMeta(id);
                AddNetworkObjectMeta(msg, id, meta);
            }
        }

        /// <summary>
        /// 添加网络对象元数据
        /// </summary>
        private void AddNetworkObjectMeta(Message msg, int id, NetworkObjectMeta meta)
        {
            msg.AddInt(id);
            msg.AddInt(meta.networkId);
            msg.AddInt(meta.prefabId);
            msg.AddInt(meta.stateWordSize);
            msg.AddBool(meta.destroyed);
        }


        public void WriteState(Message msg, bool isFullPak)
        {
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;
            // 第一次发送全量包，TODO：后续增加AOI
            for (int worldIdx = 0; worldIdx < simulation.entities.Count; worldIdx++)
            {
                Entity entity = simulation.entities[worldIdx];
                if (entity != null && (entity.dirty || isFullPak) &&
                    worldState.CurrentSnapshot != null && !worldState.CurrentSnapshot.IsObjectDestroyed(worldIdx))
                {
                    msg.AddInt(worldIdx);
                    for (int idx = 0; idx < entity.entityBlockWordSize; idx++)
                    {
                        if (!isFullPak && !entity.IsStateDirty(idx)) continue;
                        msg.AddInt(idx);
                        msg.AddInt(entity.GetState(idx));
                    }

                    msg.AddInt(-1); // 单个Entity终止符号
                }
            }

            msg.AddInt(-1); // 状态写入终止符号
        }
    }
}