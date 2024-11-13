namespace StargateNet
{
    public abstract class SgPeer
    {
        internal virtual bool IsServer => false;
        internal virtual bool IsClient => false;
        internal StargateEngine Engine { get; set; }
        internal SgPeer(StargateEngine engine, StargateConfigData configData)
        {
            this.Engine = engine;
        }

        public abstract void NetworkUpdate();

        public abstract void Disconnect();
    }
}