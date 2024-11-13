namespace StargateNet
{
    /// <summary>
    /// 用于记录一个NetworkObject的存活信息
    /// </summary>
    public struct NetworkObjectMeta
    {
        public int networkId;
        public int prefabId;
        public int stateWordSize; // 近state的大小，*2后才是Entity内存大小 
        public bool destroyed;
    }
}