using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public class ClientData
    {
        public readonly int maxClientInput;
        public Queue<SimulationInput> clientInput = new(); //  ow gdc所说的input缓冲区        
        public Tick LastTick { get; private set; }
        public bool Started { get; private set; }
        public SimulationInput currentInput = new SimulationInput();

        public ClientData(int maxClientInput)
        {
            this.maxClientInput = maxClientInput;
            this.LastTick = Tick.InvalidTick;
        }

        public bool ReciveInput(SimulationInput input)
        {
            if (clientInput.Count >= this.maxClientInput)
                return false;
            if (this.LastTick.IsValid && input.targetTick < this.LastTick)
                return false;

            this.Started = true;
            clientInput.Enqueue(input);
            this.LastTick = input.targetTick;
            return true;
        }

        public void Reset()
        {
            this.Started = false;
            this.clientInput.Clear();
            this.LastTick = Tick.InvalidTick;
        }
    }
}