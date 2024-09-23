namespace StargateNet
{
    public class ClientSimulation : Simulation
    {
        internal Tick currentTick = Tick.InvalidTick;
        internal Tick predictedTick = Tick.InvalidTick;
        internal Tick authoritativeTick = Tick.InvalidTick;
        
        public ClientSimulation(SgNetworkEngine engine) : base(engine)
        {
        }
        
        public override void PreStep()
        {
            
        }
        
        public override void PreUpdate()
        {
            
        }
        
        // TODO:有处理两帧，上一帧和当前帧(未来帧) From,To
    }
}