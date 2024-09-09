using Riptide;

namespace LHNetwork.ClientCode
{
    public class ClientNetworkInstance : NetworkInstance
    {
        public override bool IsServer => false;
        public override bool IsClient => true;

        public string ServerIP { private set; get; }
        public ushort Port { private set; get; }
        
        public Client Client { private set; get; }

        public ClientNetworkInstance(string serverIP, ushort port)
        {
            ServerIP = serverIP;
            Port = port;
        }
        
        public override void NetworkStart()
        {
            Client = new Client();
        }

        public override void NetworkUpdate()
        {
            Client.Update();
        }

        public override void Connect()
        {
            Client.Connect($"{ServerIP}:{Port}");
        }

        public override void OnQuit()
        {
            Client.Disconnect();
        }
    }
}