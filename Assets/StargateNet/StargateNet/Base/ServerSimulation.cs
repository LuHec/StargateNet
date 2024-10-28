using System.Collections.Generic;
using Riptide.Utils;

namespace StargateNet
{
    public class ServerSimulation : Simulation
    {
        public Queue<SimulationInput> clientInput = new();

        public ServerSimulation(SgNetworkEngine engine) : base(engine)
        {
        }

        internal override void PreFixedUpdate()
        {
            // 保证每帧都有一个默认的空Tick
            this.currentInput = CreateInput(Tick.InvalidTick, Tick.InvalidTick);
            ConsumeInputs(this.engine.simTick);
        }

        internal void AddInput(Tick clientTick, Tick targetTick)
        {
            clientInput.Enqueue(CreateInput(clientTick, targetTick));
        }

        private void ConsumeInputs(Tick targetTick)
        {
            while (clientInput.Count > 0 && clientInput.Peek().targetTick <= targetTick)
            {
                var input = clientInput.Dequeue();
                if (input.targetTick < targetTick)
                {
                    RecycleInput(input);
                }
                else if (input.targetTick == targetTick)
                {
                    this.currentInput = input;
                }
            }
        }
    }
}