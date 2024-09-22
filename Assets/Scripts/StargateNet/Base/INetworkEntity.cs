namespace StargateNet
{
    public interface INetworkEntity
    {
        INetworkScript[] NetworkScripts { get; }

        void Initialize(SgNetworkEngine engine);
    }
}