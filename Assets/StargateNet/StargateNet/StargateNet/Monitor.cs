namespace StargateNet
{
    public sealed class Monitor
    {
        public int tick = -1;
        public float deltaTime = -1;
        public int clockLevel = 0; // 0：慢速 1:基准速度 2：加速
        public float rtt = -1;
        public float smothRTT = -1;
        public int resims = -1;
        public int connectedClients = -1;
        public int inputCount = -1;
        public int entities = 0;
        public ulong unmanagedMemeory = 0;
        public ulong unmanagedMemeoryInuse = 0; // 使用中的内存(不包含block_header)
    }
}