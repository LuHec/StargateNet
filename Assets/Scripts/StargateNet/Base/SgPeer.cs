namespace StargateNet
{
    public abstract class SgPeer
    {
        public virtual bool IsServer => false;
        public virtual bool IsClient => false;

        public SgNetworkEngine Engine { get; set; }

        public SgPeer(SgNetworkEngine engine, SgNetConfigData configData)
        {
            this.Engine = engine;
        }
        
        public abstract void NetworkUpdate();

        /// <summary>
        /// 发送一定的字节，尽量压缩到1400字节左右防止分包。不可靠。
        /// </summary>
        /// <param name="data"></param>
        public abstract void SendMessageUnreliable(byte[] data);

        public abstract void Disconnect();
    }
}