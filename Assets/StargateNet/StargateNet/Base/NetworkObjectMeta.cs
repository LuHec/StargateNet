namespace StargateNet
{
    /// <summary>
    /// 用于记录一个NetworkObject的存活信息
    /// </summary>
    public struct NetworkObjectMeta
    {
        public int networkId;
        public bool destroyed;
    }
}