﻿using System;
using System.Collections.Generic;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public class ClientSimulation : Simulation
    {
        internal Tick currentTick = Tick.InvalidTick;
        internal Tick predictedTick = Tick.InvalidTick;
        internal Tick authoritativeTick = Tick.InvalidTick; // 客户端接收到的AuthorTick,服务端Tick从10开始 
        internal Tick serverRcvedClientTick = Tick.InvalidTick;
        internal Snapshot rcvBuffer; // 接收时存放最新的server snapshot
        internal List<StargateAllocator> predictedSnapshots; // 客户端用于预测snapshot，由于客户端不会预测物体的销毁和生成，所以只存属性
        internal List<SimulationInput> inputs = new(128);
        internal double serverInputRcvTimeAvg; // 服务端算出来的input接收平均时间
        internal bool IsResimulation { private set; get; }
        internal NetworkObjectRef ClientControlledEntity { get; set; }
        private readonly int _maxPredictedTicks;
        private List<Entity> _predictedEntities = new(32);
        private List<IClientSimulationCallbacks> _clientSimulationCallbacksList = new(128);
        private Tick clientInterpolationRemoteFromTick;
        private float clientInterpolationAlpha;
        private bool needRollback = false;


        internal ClientSimulation(StargateEngine engine) : base(engine)
        {
            this._maxPredictedTicks = engine.ConfigData.maxPredictedTicks;
            this.predictedSnapshots = new List<StargateAllocator>(this._maxPredictedTicks);
        }

        public void AddClientSimulationCallbacks(IClientSimulationCallbacks callbacks)
        {
            this._clientSimulationCallbacksList.Add(callbacks);
        }

        public void RemoveClientSimulationCallbacks(IClientSimulationCallbacks callbacks)
        {
            this._clientSimulationCallbacksList.Remove(callbacks);
        }

        internal override void HandledRelease()
        {
            base.HandledRelease();
            foreach (var snapshot in this.predictedSnapshots)
            {
                snapshot.HandledRelease();
            }
        }

        /// <summary>
        /// 处理收到的服务端数据包,首个包用AuthorTick = -1去处理
        /// </summary>
        /// <param name="srvTick">服务端 Tick</param>
        /// <param name="srvClientAuthorTick">服务端接收的客户端最新AuthorTick</param>
        /// <param name="srvInputTick">服务端收到的最新input tick</param>
        /// <param name="isMultiPacket">是否是多帧包</param>
        /// <param name="isFullPacket">是否是全量包</param>
        /// <returns>是否接受并处理了该数据包</returns>
        internal bool OnRcvPak(Tick srvTick, Tick srvClientAuthorTick, Tick srvInputTick, bool isMultiPacket, bool isFullPacket)
        {
            this.serverRcvedClientTick = srvInputTick;
            this.needRollback = false;
            // 优先处理全量包
            if (isFullPacket && this.IsValidFullPacket(srvTick))
            {
                this.HandleFullPacket(srvTick);
                this.needRollback = true;
                return true;
            }

            // 检查正常包或多帧包,这里会把首个包丢失的情况也涵盖进去
            if (this.IsValidNormalPacket(srvTick) ||
                this.IsValidMultiPacket(srvTick, srvClientAuthorTick, isMultiPacket))
            {
                this.authoritativeTick = srvTick;
                this.needRollback = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 处理全量包
        /// </summary>
        private void HandleFullPacket(Tick srvTick)
        {
            this.authoritativeTick = srvTick;
            RiptideLogger.Log(LogType.Debug, $"Full packet received. Tick updated to {srvTick.tickValue}.");
        }

        private bool IsValidFullPacket(Tick srvTick)
        {
            return srvTick > this.authoritativeTick;
        }

        /// <summary>
        /// 检查是否是有效的正常包
        /// </summary>
        private bool IsValidNormalPacket(Tick srvTick)
        {
            return srvTick == this.authoritativeTick + 1;
        }

        /// <summary>
        /// 检查是否是有效的多帧包
        /// </summary>
        private bool IsValidMultiPacket(Tick srvTick, Tick srvClientAuthorTick, bool isMultiPacket)
        {
            return isMultiPacket && srvTick >= this.authoritativeTick + 1 && srvClientAuthorTick <= this.authoritativeTick;
        }

        private void InvokeClientOnPreRollBack()
        {
            foreach (var callbacks in this._clientSimulationCallbacksList)
            {
                callbacks?.OnPreRollBack();
            }
            this.remoteCallbacks.Clear();
        }

        private void InvokeClientOnPostResimulation()
        {
            foreach (var callbacks in this._clientSimulationCallbacksList)
            {
                callbacks?.OnPostResimulation();
            }
        }


        /// <summary>
        /// 客户端的模拟,由于玩家输入一般是在渲染帧获取的，所以在逻辑帧开始计算前，要将延迟补偿信息填入
        /// </summary>
        internal override void PreFixedUpdate()
        {
            if (!this.authoritativeTick.IsValid)
            {
                return;
            }

            if (this.engine.SimulationClock.IsFirstCall)
                this.Reconcile();

            this.currentInput = CreateInput(this.authoritativeTick, this.currentTick, this.clientInterpolationAlpha, this.clientInterpolationRemoteFromTick);
            this.inputs.Add(this.currentInput);
            this.clientInterpolationAlpha = 0f;
            this.clientInterpolationRemoteFromTick = Tick.InvalidTick;

            // 关于新输入把旧输入冲掉导致服务端丢失操作的问题：
            // 首先模拟函数的调用时机在Send Input之后
            // Reconcile中限定了客户端的输入范围在AuthorTick到CurrentTick，都是上一帧的数据，已经在Send中被发出。
            // 本帧要发的有效数据应该是AuthorTick + 1到CurrentTick + 1
            while (this.inputs.Count > 0 && this.inputs.Count > this._maxPredictedTicks) // 新输入占位
            {
                RecycleInput(this.inputs[0]);
                this.inputs.RemoveAt(0);
            }
        }

        internal override void PostFixedUpdate()
        {
            this.engine.Monitor.tick = this.currentTick.tickValue;
            this.currentInput = null; // 这里不回收输入！！！currentInput用的东西在列表里，要用来做Resimulation的
            // 清空写入的输入
            foreach (var inputBlock in this.typeToInputBlockTable.Values)
            {
                inputBlock.Clear();
            }
        }

        internal override void PreUpdate()
        {
        }

        /// <summary>
        /// RollBack和Resim
        /// </summary>
        private void Reconcile()
        {
            int delayTickCount = Math.Abs(this.currentTick - this.authoritativeTick);
            this.currentTick = this.authoritativeTick; // currentTick复制authorTick，并从这一帧开始重新模拟
            if (this.currentTick.IsValid && delayTickCount >= this._maxPredictedTicks) // 严重丢包时直接移除所有的操作，因为回滚重模拟已经没有意义了，下一帧则会从头开始模拟
            {
                this.engine.Client.HeavyPakLoss = true;
                this.RemoveAllInputs();
            }

            // 拷贝上一帧的结果用于local插值。只会发生在FirstCall，即插值只会插正常的两帧(非一帧内多次的帧)
            this.engine.WorldState.CurrentSnapshot.CopyTo(this.fromSnapshot);
            if (delayTickCount < this._maxPredictedTicks)
            {
                // 客户端收到的11帧Snapshot，在服务端指的是10帧最终的状态，11帧的初始状态。所以客户端这里第11帧是应该早就被上传了的，这里需要被丢弃。
                this.RemoveAckedInput(this.authoritativeTick - 1); // 移除服务器接收到的输入(即使丢包了也不管，服务器不会重新模拟)
                this.InvokeClientOnPreRollBack();
                Snapshot lastAuthorSnapshot = this.engine.WorldState.FromSnapshot; // 服务端发来的最新Snapshot
                this.engine.WorldState.CurrentSnapshot.CopyTo(this.previousState); // 准备回调数据
                this._predictedEntities.Clear();
                foreach (var entity in this.entities)
                {
                    if (entity != null)
                        this._predictedEntities.Add(entity);
                }

                if (lastAuthorSnapshot != null && lastAuthorSnapshot.NetworkStates.pools.Count > 0) // 回滚
                    this.RollBackGroup(this._predictedEntities, lastAuthorSnapshot);
                this.DeserializeToGamecode();
                this.SyncPhysicTransform();
                this.InvokeRemoteCallbackEvent();
                this.IsResimulation = true;
                for (int i = 0; i < this.inputs.Count; i++)
                {
                    this.currentInput = this.inputs[i];
                    this.ExecuteNetworkFixedUpdate();
                    this.SerializeToNetcode();
                    this.currentTick++;
                }

                this.IsResimulation = false;
                this.InvokeClientOnPostResimulation();
                this.remoteCallbacks.Clear();
                this.needRollback = false;
            }
            else { this.needRollback = true; }

            this.engine.Monitor.resims = this.currentTick - this.authoritativeTick;
            this.engine.Monitor.inputCount = this.inputs.Count;
        }

        private void RemoveAllInputs()
        {
            for (int i = 0; i < this.inputs.Count; i++)
            {
                RecycleInput(this.inputs[i]);
            }

            this.inputs.Clear();
        }

        private void RemoveAckedInput(Tick targetTick)
        {
            if (this.inputs.Count == 0) return;
            if (this.inputs[^1].clientTargetTick <= targetTick)
            {
                RemoveAllInputs();
            }
            else
            {
                while (this.inputs.Count > 0 && this.inputs[0].clientTargetTick <= targetTick)
                {
                    RecycleInput(this.inputs[0]);
                    this.inputs.RemoveAt(0);
                }
            }
        }

        /// <summary>
        /// RollBack保证回滚的目标帧和当前预测帧物体meta一致。因为预测不会消除物体
        /// TODO:AOI的区域划分会只回滚玩家所在sector
        /// </summary>
        private void RollBackGroup(List<Entity> group, Snapshot authorSnapshot)
        {
            foreach (var entity in group)
            {
                this.RollBackEntity(entity, authorSnapshot);
            }
        }

        private unsafe void RollBackEntity(Entity entity, Snapshot authorSnapshot)
        {
            StargateAllocator predictedState = this.engine.WorldState.CurrentSnapshot.NetworkStates;
            StargateAllocator authorState = authorSnapshot.NetworkStates;
            int poolId = entity.poolId;
            int* predictedData = (int*)predictedState.pools[poolId].dataPtr + entity.entityBlockWordSize;
            int* authorData = (int*)authorState.pools[poolId].dataPtr + entity.entityBlockWordSize;
            for (int dataIdx = 0; dataIdx < entity.entityBlockWordSize; dataIdx++) // 回滚数据
            {
                if (predictedData[dataIdx] != authorData[dataIdx])
                {
                    this.OnEntityStateRemoteChanged(entity, dataIdx, false);
                    predictedData[dataIdx] = authorData[dataIdx];
                }

            }
        }

        internal bool FetchInput<T>(out T input) where T : unmanaged, INetworkInput
        {
            input = default(T);
            List<InputBlock> inputBlocks = this.currentInput.inputBlocks;
            string typeName = typeof(T).Name;
            if (!typeNameToTypeTable.TryGetValue(typeName, out var inputType)) return false;
            for (int i = 0; i < inputBlocks.Count; i++)
            {
                if (inputBlocks[i].type == inputType)
                {
                    input = inputBlocks[i].Get<T>();
                    return true;
                }
            }

            return false;
        }

        internal override void SetInput<T>(T networkInput, bool needRefreshAlpha = false)
        {
            base.SetInput(networkInput, needRefreshAlpha);
            if (needRefreshAlpha)
            {
                this.clientInterpolationAlpha = this.engine.InterpolationRemote.Alpha;
                this.clientInterpolationRemoteFromTick = this.engine.InterpolationRemote.FromTick;
            }
        }
    }
}