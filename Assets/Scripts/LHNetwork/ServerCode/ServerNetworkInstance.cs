using Riptide;

namespace LHNetwork.ServerCode
{
    public class ServerNetworkInstance : NetworkInstance
    {
        public override bool IsServer => true;
        public override bool IsClient => false; 
        
        public ushort Port { private set; get; }
        public ushort MaxClientCount { private set; get; }
        public Server Server { private set; get; }

        public ServerNetworkInstance(ushort port, ushort maxClientCount)
        {
            Port = port;
            MaxClientCount = maxClientCount;
        }

        public override void NetworkStart()
        {
            Server = new Server();
        }
        
        public override void NetworkUpdate()
        {
            Server.Update();
        }

        public override void Connect()
        {
            Server.Start(Port, MaxClientCount);
        }

        public override void OnQuit()
        {
            Server.Stop();
        }
    }
}