using System.Collections.Generic;
using Riptide;
using UnityEngine;

namespace StargateNet
{
    public class ClientConnection
    {
        internal bool connected = false;
        internal ClientData clientData;
        internal Connection connection;
        internal Tick lastAckTick = Tick.InvalidTick;
        internal StargateEngine engine;
        internal NetworkObjectRef controlEntityRef;
        internal HashSet<NetworkObjectRef> lastVisibleObjects = new(256); // 上一帧可见的对象
        internal HashSet<NetworkObjectRef> currentVisibleObjects = new(256); // 当前视野内的对象
        private const int INTEREST_RADIUS = 2; // AOI半径，以区块为单位
        private List<Snapshot> _cachedSnapshots = new(32); // 用于存放过去的Snapshot，辅助MultiPak
        private List<bool> _cachedDirtyMetaIds; // 用于存放dirty的metaId，辅助MultiPak
        private HashSet<int> _cachedDirtyStateIds = new(128); // 用于存放Entity过去dirty的stateId，辅助MultiPak
        

        public ClientConnection(StargateEngine engine)
        {
            this.engine = engine;
            this._cachedDirtyMetaIds = new(engine.ConfigData.maxNetworkObjects);
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
            this.currentVisibleObjects.Clear();
            this.lastVisibleObjects.Clear();
        }

        public void PrepareToWrite()
        {
            this._cachedSnapshots.Clear();
            this._cachedDirtyStateIds.Clear();
            for (int i = 0; i < this._cachedDirtyMetaIds.Count; i++)
            {
                this._cachedDirtyMetaIds[i] = false;
            }
            // 交换可见对象列表
            (lastVisibleObjects, currentVisibleObjects) = (currentVisibleObjects, lastVisibleObjects);
            currentVisibleObjects.Clear();
        }

        /// <summary>
        /// 服务端默认发上一帧的delta，只有客户端表示丢包了才会发多个差分包.
        /// 不能用ClientTick == -1来判断是否是第一个包，因为有滞后性。这里默认客户端收到了首个包，如果后续有丢包，那客户端会通知服务端发全量包
        /// </summary>
        /// <param name="writeBuffer"></param>
        /// <param name="cachedMeta"></param>
        /// <param name="isMultiPak"></param>
        public unsafe void WriteMeta(ReadWriteBuffer writeBuffer, bool isMultiPak, List<int> cachedMeta)
        {
            WorldState worldState = this.engine.WorldState;
            Snapshot curSnapshot = worldState.CurrentSnapshot;

            if (isMultiPak)
            {
                int hisTickCount = this.engine.SimTick.tickValue - this.lastAckTick.tickValue;
                bool isMissingTooManyFrames =
                    this.clientData.isFirstPak || hisTickCount > this.engine.WorldState.HistoryCount;
                writeBuffer.AddBool(isMissingTooManyFrames); // 全量标识
                this.HandleMultiPacketMeta(writeBuffer, curSnapshot, isMissingTooManyFrames, hisTickCount);
            }
            else
            {
                writeBuffer.AddBool(false); // 全量标识
                this.WriteCachedMeta(writeBuffer, curSnapshot, cachedMeta);
            }

            writeBuffer.AddInt(-1); // meta写入终止符号
        }

        /// <summary>
        /// 处理多包差分元数据逻辑
        /// </summary>
        private void HandleMultiPacketMeta(ReadWriteBuffer writeBuffer, Snapshot curSnapshot, bool isMissingTooManyFrames,
            int hisTickCount)
        {
            // 发送全量数据
            if (isMissingTooManyFrames)
            {
                this.WriteFullMeta(writeBuffer, curSnapshot);
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

                this.WriteDeltaMeta(writeBuffer, curSnapshot);
            }
        }

        /// <summary>
        /// 处理全量元数据，只发送没被销毁的物体
        /// </summary>
        private void WriteFullMeta(ReadWriteBuffer writeBuffer, Snapshot curSnapshot)
        {
            for (int id = 0; id < this.engine.ConfigData.maxNetworkObjects; id++)
            {
                NetworkObjectMeta meta = curSnapshot.GetWorldObjectMeta(id);
                // 只发送需要同步的对象
                if (ShouldSendObject(new NetworkObjectRef(meta.networkId), meta.destroyed))
                {
                    this.AddNetworkObjectMeta(writeBuffer, id, meta);
                }
            }
        }

        /// <summary>
        /// 处理差分元数据，发送所有dirty物体。
        /// </summary>
        private void WriteDeltaMeta(ReadWriteBuffer writeBuffer, Snapshot curSnapshot)
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
                    // 只发送需要同步的对象
                    if (ShouldSendObject(new NetworkObjectRef(meta.networkId), meta.destroyed))
                    {
                        AddNetworkObjectMeta(writeBuffer, id, meta);
                    }
                }
            }
        }

        /// <summary>
        /// 写入缓存元数据
        /// </summary>
        private void WriteCachedMeta(ReadWriteBuffer writeBuffer, Snapshot curSnapshot, List<int> cachedMeta)
        {
            foreach (int id in cachedMeta)
            {
                NetworkObjectMeta meta = curSnapshot.GetWorldObjectMeta(id);
                // 只发送需要同步的对象
                if (ShouldSendObject(new NetworkObjectRef(meta.networkId), meta.destroyed))
                {
                    AddNetworkObjectMeta(writeBuffer, id, meta);
                }
            }
        }

        /// <summary>
        /// 添加网络对象元数据
        /// </summary>
        private void AddNetworkObjectMeta(ReadWriteBuffer readWriteBuffer, int id, NetworkObjectMeta meta)
        {
            readWriteBuffer.AddInt(id);
            readWriteBuffer.AddInt(meta.networkId);
            readWriteBuffer.AddInt(meta.prefabId);
            readWriteBuffer.AddInt(meta.inputSource);
            readWriteBuffer.AddBool(meta.destroyed);
        }

        /// <summary>
        /// 写入Entity的状态，TODO：增加AOI
        /// </summary>
        /// <param name="writeBuffer"></param>
        /// <param name="isMultiPak"></param>
        public void WriteState(ReadWriteBuffer writeBuffer, bool isMultiPak)
        {
            WorldState worldState = this.engine.WorldState;
            Simulation simulation = this.engine.Simulation;

            if (isMultiPak)
            {
                int hisTickCount = this.engine.SimTick.tickValue - this.lastAckTick.tickValue;
                bool isMissingTooManyFrames =
                    this.clientData.isFirstPak || hisTickCount > this.engine.WorldState.HistoryCount;
                this.HandleMultiPacketState(writeBuffer, isMissingTooManyFrames);
            }
            else
            {
                for (int worldIdx = 0; worldIdx < simulation.entities.Count; worldIdx++)
                {
                    Entity entity = simulation.entities[worldIdx];
                    if (entity != null && entity.dirty && !worldState.CurrentSnapshot.IsObjectDestroyed(worldIdx))
                    {
                        // 只发送在AOI范围内的对象状态
                        if (ShouldSendObject(entity.networkId, false))
                        {
                            writeBuffer.AddInt(worldIdx);
                            for (int idx = 0; idx < entity.entityBlockWordSize; idx++)
                            {
                                if (!entity.IsStateDirty(idx)) continue;
                                writeBuffer.AddInt(idx);
                                writeBuffer.AddInt(entity.GetState(idx));
                            }

                            writeBuffer.AddInt(-1); // 单个Entity终止符号
                        }
                    }
                }
            }

            writeBuffer.AddInt(-1); // 状态写入终止符号
        }

        /// <summary>
        /// 处理多帧Snapshot和全量Snapshot的状态写入
        /// </summary>
        /// <param name="writeBuffer"></param>
        /// <param name="isMissingTooManyFrames"></param>
        private void HandleMultiPacketState(ReadWriteBuffer writeBuffer, bool isMissingTooManyFrames)
        {
            if (isMissingTooManyFrames)
            {
                this.WriteFullState(writeBuffer);
            }
            else
            {
                this.WriteDeltaState(writeBuffer);
            }
        }

        /// <summary>
        /// 写入所有状态，不管有没有dirty
        /// </summary>
        /// <param name="writeBuffer"></param>
        private void WriteFullState(ReadWriteBuffer writeBuffer)
        {
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;

            for (int worldIdx = 0; worldIdx < simulation.entities.Count; worldIdx++)
            {
                Entity entity = simulation.entities[worldIdx];
                if (entity != null && !worldState.CurrentSnapshot.IsObjectDestroyed(worldIdx))
                {
                    writeBuffer.AddInt(worldIdx);
                    for (int idx = 0; idx < entity.entityBlockWordSize; idx++)
                    {
                        writeBuffer.AddInt(idx);
                        writeBuffer.AddInt(entity.GetState(idx));
                    }

                    writeBuffer.AddInt(-1); // 单个Entity终止符号
                }
            }
        }

        /// <summary>
        /// 写入多帧的状态，只要过去这个Entity发生了变化，那就会被写入
        /// </summary>
        /// <param name="writeBuffer"></param>
        private void WriteDeltaState(ReadWriteBuffer writeBuffer)
        {
            Simulation simulation = this.engine.Simulation;
            WorldState worldState = this.engine.WorldState;

            for (int worldIdx = 0; worldIdx < simulation.entities.Count; worldIdx++)
            {
                Entity entity = simulation.entities[worldIdx];
                if (entity != null && !worldState.CurrentSnapshot.IsObjectDestroyed(worldIdx))
                {
                    writeBuffer.AddInt(worldIdx);
                    this.WriteEntityDeltaState(writeBuffer, entity, worldIdx);
                    writeBuffer.AddInt(-1); // 单个Entity终止符号
                }
            }
        }

        /// <summary>
        /// 写入单个Entity的状态，只要过去Entity发生了变化，那就会被写入
        /// </summary>
        /// <param name="writeBuffer"></param>
        /// <param name="entity"></param>
        /// <param name="worldMetaId"></param>
        private unsafe void WriteEntityDeltaState(ReadWriteBuffer writeBuffer, Entity entity, int worldMetaId)
        {
            this._cachedDirtyStateIds.Clear();

            for (int stateId = 0; stateId < entity.entityBlockWordSize; stateId++)
            {
                if (entity.IsStateDirty(stateId))
                {
                    this._cachedDirtyStateIds.Add(stateId);
                }
            }

            for (int i = this._cachedSnapshots.Count - 1; i >= 0; i--) // 找到最后一个Entity所在的历史Snapshot
            {
                Snapshot snapshot = this._cachedSnapshots[i];
                NetworkObjectMeta meta = snapshot.GetWorldObjectMeta(worldMetaId);
                if (meta.networkId != entity.networkId.refValue) break;
                StargateAllocator.MemoryPool statePool = snapshot.NetworkStates.pools[entity.poolId];
                int* poolData = (int*)statePool.dataPtr;
                int* bitmap = poolData; //bitmap放在首部

                for (int stateId = 0; stateId < entity.entityBlockWordSize; stateId++)
                {
                    if (bitmap[stateId] == 1)
                    {
                        this._cachedDirtyStateIds.Add(stateId);
                    }
                }
            }

            foreach (var stateId in this._cachedDirtyStateIds)
            {
                writeBuffer.AddInt(stateId);
                writeBuffer.AddInt(entity.GetState(stateId));
            }
        }

        /// <summary>
        /// 计算当前客户端的可见对象
        /// </summary>
        internal void CalculateVisibleObjects(InterestManager im, Entity playerEntity)
        {
            // 保存上一帧的可见对象列表
            lastVisibleObjects.Clear();
            foreach (var obj in currentVisibleObjects)
            {
                lastVisibleObjects.Add(obj);
            }
            currentVisibleObjects.Clear();

            if (playerEntity == null) return;

            // 计算玩家所在的区块
            Transform transform = playerEntity.entityObject.transform;
            InterestBlock playerBlock = new InterestBlock
            {
                xIndex = (int)Mathf.Floor(transform.position.x / InterestManager.boundX),
                yIndex = (int)Mathf.Floor(transform.position.y / InterestManager.boundY),
                zIndex = (int)Mathf.Floor(transform.position.z / InterestManager.boundZ)
            };

            // 遍历周围区块
            for (int x = -INTEREST_RADIUS; x <= INTEREST_RADIUS; x++)
            {
                for (int y = -INTEREST_RADIUS; y <= INTEREST_RADIUS; y++)
                {
                    for (int z = -INTEREST_RADIUS; z <= INTEREST_RADIUS; z++)
                    {
                        InterestBlock checkBlock = new InterestBlock
                        {
                            xIndex = playerBlock.xIndex + x,
                            yIndex = playerBlock.yIndex + y,
                            zIndex = playerBlock.zIndex + z
                        };

                        if (im.interestBlockMap.TryGetValue(checkBlock, out var entityList))
                        {
                            foreach (var netId in entityList)
                            {
                                currentVisibleObjects.Add(netId);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 判断对象是否需要发送到客户端
        /// </summary>
        internal bool ShouldSendObject(NetworkObjectRef networkId)
        {
            // 新进入视野的物体
            if (currentVisibleObjects.Contains(networkId) && !lastVisibleObjects.Contains(networkId))
                return true;

            // 已经在视野中的物体
            if (currentVisibleObjects.Contains(networkId))
                return true;

            // 刚离开视野的物体也需要发送一次（用于清理）
            if (!currentVisibleObjects.Contains(networkId) && lastVisibleObjects.Contains(networkId))
                return true;

            return false;
        }

        /// <summary> 
        /// 判断对象是否需要发送到客户端
        /// </summary>
        private bool ShouldSendObject(NetworkObjectRef networkId, bool isDestroyed)
        {
            // 已销毁的对象，如果之前在视野内也需要发送（通知客户端销毁）
            if (isDestroyed)
                return lastVisibleObjects.Contains(networkId);

            // 新进入视野的物体
            if (currentVisibleObjects.Contains(networkId) && !lastVisibleObjects.Contains(networkId))
                return true;

            // 已经在视野中的物体
            if (currentVisibleObjects.Contains(networkId))
                return true;

            // 刚离开视野的物体也需要发送一次（用于客户端处理）
            if (!currentVisibleObjects.Contains(networkId) && lastVisibleObjects.Contains(networkId))
                return true;

            return false;
        }
    }
}