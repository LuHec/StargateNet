using System;
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
        internal Tick authoritativeTick = Tick.InvalidTick;
        internal RingQueue<StargateAllocator> snapShots = new(32); //本地可取的snapshot环形队列，可以获得前32帧的snapshot
        internal List<SimulationInput> inputs = new(512);
        internal SimulationInput currentInput = new SimulationInput();
        internal StargateAllocator lastAuthorSnapShots;
        internal double serverInputRcvTimeAvg; // 服务端算出来的input接收平均时间


        internal ClientSimulation(SgNetworkEngine engine) : base(engine)
        {
        }

        /// <summary>
        /// 保证会在Sim流程前调用
        /// </summary>
        /// <param name="srvTick"></param>
        internal void OnRcvPak(Tick srvTick)
        {
            this.engine.Timer.OnRecvPak();
            authoritativeTick = srvTick;
        }

        /// <summary>
        /// 客户端的模拟
        /// </summary>
        internal override void PreFixedUpdate()
        {
            if (!this.authoritativeTick.IsValid)
                return;

            RiptideLogger.Log(LogType.Warning,
                $"No Rollback yet,Clinet CurrentTick:{this.currentTick.tickValue}, AuthoritativeTick:{this.authoritativeTick}");
            if (this.engine.Timer.IsFirstCall)
                Reconcile();
            RiptideLogger.Log(LogType.Warning,
                $"Rollback,Clinet CurrentTick:{this.currentTick.tickValue}, AuthoritativeTick:{this.authoritativeTick}");
            SimulationInput input = CreateInput(this.authoritativeTick, this.currentTick);
            this.inputs.Add(input);
            while (this.inputs.Count > 0 && this.inputs.Count > this.engine.ConfigData.maxPredictedTicks) // 新输入占位
            {
                RecycleInput(this.inputs[0]);
                this.inputs.RemoveAt(0);
            }
        }

        internal override void PostFixedUpdate()
        {
            this.engine.Monitor.tick = this.currentTick.tickValue;
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
            if (delayTickCount > this.engine.ConfigData.maxPredictedTicks) // 严重丢包时直接移除所有的操作，因为回滚重模拟已经没有意义了
            {
                this.engine.Client.HeavyPakLoss = true;
                RemoveAllInputs();
            }

            if (this.currentTick.IsValid && delayTickCount < this.engine.ConfigData.maxPredictedTicks) // 回滚+重模拟
            {
                RemoveInputBefore(this.authoritativeTick);
                for (int i = 0; i < this.inputs.Count; i++)
                {
                    this.currentTick++;
                }
            }

            this.engine.Monitor.resims = this.currentTick - this.authoritativeTick;
            this.engine.Monitor.inputCount = this.inputs.Count;
        }

        private void RemoveAllInputs()
        {
            for (int i = 0; i < this.inputs.Count; i++)
            {
                RecycleInput(this.inputs[0]);
                this.inputs.Clear();
            }
        }

        private void RemoveInputBefore(Tick targetTick)
        {
            if (this.inputs.Count == 0) return;
            if (this.inputs[^1].targetTick < targetTick)
            {
                RemoveAllInputs();
            }
            else
            {
                while (this.inputs.Count > 0 && this.inputs[0].targetTick < targetTick)
                {
                    RecycleInput(this.inputs[0]);
                    this.inputs.RemoveAt(0);
                }
            }
        }
    }
}