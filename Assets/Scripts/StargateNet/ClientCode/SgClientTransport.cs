using Riptide;

namespace StargateNet
{
    public class SgClientTransport : SgTransport
    {
        public override bool IsServer => false;
        public override bool IsClient => true;

        public string ServerIP { private set; get; }
        public ushort Port { private set; get; }
        
        public Client Client { private set; get; }

        public SgClientTransport(string serverIP, ushort port)
        {
            ServerIP = serverIP;
            Port = port;
        }
        
        public override void TransportStart()
        {
            Client = new Client();
        }

        public override void TransportUpdate()
        {
            Client.Update();
        }

        public override void Connect()
        {
            Client.Connect($"{ServerIP}:{Port}");
        }

        public override void SendMessage()
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)ServerToClientId.sync);
            message.AddString("Hello");

            Client.Send(message);
        }
        
        public override void OnQuit()
        {
            Client.Disconnect();
        }
    }
}