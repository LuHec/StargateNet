namespace StargateNet
{
    /// <summary>
    /// 提供给用户的接口
    /// </summary>
    public interface IStargateScript
    {
        Entity Entity { get; }
        void NetworkStart(SgNetworkGalaxy galaxy);
        void NetworkLaterStart(SgNetworkGalaxy galaxy);
        void NetworkUpdate(SgNetworkGalaxy galaxy);
        void NetworkFixedUpdate(SgNetworkGalaxy galaxy);
        void NetworkRender(SgNetworkGalaxy galaxy);
        void NetworkDestroy(SgNetworkGalaxy galaxy);
        void SerializeToNetcode();
        void DeserializeToGameCode();
    }
}