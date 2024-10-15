namespace StargateNet
{
    public interface IStargateNetworkScript
    {
        unsafe int* StateBlock { get; }
        Entity Entity { get; }
    }
}