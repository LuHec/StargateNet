using System.Collections.Generic;

namespace StargateNet
{
    public abstract class Simulation
    {
        internal SgNetworkEngine engine;

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
    }
}