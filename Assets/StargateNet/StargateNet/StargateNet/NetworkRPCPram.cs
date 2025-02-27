namespace StargateNet
{
    public class NetworkRPCPram
    {
        public int entityId;
        public int scriptId;
        public int rpcId;
        public unsafe byte* prams;
        public int pramsBytes;
    }
}