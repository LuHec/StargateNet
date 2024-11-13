using System.Collections.Generic;

namespace StargateNet
{
    public abstract class Simulation
    {
        internal StargateEngine engine;

        internal Dictionary<NetworkObjectRef, Entity>
            EntitiesTable { private set; get; } // 记录当前的Entities，但并不直接执行这些实例

        internal unsafe int* networkRefMap;
        internal List<Entity> paddingToAddEntities = new(32); // 待加入模拟的实体，用于延迟添加到模拟列表。Entity会在这之前就被添加到table中
        internal List<Entity> paddingToRemoveEntities = new(32);
        internal NetworkObjectRef currentMaxNetworkObjectRef = NetworkObjectRef.InvalidNetworkObjectRef;
        protected Queue<SimulationInput> inputPool = new(32);
        protected Queue<Entity> reuseEntities = new(32);


        internal Simulation(StargateEngine engine)
        {
            this.engine = engine;
        }

        internal unsafe Entity CreateEntity(NetworkObject networkObject, NetworkObjectRef networkObjectRef,
            out int stateWordSize)
        {
            // 用全局的内存来分配，在一帧结束后，内存会被拷贝到WorldState中
            StargateAllocator allocator = this.engine.ObjectAllocator;
            NetworkBehavior[] networkBehaviors = networkObject.GetComponents<NetworkBehavior>();
            Entity entity = new Entity(networkObjectRef, this.engine, networkObject);
            int byteSize = 0;
            for (int i = 0; i < networkBehaviors.Length; i++)
            {
                byteSize += networkBehaviors[i].StateBlockSize;
            }

            stateWordSize = byteSize / 4;
            // 给每个脚本切割内存和bitmap
            int wordOffset = 0;
            if (allocator.AddPool(byteSize * 2, out int poolId))
            {
                entity.poolId = poolId;
                int* poolData = (int*)allocator.pools[networkObjectRef.refValue].data;
                int* bitmap = poolData; //bitmap放在首部
                int* state = poolData + byteSize / 4;
                entity.Initialize(state, bitmap, byteSize / 4);
                for (int i = 0; i < networkBehaviors.Length; i++)
                {
                    networkBehaviors[i].StateBlock = state + wordOffset;
                    wordOffset += networkBehaviors[i].StateBlockSize / 4; // 懒得改ilprocessor，所以暂时用字节数
                }
            }

            return entity;
        }

        /// <summary>
        /// 立即添加一个Entity，会触发被动脚本，但下一帧才会执行它的主动网络脚本
        /// </summary>
        /// <param name="networkObject"></param>
        /// <param name="networkId">网络id</param>
        /// <param name="worldIdx">worldMeta的下标</param>
        /// <param name="meta"></param>
        internal unsafe void AddEntity(NetworkObject networkObject, int networkId, int worldIdx, NetworkObjectMeta meta)
        {
            NetworkObjectRef networkObjectRef = new NetworkObjectRef(networkId);
            Entity entity = this.CreateEntity(networkObject, networkObjectRef, out int stateWordSize);
            this.EntitiesTable.Add(networkObjectRef, entity);
            this.paddingToAddEntities.Add(entity);
            // 修改meta并标记
            meta.networkId = networkObjectRef.refValue;
            meta.stateWordSize = stateWordSize;
            meta.prefabId = networkObject.PrefabId;
            meta.destroyed = false;
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            currentSnapshot.worldObjectMeta[networkObjectRef.refValue] = meta;
            currentSnapshot.dirtyObjectMetaMap[networkObjectRef.refValue] = 1;
        }

        /// <summary>
        /// 立即删除一个Entity,后续主动/被动网络脚本不会被执行(主动脚本控制不执行，被动脚本由于物体已被销毁，也不会执行)
        /// </summary>
        /// <param name="networkObjectRef"></param>
        internal unsafe void RemoveEntity(NetworkObjectRef networkObjectRef)
        {
            Entity entity = this.EntitiesTable[networkObjectRef];
            this.EntitiesTable.Remove(networkObjectRef);
            this.paddingToRemoveEntities.Add(entity);
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            // 修改meta并标记
            currentSnapshot.worldObjectMeta[networkObjectRef.refValue].destroyed = true;
            currentSnapshot.dirtyObjectMetaMap[networkObjectRef.refValue] = 1;
        }

        /// <summary>
        /// 主要作用是添加模拟脚本
        /// </summary>
        internal void DrainPaddingAddedEntity()
        {
            foreach (var entity in this.paddingToAddEntities)
            {
                this.engine.IM.simulationList.Add(entity);
            }

            this.paddingToAddEntities.Clear();
        }

        /// <summary>
        /// 主要作用是移除模拟脚本并回收Entity的内存
        /// </summary>
        internal void DrainPaddingRemovedEntity()
        {
            foreach (var entity in this.paddingToRemoveEntities)
            {
                this.engine.ObjectAllocator.ReleasePool(entity.poolId); // 内存归还
                this.engine.IM.simulationList.Remove(entity);
                if (this.engine.IsServer) // worldIdx归还
                {
                    this.engine.EntityMetaManager.ReturnWorldIdx(entity.worldMetaId);
                }
                entity.Reset();
            }

            this.paddingToRemoveEntities.Clear();
        }

        internal virtual void PreUpdate()
        {
        }

        internal virtual void PreFixedUpdate()
        {
        }

        internal virtual void PostFixedUpdate()
        {
        }

        /// <summary>
        /// Simulate world in fixed update
        /// </summary>
        internal void FixedUpdate()
        {
            // 对于客户端，先在这里处理回滚，然后再模拟下一帧
            this.ExecuteNetworkFixedUpdate();
        }

        internal void ExecuteNetworkUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkUpdate();
        }

        internal void ExecuteNetworkRender()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkRender();
        }

        internal void ExecuteNetworkFixedUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkFixedUpdate();
        }

        internal SimulationInput CreateInput(Tick srvTick, Tick targetTick)
        {
            if (inputPool.Count == 0)
            {
                inputPool.Enqueue(new SimulationInput());
            }

            SimulationInput resInput = inputPool.Dequeue();
            resInput.srvTick = srvTick;
            resInput.targetTick = targetTick;
            return resInput;
        }

        internal void RecycleInput(SimulationInput input)
        {
            this.inputPool.Enqueue(input);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="networkEntity"></param>
        internal virtual void AddToSimulation(INetworkEntity networkEntity)
        {
        }
    }
}