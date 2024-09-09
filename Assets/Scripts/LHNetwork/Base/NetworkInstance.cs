public abstract class NetworkInstance
{
    public virtual bool IsServer => false;
    
    public virtual bool IsClient => true;
    
    public abstract void NetworkStart();
    
    public abstract void NetworkUpdate();

    public abstract void Connect();
    
    public abstract void OnQuit();
}

