namespace StargateNet
{
    public abstract class SgPeer
    {
        public virtual bool IsServer => false;
        public virtual bool IsClient => false;

        public SgNetworkEngine Engine { get; set; }

        public SgPeer(SgNetworkEngine engine, StargateConfigData configData)
        {
            this.Engine = engine;
        }
        
        public abstract void NetworkUpdate();

        public abstract void Disconnect();
    }
}