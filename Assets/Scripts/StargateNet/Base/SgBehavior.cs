namespace StargateNet
{
    public abstract class SgBehavior : INetworkEntityScript
    {
        public unsafe int* StateBlock { get; internal set; }
        public Entity Entity { get; internal set; }
    }
}