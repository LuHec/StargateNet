using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public class ClientData
    {
        public readonly int maxClientInput;
        public Queue<SimulationInput> clientInput = new(); //  ow gdc所说的input缓冲区        
        public Tick LastTargetTick { get; private set; }
        public bool Started { get; private set; }
        public SimulationInput CurrentInput { get; private set; }
        public double lastPakTime;
        public double deltaPakTime;
        public bool pakLoss = false;
        public bool isFirstPak = true;
        public Tick clientLastAuthorTick = Tick.InvalidTick;

        public ClientData(int maxClientInput)
        {
            this.maxClientInput = maxClientInput;
            this.LastTargetTick = Tick.InvalidTick;
        }

        /// <summary>
        /// 维护从LastReceiveTick开始的Input，后来的会顶掉之前的(可能会顶掉还没被使用的)
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public void ReceiveInput(SimulationInput input)
        {
            this.Started = true;
            clientInput.Enqueue(input);
            this.LastTargetTick = input.clientTargetTick;
        }

        public void PrepareCurrentInput(Tick srvTick)
        {
            if (this.clientInput.Count > 0 && this.clientInput.Peek().clientTargetTick == srvTick)
            {
                this.CurrentInput = this.clientInput.Dequeue();
            }
        }

        public void ClearInput(ServerSimulation simulation)
        {
            simulation.RecycleInput(this.CurrentInput);
            this.CurrentInput = null;
        }

        public void Reset()
        {
            this.Started = false;
            this.clientInput.Clear();
            this.LastTargetTick = Tick.InvalidTick;
        }
    }
}