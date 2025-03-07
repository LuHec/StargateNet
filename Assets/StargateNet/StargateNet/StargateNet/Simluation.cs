using System;
using System.Collections.Generic;
using Riptide;
using Riptide.Utils;
using UnityEngine;

namespace StargateNet
{
    public abstract class Simulation
    {
        internal StargateEngine engine;
        internal Snapshot fromSnapshot;
        internal Snapshot toSnapshot;
        internal Dictionary<string, int> typeNameToTypeTable;
        internal Dictionary<int, InputBlock> typeToInputBlockTable;
        internal Dictionary<NetworkObjectRef, Entity> entitiesTable; // 记录当前的Entities，但并不直接执行这些实例
        internal List<Entity> entities; // 用于存储此帧所有的entities，ds不需要这个信息，回放模式可以通过meta还原，延迟补偿不会用到已经删除的实体
        internal List<Entity> paddingToAddEntities = new(32); // 待加入模拟的实体，用于延迟添加到模拟列表。Entity会在这之前就被添加到table中
        internal List<Entity> paddingToAddEntitiesTemp = new(32);
        internal List<Entity> paddingToRemoveEntities = new(32);
        internal NetworkObjectRef currentMaxNetworkObjectRef = NetworkObjectRef.InvalidNetworkObjectRef;
        internal SimulationInput currentInput;
        protected Queue<SimulationInput> inputPool = new(256);
        protected Queue<Entity> reuseEntities = new(32);
        internal Snapshot previousState;
        protected HashSet<CallbackData> remoteCallbacks = new(2048);
        private StargateAllocator _inputAllocator;
        

        internal unsafe Simulation(StargateEngine engine)
        {
            this.engine = engine;
            this.entities = new List<Entity>(engine.maxEntities);
            this.entitiesTable = new(engine.maxEntities);
            for (int i = 0; i < engine.maxEntities; ++i)
            {
                this.entities.Add(null);
            }

            // 读入输入的类型，初始化查找表和内存
            int inputTotalBytes = 0;
            StargateConfigData configData = engine.ConfigData;
            int inputTypeCount = configData.networkInputsTypes.Count;
            this.typeNameToTypeTable = new Dictionary<string, int>(inputTypeCount);
            this.typeToInputBlockTable = new Dictionary<int, InputBlock>(inputTypeCount);
            for (int i = 0; i < inputTypeCount; i++)
            {
                this.typeNameToTypeTable[configData.networkInputsTypes[i]] = i;
                inputTotalBytes += configData.networkInputsBytes[i];
            }

            inputTotalBytes *= (configData.maxPredictedTicks + 10);
            inputTotalBytes = this.engine.IsClient ? inputTotalBytes * 2 : inputTotalBytes * (configData.maxClientCount + 1);
            this._inputAllocator = new StargateAllocator(inputTotalBytes, engine.Monitor);
            for (int i = 0; i < inputTypeCount; i++)
            {
                this.typeToInputBlockTable[i] = AllocateInputBlock(configData.networkInputsBytes[i], i);
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
            if(this.engine.IsClient)
            {
                this.engine.ClientSimulation.ClientControlledEntity = entity.NetworkId;
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
        /// <param name="inputSource">输入源</param>
        internal unsafe void AddEntity(NetworkObject networkObject, int networkId, int worldIdx, int inputSource)
        {
            NetworkObjectRef networkObjectRef = new NetworkObjectRef(networkId);
            Entity entity = this.CreateEntity(networkObject, networkObjectRef, worldIdx, inputSource, out int stateWordSize);
            networkObject.Initialize(this.engine, entity, networkObject.GetComponentsInChildren<IStargateNetworkScript>());
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
            if(this.engine.IsServer && inputSource!=-1)
            {
                this.engine.Server.clientConnections[inputSource].controlEntityRef = networkObjectRef;
            }
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
            }
            var temp = this.paddingToAddEntities;
            this.paddingToAddEntities = this.paddingToAddEntitiesTemp;
            this.paddingToAddEntitiesTemp = temp;
            foreach (var entity in this.paddingToAddEntitiesTemp)
            {
                entity.InitObject();
            }

            this.paddingToAddEntitiesTemp.Clear();
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
                this.engine.IM.RemoveEntity(entity);
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
            this.engine.Monitor.entities = this.engine.IM.Count;
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
        /// DataIdx就是property address - entity base address
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="dataIdx"></param>
        /// <param name="isServer"></param>
        internal unsafe void OnEntityStateRemoteChanged(Entity entity, int dataIdx, bool isServer)
        {
            if (!entity.networkObjectSharedMeta.callbacks.TryGetValue(dataIdx, out CallbackWrapper callbackWrapper))
            {
                return;
            }

            this.remoteCallbacks.Add(new CallbackData()
            {
                Event = callbackWrapper.callbackEvent,
                previousData = (int*)this.previousState.NetworkStates.pools[entity.poolId].dataPtr + entity.entityBlockWordSize + dataIdx,
                offset = 0,
                propertyIdx = callbackWrapper.propertyIndex,
                wordSize = callbackWrapper.propertyWordSize,
                behaviour = entity.entityObject.NetworkScripts[callbackWrapper.behaviorIndex]
            });
        }

        internal unsafe void OnEntityStateChangedLocal(Entity entity, int dataIdx)
        {
            if (!entity.networkObjectSharedMeta.callbacks.TryGetValue(dataIdx, out CallbackWrapper callbackWrapper) || callbackWrapper.invokeDurResim == 0 && this.engine.IsResimulation)
                return;
            if (entity.networkId.refValue != this.previousState.GetWorldObjectMeta(entity.worldMetaId).networkId)
                return;

            if(this.engine.IsClient && !this.engine.ClientSimulation.currentTick.IsValid ) return;
            int* previousData = (int*)this.previousState.NetworkStates.pools[entity.poolId].dataPtr + entity.entityBlockWordSize + dataIdx;
            CallbackData callbackData = new CallbackData()
            {
                Event = callbackWrapper.callbackEvent,
                previousData = previousData,
                offset = 0,
                propertyIdx = callbackWrapper.propertyIndex,
                wordSize = callbackWrapper.propertyWordSize,
                behaviour = entity.entityObject.NetworkScripts[callbackWrapper.behaviorIndex]
            };


            callbackData.Event(callbackData.behaviour, callbackData);
        }

        internal void InvokeRemoteCallbackEvent()
        {
            foreach (CallbackData callbackData in remoteCallbacks)
            {
                callbackData.Event(callbackData.behaviour, callbackData);
            }
        }

        /// <summary>
        /// 在Update期间调整NetworkInput.
        /// </summary>
        internal virtual void SetInput<T>(T networkInput, bool needRefreshAlpha = false) where T : unmanaged, INetworkInput
        {
            string typeName = typeof(T).Name;
            if (!this.typeNameToTypeTable.ContainsKey(typeName)) return;
            int type = this.typeNameToTypeTable[typeName];
            InputBlock inputBlock = this.typeToInputBlockTable[type];
            inputBlock.SetInput(networkInput);
        }

        /// <summary>
        /// 客户端行为，服务端不能使用这个
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        internal T GetInput<T>() where T : unmanaged, INetworkInput
        {
            string typeName = typeof(T).Name;
            T input = default(T);
            InputBlock inputBlock;
            if (this.typeNameToTypeTable.TryGetValue(typeName, out int inputType))
            {
                inputBlock = this.typeToInputBlockTable[inputType];
                input = inputBlock.Get<T>();
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
            foreach (InputBlock template in typeToInputBlockTable.Values)
            {
                InputBlock inputBlock = AllocateInputBlock(template.inputSizeBytes, template.type);
                this.typeToInputBlockTable[template.type].CopyTo(inputBlock);
                resInput.AddInputBlock(inputBlock);
            }

            return resInput;
        }

        internal void RecycleInput(SimulationInput input)
        {
            if (input == null) return;
            for (int i = 0; i < input.inputBlocks.Count; i++)
            {
                FreeInputBlock(input.inputBlocks[i]);
            }

            input.Clear();
            this.inputPool.Enqueue(input);
        }

        internal bool WriteInputBlock(SimulationInput input, int type, Message msg)
        {
            for (int i = 0; i < input.inputBlocks.Count; i++)
            {
                if (type == input.inputBlocks[i].type)
                {
                    InputBlock inputBlock = input.inputBlocks[i];
                    int bytes = this.typeToInputBlockTable[type].inputSizeBytes;
                    for (int byteIdx = 0; byteIdx < bytes; byteIdx++)
                    {
                        inputBlock.CopyByte(byteIdx, msg.GetByte());
                    }
                    
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entity"></param>
        protected virtual void AddToSimulation(Entity entity)
        {
            this.engine.IM.AddEntity(entity);
        }

        private unsafe InputBlock AllocateInputBlock(int bytes, int type)
        {
            InputBlock inputBlock = new InputBlock(
                (byte*)this._inputAllocator.Malloc(bytes),
                bytes,
                type);
            inputBlock.Clear();
            return inputBlock;
        }

        private unsafe void FreeInputBlock(InputBlock inputBlock)
        {
            this._inputAllocator.Free(inputBlock.inputBlockPtr);
        }
    }
}