using Riptide;

namespace StargateNet
{
    public class ClientConnection
    {
        public ClientData clientData;
        public Connection connection;
        public Tick lastAckTick;
    }
}