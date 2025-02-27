namespace StargateNet
{
    /// <summary>
    /// 负责处理网络框架
    /// </summary>
    public interface IStargateNetworkScript : IStargateScript
    { 
        unsafe int* StateBlock { get; }
        new Entity Entity { get; }
        int ScriptIdx { get; set; }
        void Initialize(Entity entity);
        void InternalInit();
        void InternalReset();
        void InternalRegisterRPC();
    }
}