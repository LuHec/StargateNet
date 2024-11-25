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
        internal Tick authoritativeTick = Tick.InvalidTick;  // 客户端接收到的AuthorTick,服务端Tick从10开始 
        internal List<StargateAllocator> predictedSnapshots; // 客户端用于预测snapshot，由于客户端不会预测物体的销毁和生成，所以只存属性
        internal List<SimulationInput> inputs = new(512);
        internal SimulationInput currentInput = new SimulationInput();
        internal StargateAllocator lastAuthorSnapShots;
        internal double serverInputRcvTimeAvg; // 服务端算出来的input接收平均时间
        private readonly int _maxPredictedTicks;


        internal ClientSimulation(StargateEngine engine) : base(engine)
        {
            this._maxPredictedTicks = engine.ConfigData.maxPredictedTicks;
            this.predictedSnapshots = new List<StargateAllocator>(this._maxPredictedTicks);
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
        /// <param name="isMultiPacket">是否是多帧包</param>
        /// <param name="isFullPacket">是否是全量包</param>
        /// <returns>是否接受并处理了该数据包</returns>
        internal bool OnRcvPak(Tick srvTick, Tick srvClientAuthorTick, bool isMultiPacket, bool isFullPacket)
        {
            // 优先处理全量包
            if (isFullPacket && this.IsValidFullPacket(srvTick))
            {
                this.HandleFullPacket(srvTick);
                return true;
            }

            // 检查正常包或多帧包,这里会把首个包丢失的情况也涵盖进去
            if (this.IsValidNormalPacket(srvTick) || this.IsValidMultiPacket(srvTick, srvClientAuthorTick, isMultiPacket))
            {
                this.authoritativeTick = srvTick;
                RiptideLogger.Log(LogType.Debug, $"Packet accepted. Updated Tick to {srvTick.tickValue}.");
                return true;
            }

            // 非法包
            RiptideLogger.Log(LogType.Warning, $"Rejected packet with invalid Tick: {srvTick.tickValue}.");
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


        /// <summary>
        /// 客户端的模拟
        /// </summary>
        internal override void PreFixedUpdate()
        {
            if (!this.authoritativeTick.IsValid)
                return;

            if (this.engine.SimulationClock.IsFirstCall)
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
            while (this.inputs.Count > 0 && this.inputs.Count > this._maxPredictedTicks) // 新输入占位
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
            if (delayTickCount > this._maxPredictedTicks) // 严重丢包时直接移除所有的操作，因为回滚重模拟已经没有意义了
            {
                this.engine.Client.HeavyPakLoss = true;
                RemoveAllInputs();
            }

            if (this.currentTick.IsValid && delayTickCount < this._maxPredictedTicks) // 回滚+重模拟
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