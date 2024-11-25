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
        private List<Snapshot> _cachedSnapshots = new(32);
        private List<bool> _cachedDirtyMetaIds;

        public ClientConnection(StargateEngine engine)
        {
            this.engine = engine;
            this._cachedDirtyMetaIds = new (engine.ConfigData.maxNetworkObjects);
            for (int i = 0; i < engine.ConfigData.maxNetworkObjects; i++)
            {
                this._cachedDirtyMetaIds.Add(false);
            }
        }

        public void Reset()
        {
            this.connected = false;
            this.clientData = null;
            this.connection = null;
            this.lastAckTick = Tick.InvalidTick;
        }

        public void PrepareToWrite()
        {
            this._cachedSnapshots.Clear();
            for (int i = 0; i < this._cachedDirtyMetaIds.Count; i++)
            {
                this._cachedDirtyMetaIds[i] = false;
            }
        }

        /// <summary>
        /// 服务端默认发上一帧的delta，只有客户端表示丢包了才会发多个差分包.
        /// 不能用ClientTick == -1来判断是否是第一个包，因为有滞后性。这里默认客户端收到了首个包，如果后续有丢包，那客户端会通知服务端发全量包
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="cachedMeta"></param>
        /// <param name="isMultiPak"></param>
        public unsafe void WriteMeta(Message msg, bool isMultiPak, List<int> cachedMeta)
        {
            WorldState worldState = this.engine.WorldState;
            Snapshot curSnapshot = worldState.CurrentSnapshot;

            if (isMultiPak)
            {
                int hisTickCount = this.engine.SimTick.tickValue - this.lastAckTick.tickValue;
                bool isMissingTooManyFrames =
                    this.clientData.isFirstPak || hisTickCount > this.engine.WorldState.HistoryCount;
                msg.AddBool(isMissingTooManyFrames); // 全量标识
                this.HandleMultiPacketMeta(msg, curSnapshot, isMissingTooManyFrames, hisTickCount);
            }
            else
            {
                msg.AddBool(false); // 全量标识
                this.WriteCachedMeta(msg, curSnapshot, cachedMeta);
            }

            msg.AddInt(-1); // meta写入终止符号
        }

        /// <summary>
        /// 处理多包差分元数据逻辑
        /// </summary>
        private void HandleMultiPacketMeta(Message msg, Snapshot curSnapshot, bool isMissingTooManyFrames,
            int hisTickCount)
        {
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
                    this._cachedSnapshots.Add(snapshot);
                    hisTickCount--;
                }

                this.WriteDeltaMeta(msg, curSnapshot);
            }
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
            foreach (var hisSnapshot in this._cachedSnapshots)
            {
                for (int id = 0; id < this.engine.ConfigData.maxNetworkObjects; id++)
                {
                    this._cachedDirtyMetaIds[id] |= hisSnapshot.IsWorldMetaDirty(id);
                }
            }

            for (int id = 0; id < this.engine.ConfigData.maxNetworkObjects; id++)
            {
                this._cachedDirtyMetaIds[id] |= curSnapshot.IsWorldMetaDirty(id);
                if (this._cachedDirtyMetaIds[id])
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


        public void WriteState(Message msg, bool isMultiPak)
        {
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;
            Snapshot curSnapshot = worldState.CurrentSnapshot;

            this.WriteFullState(msg);
            // if (isMultiPak)
            // {
            //     int hisTickCount = this.engine.SimTick.tickValue - this.lastAckTick.tickValue;
            //     bool isMissingTooManyFrames =
            //         this.clientData.isFirstPak || hisTickCount > this.engine.WorldState.HistoryCount;
            //     this.HandleMultiPacketState(msg, curSnapshot, isMissingTooManyFrames, hisTickCount);
            // }
            // else
            // {
            // }

            msg.AddInt(-1); // 状态写入终止符号
        }

        private void HandleMultiPacketState(Message msg, Snapshot curSnapshot, bool isMissingTooManyFrames,
            int hisTickCount)
        {
            if (isMissingTooManyFrames)
            {
                this.WriteFullState(msg);
            }
            else
            {
                this.WriteDeltaState(msg);
            }
        }

        /// <summary>
        /// 写入所有状态
        /// </summary>
        /// <param name="msg"></param>
        private void WriteFullState(Message msg)
        {
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;

            for (int worldIdx = 0; worldIdx < simulation.entities.Count; worldIdx++)
            {
                Entity entity = simulation.entities[worldIdx];
                if (entity != null && !worldState.CurrentSnapshot.IsObjectDestroyed(worldIdx))
                {
                    msg.AddInt(worldIdx);
                    for (int idx = 0; idx < entity.entityBlockWordSize; idx++)
                    {
                        msg.AddInt(idx);
                        msg.AddInt(entity.GetState(idx));
                    }

                    msg.AddInt(-1); // 单个Entity终止符号
                }
            }
        }

        private void WriteDeltaState(Message msg)
        {
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;

            foreach (var hisSnapshot in this._cachedSnapshots)
            {
                for (int id = 0; id < this.engine.ConfigData.maxNetworkObjects; id++)
                {
                    
                }
            }
            
            
            // 第一次发送全量包，TODO：增加AOI
            for (int worldIdx = 0; worldIdx < simulation.entities.Count; worldIdx++)
            {
                Entity entity = simulation.entities[worldIdx];
                if (entity != null && entity.dirty &&
                    worldState.CurrentSnapshot != null && !worldState.CurrentSnapshot.IsObjectDestroyed(worldIdx))
                {
                    msg.AddInt(worldIdx);
                    for (int idx = 0; idx < entity.entityBlockWordSize; idx++)
                    {
                        if (!entity.IsStateDirty(idx)) continue;
                        msg.AddInt(idx);
                        msg.AddInt(entity.GetState(idx));
                    }

                    msg.AddInt(-1); // 单个Entity终止符号
                }
            }
        }
    }
}