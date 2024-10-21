using System.Collections.Generic;

namespace StargateNet
{
    public abstract class Simulation
    {
        public SgNetworkEngine engine;

        public Simulation(SgNetworkEngine engine)
        {
            this.engine = engine;
        }
        
        public virtual void PreUpdate()
        {
            
        }
        
        public virtual void PreFixedUpdate()
        {
            
        }

        /// <summary>
        /// Simulate world in fixed update
        /// </summary>
        public void FixedUpdate()
        {
            this.PreFixedUpdate();
            // TODO:客户端需要检测当前Tick是否有效(如果没有连接，那此时的Tick是无效的)
            this.ExecuteNetworkFixedUpdate();
        }
        
        public void ExecuteNetworkUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkUpdate();
        }

        public void ExecuteNetworkRender()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkRender();
        }
        
        public void ExecuteNetworkFixedUpdate()
        {
            if (!this.engine.Simulated) return;
            this.engine.IM.ExecuteNetworkFixedUpdate();
        }
    }
}