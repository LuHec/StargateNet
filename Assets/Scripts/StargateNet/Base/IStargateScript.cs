namespace StargateNet
{
    public interface IStargateScript
    {
        public void NetworkStart();
        public void NetworkUpdate();
        public void NetworkFixedUpdate();
        public void NetworkRender();
        public void NetworkDestroy();
    }
}