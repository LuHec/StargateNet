using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public class ClientData
    {
        internal readonly ServerSimulation serverSimulation;
        internal readonly int maxClientInput;
        internal Queue<SimulationInput> clientInput = new(); //  ow gdc所说的input缓冲区        
        internal Tick LastTargetTick { get; private set; }
        internal bool Started { get; private set; }
        internal SimulationInput CurrentInput { get; private set; }
        internal double lastPakTime;
        internal double deltaPakTime;
        internal bool pakLoss = false;
        internal bool isFirstPak = true;
        internal Tick clientLastAuthorTick = Tick.InvalidTick;

        public ClientData(ServerSimulation simulation, int maxClientInput)
        {
            this.serverSimulation = simulation;
            this.maxClientInput = maxClientInput;
            this.LastTargetTick = Tick.InvalidTick;
        }

        /// <summary>
        /// 维护从LastReceiveTick开始的Input，后来的会顶掉之前的.
        /// 因为客户端是连续发送输入的，所以这里保证接收的输入是连续的，不存在32，33，34，35这样的情况。
        /// 只可能出现lastTick33，收到了56,57...这种情况是丢了非常多的包
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public bool ReceiveInput(SimulationInput input)
        {
            this.Started = true;
            if (input.clientTargetTick <= this.LastTargetTick && this.LastTargetTick.IsValid)
            {
                return false;
            }

            while (this.clientInput.Count > this.maxClientInput)
            {
                this.serverSimulation.RecycleInput(this.clientInput.Dequeue());
            }

            this.clientInput.Enqueue(input);
            this.LastTargetTick = input.clientTargetTick;
            return true;
        }

        internal void PrepareCurrentInput(Tick srvTick)
        {
            if (this.clientInput.Count > 0 && this.clientInput.Peek().clientTargetTick == srvTick)
            {
                this.CurrentInput = this.clientInput.Dequeue();
            }
        }

        internal void ClearInput(ServerSimulation simulation)
        {
            simulation.RecycleInput(this.CurrentInput);
            this.CurrentInput = null;
        }

        internal void Reset()
        {
            this.Started = false;
            this.clientInput.Clear();
            this.LastTargetTick = Tick.InvalidTick;
        }
    }
}