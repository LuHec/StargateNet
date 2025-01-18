namespace StargateNet
{
    public interface INetworkEntity
    {
        Entity Entity { get; }
        IStargateNetworkScript[] NetworkScripts { get; }
    }
}