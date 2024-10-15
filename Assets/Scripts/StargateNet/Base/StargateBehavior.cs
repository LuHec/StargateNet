namespace StargateNet
{
    public abstract class StargateBehavior : IStargateNetworkScript, IStargateScript
    {
        public unsafe int* StateBlock { get; internal set; }
        public Entity Entity { get; internal set; }
        public void NetworkStart()
        {
            
        }

        public void NetworkUpdate()
        {
            
        }

        public void NetworkFixedUpdate()
        {
            
        }

        public void NetworkRender()
        {
            
        }

        public void NetworkDestroy()
        {
            
        }
    }
}