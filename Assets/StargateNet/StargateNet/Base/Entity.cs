using System;
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
        internal NetworkObjectRef networkId; // 客户端服务端一定是一致的
        internal int poolId = -1; // 内存的idx，客户端和服务端不一定一致
        internal int worldMetaId = -1; // meta的idx，客户端服务端一定是一致的
        internal int inputSource = -1;
        internal StargateEngine engine;
        internal NetworkObject entityObject; // Truly Object
        internal NetworkObjectSharedMeta networkObjectSharedMeta; // 存储回调函数
        internal int entityBlockWordSize; // Networked Field Size, 不包括bitmap大小(两者大小一致)
        internal bool dirty = false;
        private unsafe int* _stateBlock; // Networked Field memory block base address
        private unsafe int* _dirtyMap; // bit dirtymap

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
            this._stateBlock = stateBlockPtr;
            this._dirtyMap = bitmapPtr;
            this.entityBlockWordSize = blockWordSize;
            this.poolId = poolId;
            this.worldMetaId = worldMetaId;
            this.inputSource = inputSource;
            int wordOffset = 0;
            for (int i = 0; i < networkBehaviors.Length; i++)
            {
                networkBehaviors[i].StateBlock = this._stateBlock + wordOffset;
                wordOffset += networkBehaviors[i].StateBlockSize / 4; // 懒得改ilprocessor，所以暂时用字节数
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
                this._dirtyMap[i] = 0;
            }
        }

        internal unsafe void Reset()
        {
            this._stateBlock = null;
            this._dirtyMap = null;
            this.entityBlockWordSize = 0;
            this.poolId = -1;
            this.worldMetaId = -1;
            this.networkId = NetworkObjectRef.InvalidNetworkObjectRef;
        }

        internal unsafe int GetState(int idx)
        {
            if (idx >= entityBlockWordSize) throw new Exception("State idx is out of range");
            return this._stateBlock[idx];
        }

        internal unsafe long GetStateBlockIdx(int* stateBlockPtr)
        {
            long idx = this._stateBlock - stateBlockPtr;
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
            this._stateBlock[idx] = stateData;
        }

        internal unsafe bool IsStateDirty(int idx)
        {
            if (idx >= entityBlockWordSize) throw new Exception("State idx is out of range");
            return this._dirtyMap[idx] == 1;
        }

        /// <summary>
        /// Set data and make bitmap dirty 
        /// </summary>
        /// <param name="newValue"></param>
        /// <param name="address"></param>
        /// <param name="wordSize">一个word长度为4字节</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void SetData(int* newValue, int* address, int wordSize)
        {
            // 内存大小不超过INT_MAX
            int dataId = (int)(address - _stateBlock);

            for (int i = 0; i < wordSize; i++)
            {
                if (_stateBlock[dataId + i] != newValue[i])
                {
                    _stateBlock[dataId + i] = newValue[i];
                    if (this.engine.IsServer)
                    {
                        this.MakeBitmapDirty(dataId + i);
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void MakeBitmapDirty(int dataId)
        {
            this._dirtyMap[dataId] = 1;
            this.dirty = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void DirtifyData(IStargateNetworkScript stargateNetworkScript, int* newValue, int* address,
            int wordSize)
        {
            stargateNetworkScript.Entity.SetData(newValue, address, wordSize);
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
            int propertyIdx = (int)(propertyStart - entity._stateBlock);
            // 以int4为一个块进行存储，这样能让诸如vector3类型的block索引到同一个wrapper
            int key = (int)(propertyPartPtr - entity._stateBlock);
            entity.networkObjectSharedMeta.callbacks.Add(key, new CallbackWrapper(
                invokeDurResim,
                -1,
                propertyIdx,
                propertyWordSize,
                callbackEvent
            ));
            Debug.LogError($"callback test");
        }

        public static unsafe void InternalRest()
        {
        }
    }
}