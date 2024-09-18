namespace StargateNet
{
    public interface INetworkScript
    {
        public void NetworkStart();
        public void NetworkUpdate();
        public void NetworkFixedUpdate();
        public void NetworkRender();
        public void NetworkDestroy();
    }
}