namespace StargateNet
{
    public abstract class SgTransport
    {
        public virtual bool IsServer => false;
        public virtual bool IsClient => false;

        public abstract void TransportStart();

        public abstract void TransportUpdate();

        public abstract void Connect();

        public abstract void SendMessage();

        public abstract void OnQuit();
    }
}