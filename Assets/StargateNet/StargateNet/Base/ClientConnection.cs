namespace StargateNet
{
    public class ClientConnection
    {
        private ClientData _clientData;
        public float RTT { private set; get; } // 根据Snapshot的ack计算

        public ClientConnection()
        {
            
        }
    }
}