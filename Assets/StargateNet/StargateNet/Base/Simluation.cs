using System.Collections.Generic;

namespace StargateNet
{
    public abstract class Simulation
    {
        internal SgNetworkEngine engine;
        protected Queue<SimulationInput> inputPool = new();

        internal Simulation(SgNetworkEngine engine)
        {
            this.engine = engine;
        }

        internal virtual void PreUpdate()
        {
        }

        internal virtual void PreFixedUpdate()
        {
        }

        internal virtual void PostFixedUpdate()
        {
        }

        /// <summary>
        /// Simulate world in fixed update
        /// </summary>
        internal void FixedUpdate()
        {
            // 对于客户端，先在这里处理回滚，然后再模拟下一帧
            this.PreFixedUpdate();
            this.ExecuteNetworkFixedUpdate();
            this.PostFixedUpdate();
        }

        internal void ExecuteNetworkUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkUpdate();
        }

        internal void ExecuteNetworkRender()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkRender();
        }

        internal void ExecuteNetworkFixedUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkFixedUpdate();
        }

        internal SimulationInput CreateInput(Tick srvTick, Tick targetTick)
        {
            if (inputPool.Count == 0)
            {
                inputPool.Enqueue(new SimulationInput());
            }

            SimulationInput resInput = inputPool.Dequeue();
            resInput.srvTick = srvTick;
            resInput.targetTick = targetTick;
            return resInput;
        }

        internal void RecycleInput(SimulationInput input)
        {
            this.inputPool.Enqueue(input);
        }
    }
}