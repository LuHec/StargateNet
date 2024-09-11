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
        
        public void Connect(string serverIP, ushort port)
        {
            this.ServerIP = serverIP;
            this.Port = port;
            this.Client.Connect($"{ServerIP}:{Port}");
        }

        public override void TransportCreate()
        {
            this.Client = new Client();
        }

        public override void TransportUpdate()
        {
            this.Client.Update();
        }

        public override void SendMessage()
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)ServerToClientId.sync);
            message.AddString("Hello");

            this.Client.Send(message);
        }

        public override void OnQuit()
        {
            this.Client.Disconnect();
        }
    }
}