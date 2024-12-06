namespace StargateNet
{
    public interface IStargateScript
    {
        void NetworkStart(SgNetworkGalaxy galaxy);
        void NetworkUpdate(SgNetworkGalaxy galaxy);
        void NetworkFixedUpdate(SgNetworkGalaxy galaxy);
        void NetworkRender(SgNetworkGalaxy galaxy);
        void NetworkDestroy(SgNetworkGalaxy galaxy);
    }
}