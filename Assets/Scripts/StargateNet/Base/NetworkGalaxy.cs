namespace StargateNet
{
    /// <summary>
    ///  Network Engine, client and server run in same way
    /// </summary>
    public abstract class NetworkGalaxy
    {
        public SgNetEngine Engine { private set; get; }
        public virtual int CurrentTick { get; protected set; }
        public virtual bool IsServer => false;
    
        public virtual bool IsClient => true;
    
        public abstract void NetworkStart();
    
        public abstract void NetworkUpdate();

        public abstract void Connect();

        public abstract void SendMessage();
    
        public abstract void OnQuit();
    }
}

