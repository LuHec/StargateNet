using System.Collections.Generic;

namespace StargateNet
{
    public abstract class Simulation
    {
        internal SgNetworkEngine engine;
        internal SimulationInput currentInput;
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

        /// <summary>
        /// Simulate world in fixed update
        /// </summary>
        internal void FixedUpdate()
        {
            this.ExecuteNetworkFixedUpdate();
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
        
        protected SimulationInput CreateInput(Tick srvTick, Tick targetTick)
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
        
        protected void RecycleInput(SimulationInput input)
        {
            this.inputPool.Enqueue(input);
        }

    }
}