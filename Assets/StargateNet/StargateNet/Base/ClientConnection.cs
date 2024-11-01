using Riptide;

namespace StargateNet
{
    public class ClientConnection
    {
        public bool connected = false;
        public ClientData clientData;
        public Connection connection;
        public Tick lastAckTick;
    }
}