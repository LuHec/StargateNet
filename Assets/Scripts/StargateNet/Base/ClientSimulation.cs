namespace StargateNet
{
    public class ClientSimulation : Simulation
    {
        internal Tick currentTick = Tick.InvalidTick;   // 当前帧数
        internal Tick predictedTick = Tick.InvalidTick; // 将要执行模拟的帧数
        internal Tick authoritativeTick = Tick.InvalidTick; // 服务器帧数
        
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