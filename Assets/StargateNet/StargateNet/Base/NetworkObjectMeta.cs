namespace StargateNet
{
    /// <summary>
    /// 用于记录一个NetworkObject的存活信息
    /// </summary>
    public struct NetworkObjectMeta
    {
        public static readonly NetworkObjectMeta Invalid = new NetworkObjectMeta()
        {
            networkId = -1,
            prefabId = -1,
            inputSource = -1,
            destroyed = true
        };
        public int networkId;
        public int prefabId;
        public int inputSource;
        public bool destroyed;
    }
}