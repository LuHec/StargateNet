namespace StargateNet
{
    public class ClientSimulation : Simulation
    {
        internal Tick currentTick = Tick.InvalidTick;   
        internal Tick predictedTick = Tick.InvalidTick; 
        internal Tick authoritativeTick = Tick.InvalidTick; 
        internal RingQueue<StargateAllocator> snapShots = new RingQueue<StargateAllocator>(32); //本地可取的snapshot环形队列，可以获得前32帧的snapshot
        internal StargateAllocator lastAuthorSnapShots;
        
        public ClientSimulation(SgNetworkEngine engine) : base(engine)
        {
        }
        
        public override void PreFixedUpdate()
        {
            
        }
        
        public override void PreUpdate()
        {
            
        }
        
        // TODO:有处理两帧，上一帧和当前帧(未来帧) From,To
    }
}