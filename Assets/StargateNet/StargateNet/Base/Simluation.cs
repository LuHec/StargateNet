using System;
using System.Collections.Generic;
using Riptide.Utils;

namespace StargateNet
{
    public abstract class Simulation
    {
        internal StargateEngine engine;

        /// <summary>
        /// 在CS都是WorldState.CurrentState
        /// </summary>
        internal Snapshot fromSnapshot;

        internal Snapshot toSnapshot;
        internal Dictionary<short, ClientInput> clientInputs = new(256); // 存放输入，服务端存放所有InputSource的输入，客户端只存自己的输入
        internal Dictionary<NetworkObjectRef, Entity> entitiesTable; // 记录当前的Entities，但并不直接执行这些实例
        internal List<Entity> entities; // 用于存储此帧所有的entities，ds不需要这个信息，回放模式可以通过meta还原，延迟补偿不会用到已经删除的实体
        internal List<Entity> paddingToAddEntities = new(32); // 待加入模拟的实体，用于延迟添加到模拟列表。Entity会在这之前就被添加到table中
        internal List<Entity> paddingToRemoveEntities = new(32);
        internal NetworkObjectRef currentMaxNetworkObjectRef = NetworkObjectRef.InvalidNetworkObjectRef;
        internal SimulationInput currentInput;
        protected Queue<SimulationInput> inputPool = new(256);
        protected Queue<Entity> reuseEntities = new(32);

        internal Simulation(StargateEngine engine)
        {
            this.engine = engine;
            this.entities = new List<Entity>(engine.maxEntities);
            this.entitiesTable = new(engine.maxEntities);
            for (int i = 0; i < engine.maxEntities; ++i)
            {
                this.entities.Add(null);
            }
        }

        internal virtual void HandledRelease()
        {
        }

        private unsafe Entity CreateEntity(NetworkObject networkObject, NetworkObjectRef networkObjectRef,
            int worldIdx, int inputSource, out int stateWordSize)
        {
            // 用全局的内存来分配，在一帧结束后，内存会被拷贝到WorldState中
            StargateAllocator stateAllocator = this.engine.WorldState.CurrentSnapshot.NetworkStates;
            NetworkBehavior[] networkBehaviors = networkObject.GetComponents<NetworkBehavior>();
            Entity entity = new Entity(networkObjectRef, this.engine, networkObject);
            int byteSize = 0;
            for (int i = 0; i < networkBehaviors.Length; i++)
            {
                byteSize += networkBehaviors[i].StateBlockSize;
            }

            stateWordSize = byteSize / 4;
            // 给每个脚本切割内存和bitmap
            stateAllocator.AddPool(byteSize * 2, out int poolId);
            int* poolData = (int*)stateAllocator.pools[poolId].dataPtr;
            int* bitmap = poolData; //bitmap放在首部
            int* state = poolData + stateWordSize;
            entity.Initialize(state, bitmap, stateWordSize, poolId, worldIdx, inputSource, networkBehaviors);

            return entity;
        }

        /// <summary>
        /// 立即添加一个Entity，会触发被动脚本，但下一帧才会执行它的主动网络脚本
        /// </summary>
        /// <param name="networkObject"></param>
        /// <param name="networkId">网络id</param>
        /// <param name="worldIdx">worldMeta的下标</param>
        /// <param name="meta"></param>
        /// <param name="inputSource">输入源</param>
        internal unsafe void AddEntity(NetworkObject networkObject, int networkId, int worldIdx, int inputSource)
        {
            NetworkObjectRef networkObjectRef = new NetworkObjectRef(networkId);
            Entity entity = this.CreateEntity(networkObject, networkObjectRef, worldIdx, inputSource,
                out int stateWordSize);
            networkObject.Initialize(this.engine, entity, networkObject.GetComponentsInChildren<IStargateScript>());
            this.entitiesTable.Add(networkObjectRef, entity);
            this.paddingToAddEntities.Add(entity);
            // 修改meta并标记
            NetworkObjectMeta meta = new NetworkObjectMeta
            {
                networkId = networkObjectRef.refValue,
                inputSource = inputSource,
                prefabId = networkObject.PrefabId,
                destroyed = false
            };
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;

            currentSnapshot.SetWorldObjectMeta(worldIdx, meta);
            currentSnapshot.MarkMetaDirty(worldIdx);
        }

        /// <summary>
        /// 立即删除一个Entity,后续主动/被动网络脚本不会被执行(主动脚本控制不执行，被动脚本由于物体已被销毁，也不会执行)
        /// </summary>
        /// <param name="networkObjectRef"></param>
        internal unsafe void RemoveEntity(NetworkObjectRef networkObjectRef)
        {
            Entity entity = this.entitiesTable[networkObjectRef];
            if (entity == null) return;
            this.entitiesTable.Remove(networkObjectRef);
            this.paddingToRemoveEntities.Add(entity);
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            // 修改meta并标记
            currentSnapshot.SetMetaDestroyed(entity.worldMetaId, true);
            currentSnapshot.MarkMetaDirty(entity.worldMetaId);
        }

        /// <summary>
        /// 主要作用是添加模拟脚本
        /// </summary>
        internal void DrainPaddingAddedEntity()
        {
            foreach (var entity in this.paddingToAddEntities)
            {
                this.entities[entity.worldMetaId] = entity;
                this.AddToSimulation(entity);
                entity.InitObject();
            }

            this.paddingToAddEntities.Clear();
        }

        /// <summary>
        /// 移除模拟脚本，清理meta并回收Entity的内存，
        /// </summary>
        internal unsafe void DrainPaddingRemovedEntity()
        {
            Snapshot currentSnapshot = this.engine.WorldState.CurrentSnapshot;
            foreach (var entity in this.paddingToRemoveEntities)
            {
                currentSnapshot.InvalidateMeta(entity.worldMetaId);
                this.engine.WorldState.CurrentSnapshot.NetworkStates.ReleasePool(entity.poolId); // 内存归还
                this.engine.IM.simulationList.Remove(entity);
                this.entities[entity.worldMetaId] = null;
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

        internal void SerializeToNetcode()
        {
            this.SyncPhysicTransform();
            this.engine.IM.SerializeToNetcode();
        }

        internal void DeserializeToGamecode()
        {
            this.engine.IM.DeserializeToGameCode();
        }

        /// <summary>
        /// Simulate world in fixed update
        /// </summary>
        internal void FixedUpdate()
        {
            this.ExecuteNetworkFixedUpdate();
            this.engine.Monitor.entities = this.engine.IM.simulationList.Count;
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
            this.engine.PhysicSimulationUpdate.Simulate(this.engine.SimulationClock.FixedDeltaTime);
        }

        /// <summary>
        /// 同步物理
        /// </summary>
        internal void SyncPhysicTransform()
        {
            UnityEngine.Physics.SyncTransforms();
        }

        /// <summary>
        /// 在Update期间调整NetworkInput.需要注意这里InputSource是假参数！！！！字典的key是input的类型！！！！
        /// </summary>
        internal void SetInput(int inputSource, INetworkInput networkInput, bool needRefreshAlpha = false)
        {
            ClientInput clientInput = new ClientInput() { networkInput = networkInput, alpha = this.engine.InterpolationRemote.Alpha, remoteFromTick = this.engine.InterpolationRemote.FromTick};
            if (this.clientInputs.ContainsKey(0))
            {
                var oClientInput = this.clientInputs[0];
                // 只在需要时更新延迟补偿的参数
                if (needRefreshAlpha)
                {
                    this.clientInputs[0] = clientInput;
                }
                else
                {
                    oClientInput.networkInput = networkInput;
                    this.clientInputs[0] = oClientInput;
                }
            }
            else
            {
                clientInputs[0] = clientInput;
            }
        }

        internal T GetInput<T>(int type)
        {
            T input = default(T);
            if (this.clientInputs.TryGetValue(0, out var clientInput))
            {
                input = (T)clientInput.networkInput;
            }

            return input;
        }

        internal SimulationInput CreateInput(Tick srvTick, Tick targetTick, float alpha, Tick remoteFromTick)
        {
            if (inputPool.Count == 0)
            {
                inputPool.Enqueue(new SimulationInput());
            }

            SimulationInput resInput = inputPool.Dequeue();
            resInput.Init(srvTick, targetTick, alpha, remoteFromTick);
            
            return resInput;
        }

        internal void RecycleInput(SimulationInput input)
        {
            if (input == null) return;
            input.Clear();
            this.inputPool.Enqueue(input);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        protected virtual void AddToSimulation(Entity entity)
        {
            this.engine.IM.simulationList.Add(entity);
        }
    }
}