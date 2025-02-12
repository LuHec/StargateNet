namespace StargateNet
{
    public abstract class SgPeer
    {
        internal virtual bool IsServer => false;
        internal virtual bool IsClient => false;
        internal StargateEngine Engine { get; set; }
        internal float InKBps => bytesIn.AvgKBps;
        internal float OutKBps => bytesOut.AvgKBps;
        internal const int MTU = 1300;
        protected DataAccumulator bytesIn;
        protected DataAccumulator bytesOut;

        internal SgPeer(StargateEngine engine, StargateConfigData configData)
        {
            this.Engine = engine;
            this.bytesIn = new DataAccumulator(2);
            this.bytesOut = new DataAccumulator(2);
        }

        internal abstract void NetworkUpdate();

        public abstract void Disconnect();
    } 
}