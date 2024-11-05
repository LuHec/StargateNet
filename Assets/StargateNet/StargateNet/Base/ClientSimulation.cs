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
        internal RingQueue<Snapshot> snapShots = new(32); //本地可取的snapshot环形队列，可以获得前32帧的snapshot
        internal List<SimulationInput> inputs = new(512);
        internal SimulationInput currentInput = new SimulationInput();
        internal StargateAllocator lastAuthorSnapShots;
        internal double serverInputRcvTimeAvg; // 服务端算出来的input接收平均时间


        internal ClientSimulation(StargateEngine engine) : base(engine)
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

            if (this.engine.Timer.IsFirstCall)
            {
                Reconcile();
                // 只有第一次模拟才创建输入，Clock后续的追帧模拟因为在同一unity帧内读取不到用户输入,所以不加入
                // 否则会因为重复输入过多而冲掉了服务端后续接受到的有效帧数
                // (服务端优先保留旧的输入，【待求证】如果优先新的帧数，可能导致服务端下一帧的输入被冲掉)
                SimulationInput input = CreateInput(this.authoritativeTick, this.currentTick);
                this.inputs.Add(input);
            }

            // 关于新输入把旧输入冲掉导致服务端丢失操作的问题：
            // 首先模拟函数的调用时机在Send Input之后
            // Reconcile中限定了客户端的输入范围在AuthorTick到CurrentTick，都是上一帧的数据，已经在Send中被发出。
            // 本帧要发的有效数据应该是AuthorTick + 1到CurrentTick + 1
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