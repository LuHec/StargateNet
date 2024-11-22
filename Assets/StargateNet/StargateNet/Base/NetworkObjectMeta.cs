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
            stateWordSize = -1,
            destroyed = false
        };
        public int networkId;
        public int prefabId;
        public int stateWordSize; // 近state的大小，*2后才是Entity内存大小 
        public bool destroyed;
    }
}