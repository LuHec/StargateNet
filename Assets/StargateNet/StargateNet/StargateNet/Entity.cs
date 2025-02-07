using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    /// A networked object isn't actually a GameObject;
    /// It's merely conceptual, existing to represent an entity within a networking context.
    /// </summary>
    public sealed class Entity
    {
        public NetworkObjectRef networkId; // 客户端服务端一定是一致的
        public int worldMetaId = -1; // meta的idx，客户端服务端一定是一致的
        public int poolId = -1; // 内存的idx，客户端和服务端不一定一致
        public int inputSource = -1;
        public StargateEngine engine;
        internal NetworkObject entityObject; // Truly Object
        internal NetworkObjectSharedMeta networkObjectSharedMeta; // 存储回调函数
        public int entityBlockWordSize; // Networked Field Size, 不包括bitmap大小(两者大小一致)
        internal bool dirty = false;
        internal unsafe int* stateBlock; // 所有脚本属性的基存储地址，不包含bitmap
        internal unsafe int* dirtyMap; // bitmap基地址

        /// <summary>
        /// 初始化脚本等.获取大小等等
        /// </summary>
        /// <param name="networkId"></param>
        /// <param name="engine"></param>
        /// <param name="entityObject">真正的实体对象</param>
        public Entity(NetworkObjectRef networkId, StargateEngine engine, NetworkObject entityObject)
        {
            this.networkId = networkId;
            this.engine = engine;
            this.entityObject = entityObject;
        }

        internal unsafe void Initialize(int* stateBlockPtr, int* bitmapPtr, int blockWordSize, int poolId,
            int worldMetaId, int inputSource, NetworkBehavior[] networkBehaviors)
        {
            this.stateBlock = stateBlockPtr;
            this.dirtyMap = bitmapPtr;
            this.entityBlockWordSize = blockWordSize;
            this.poolId = poolId;
            this.worldMetaId = worldMetaId;
            this.inputSource = inputSource;
            int wordOffset = 0;
            for (int i = 0; i < networkBehaviors.Length; i++)
            {
                networkBehaviors[i].StateBlock = this.stateBlock + wordOffset;
                wordOffset += networkBehaviors[i].StateBlockSize / 4; // 懒得改ilprocessor，所以暂时用字节数
                networkBehaviors[i].Entity = this;
                networkBehaviors[i].ScriptIdx = i;
            }

            BehaviorToHash(networkBehaviors);
        }

        private void BehaviorToHash(NetworkBehavior[] networkBehaviors)
        {
            string str = "";
            for (int i = 0; i < networkBehaviors.Length; i++)
            {
                str += networkBehaviors[i].GetType();
            }

            int hash = str.GetHashCode();
            Dictionary<int, NetworkObjectSharedMeta> networkObjectSharedMetas = this.engine.ReflectionData.NetworkObjectSharedMetas;
            if (!networkObjectSharedMetas.TryGetValue(hash, out NetworkObjectSharedMeta meta))
            {
                meta = new NetworkObjectSharedMeta();
                networkObjectSharedMetas.Add(hash, meta);
            }

            this.networkObjectSharedMeta = meta;
            foreach (var beh in networkBehaviors)
            {
                beh.InternalInit();
            }
        }

        internal void InitObject()
        {
            foreach (var script in entityObject.NetworkScripts)
            {
                script.NetworkStart(this.engine.SgNetworkGalaxy);
            }
        }

        internal bool FetchInput<T>(out T input) where T : INetworkInput
        {
            return this.engine.FetchInput(out input, this.inputSource);
        }

        /// <summary>
        /// 每个Tick后都清理
        /// </summary>
        internal unsafe void TickReset()
        {
            this.dirty = false;
            for (int i = 0; i < entityBlockWordSize; i++)
            {
                this.dirtyMap[i] = 0;
            }
        }

        internal unsafe void Reset()
        {
            this.stateBlock = null;
            this.dirtyMap = null;
            this.entityBlockWordSize = 0;
            this.poolId = -1;
            this.worldMetaId = -1;
            this.networkId = NetworkObjectRef.InvalidNetworkObjectRef;
            this.networkObjectSharedMeta = null;
        }

        internal unsafe int GetState(int idx)
        {
            if (idx >= entityBlockWordSize) throw new Exception("State idx is out of range");
            return this.stateBlock[idx];
        }

        public unsafe long GetStateBlockIdx(int* stateBlockPtr)
        {
            long idx = this.stateBlock - stateBlockPtr;
            if (idx < 0 || idx > entityBlockWordSize) throw new Exception("State block idx is out of range");
            return idx;
        }

        /// <summary>
        /// 客户端专用拷贝函数
        /// </summary>
        /// <param name="idx"></param>
        /// <param name="stateData"></param>
        /// <exception cref="Exception"></exception>
        internal unsafe void SetState(int idx, int stateData)
        {
            if (idx >= entityBlockWordSize) throw new Exception("State idx is out of range");
            this.stateBlock[idx] = stateData;
        }

        internal unsafe bool IsStateDirty(int idx)
        {
            if (idx >= entityBlockWordSize) throw new Exception("State idx is out of range");
            return this.dirtyMap[idx] == 1;
        }

        /// <summary>
        /// 设置数据并标记bitmap。这里SetData只会完整的覆盖数据，不会出现部分字节覆盖的情况
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="address"></param>
        /// <param name="wordSize">一个word长度为4字节</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SetData(int* newValue, int* address, int wordSize)
        {
            // 内存大小不超过INT_MAX
            int dataId = (int)(address - this.stateBlock);
            bool hasChanged = false;
            for (int i = 0; i < wordSize; i++)
            {
                if (stateBlock[dataId + i] != newValue[i])
                {
                    stateBlock[dataId + i] = newValue[i];
                    if (this.engine.IsServer)
                    {
                        this.MakeBitmapDirty(dataId + i);
                    }

                    hasChanged = true;
                }
            }

            if (hasChanged)
            {
                this.engine.Simulation.OnEntityStateChangedLocal(this, dataId);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void MakeBitmapDirty(int dataId)
        {
            this.dirtyMap[dataId] = 1;
            this.dirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DirtifyData(IStargateNetworkScript stargateNetworkScript, int* newValue, int* address,
            int wordSize)
        {
            Entity entity = stargateNetworkScript.Entity;
            entity.SetData(newValue, address, wordSize);
        }

        /// <summary>
        /// ILProcessor使用，用于插入到Entity初始化时.
        /// tips：不要改名字，IL那边用字符串的
        /// </summary>
        /// <param name="stargateNetworkScript"></param>
        /// <param name="invokeDurResim"></param>
        /// <param name="propertyStart">整个属性的地址</param>
        /// <param name="propertyPartPtr">属性一部分的地址，这个一般在Vector3这类使用，存储方式是4个字节为一个单位</param>
        /// <param name="propertyWordSize"></param>
        /// <param name="callbackEvent"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void InternalRegisterCallback(
            IStargateNetworkScript stargateNetworkScript,
            int invokeDurResim,
            int* propertyStart,
            int* propertyPartPtr,
            int propertyWordSize,
            CallbackEvent callbackEvent
        )
        {
            Entity entity = stargateNetworkScript.Entity;
            int propertyIdx = (int)(propertyStart - entity.stateBlock);
            // 以int4为一个块进行存储，这样能让诸如vector3类型的block索引到同一个wrapper
            int key = (int)(propertyPartPtr - entity.stateBlock);
            entity.networkObjectSharedMeta.callbacks.TryAdd(key, new CallbackWrapper(
                invokeDurResim,
                stargateNetworkScript.ScriptIdx,
                propertyIdx,
                propertyWordSize,
                callbackEvent
            ));
        }

        public static unsafe void InternalRest()
        {
        }
    }
}