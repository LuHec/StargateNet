namespace StargateNet
{
    public interface INetworkEntity
    {
        Entity Entity { get; }
        IStargateScript[] NetworkScripts { get; }
    }
}