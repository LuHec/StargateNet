namespace StargateNet
{
    public abstract class SgTransport
    {
        public virtual bool IsServer => false;
        public virtual bool IsClient => false;

        public SgTransport(SgNetConfigData configData)
        {
            
        }
        
        public abstract void NetworkUpdate();

        public abstract void SendMessage(string str);

        public abstract void Disconnect();
    }
}