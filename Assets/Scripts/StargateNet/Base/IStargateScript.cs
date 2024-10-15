namespace StargateNet
{
    public interface IStargateScript
    {
        void NetworkStart();
        void NetworkUpdate();
        void NetworkFixedUpdate();
        void NetworkRender();
        void NetworkDestroy();
    }
}