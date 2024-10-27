using System.Collections.Generic;

namespace StargateNet
{
    public class ClientSimulation : Simulation
    {
        internal Tick currentTick = Tick.InvalidTick;
        internal Tick predictedTick = Tick.InvalidTick;
        internal Tick authoritativeTick = Tick.InvalidTick;
        internal Queue<SimulationInput> inputPool = new();
        internal RingQueue<StargateAllocator> snapShots = new(32); //本地可取的snapshot环形队列，可以获得前32帧的snapshot
        internal RingQueue<SimulationInput> inputs;
        internal SimulationInput currentInput = new SimulationInput();
        internal StargateAllocator lastAuthorSnapShots;
        
        
        internal ClientSimulation(SgNetworkEngine engine) : base(engine)
        {
            this.inputs = new RingQueue<SimulationInput>(engine.ConfigData.maxPredictedTicks);
        }

        internal void OnRcvPak(Tick svrTick)
        {
            
        }

        /// <summary>
        /// 客户端的模拟
        /// </summary>
        internal override void PreFixedUpdate()
        {
            if (!this.authoritativeTick.IsValid)
                return;

            if (this.engine.timer.IsFirstCall)
                Reconcile();
        }

        internal override void PreUpdate()
        {
        }

        /// <summary>
        /// RollBack和Resim
        /// </summary>
        private void Reconcile()
        {
            int predictedTickCount = this.currentTick - this.authoritativeTick;
            this.currentTick = this.authoritativeTick; // currentTick复制authorTick，并从这一帧开始重新模拟

            if (this.currentTick.IsValid && predictedTickCount < this.engine.ConfigData.maxPredictedTicks)
            {
                // 回滚
                
                while (this.inputs.Size > 0)
                {
                    this.currentInput = this.inputs.DeQueue();
                    //重新模拟
                    // 客户端的模拟帧数
                    this.currentTick++;
                }
            }
        }

        private void OnHeavyLose()
        {
        }
    }
}