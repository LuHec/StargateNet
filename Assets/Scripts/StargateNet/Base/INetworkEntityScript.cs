namespace StargateNet
{
    public interface INetworkEntityScript
    {
        unsafe int* StateBlock { get; }
        Entity Entity { get; }
    }
}