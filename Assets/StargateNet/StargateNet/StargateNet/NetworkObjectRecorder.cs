namespace StargateNet
{
    public struct NetworkObjectRecorder
    {
        public int networkId;
        public Tick lastSendTick;
        public bool needSync;
    }
}