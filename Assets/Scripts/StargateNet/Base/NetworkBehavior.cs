namespace StargateNet
{
    public abstract class NetworkBehavior :  INetworkScript, INetworkEntityScript
    {
        public unsafe int* networkedBlock;
        unsafe int* INetworkEntityScript.State => this.networkedBlock;
        
        public virtual void NetworkStart()
        {
            
        }

        public virtual void NetworkUpdate()
        {
            
        }

        public virtual void NetworkFixedUpdate()
        {
            
        }

        public virtual void NetworkRender()
        {
            
        }

        public virtual void NetworkDestroy()
        {
            
        }
    }
}