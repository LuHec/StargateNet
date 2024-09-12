using System;
using Riptide;
using Riptide.Utils;

namespace StargateNet
{
    public class SgClientTransport : SgTransport
    {
        public override bool IsServer => false;
        public override bool IsClient => true;

        public string ServerIP { private set; get; }
        public ushort Port { private set; get; }

        public Client Client { private set; get; }

        public SgClientTransport(SgNetConfigData configData) : base(configData)
        {
            this.Client = new Client();
            Client.ConnectionFailed += this.OnConnectionFailed;
            Client.Connected += this.OnConnected;
        }

        public void Connect(string serverIP, ushort port)
        {
            this.ServerIP = serverIP;
            this.Port = port;
            this.Client.Connect($"{ServerIP}:{Port}");
            RiptideLogger.Log(LogType.Info, "Client Connecting");
        }

        public override void NetworkUpdate()
        {
            this.Client.Update();
        }

        public override void SendMessage(string str)
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)Protocol.ToServer);
            message.AddString(str);

            this.Client.Send(message);
        }

        [MessageHandler((ushort)Protocol.ToClient)]
        public static void MessageReceiver(Message message)
        {
            RiptideLogger.Log(LogType.Debug, message.GetString());
        }

        public override void Disconnect()
        {
            this.Client.Disconnect();
        }

        private void OnConnected(object sender, EventArgs e)
        {
            RiptideLogger.Log(LogType.Debug, "Client Connected");
        }

        private void OnConnectionFailed(object sender, ConnectionFailedEventArgs args)
        {
            RiptideLogger.Log(LogType.Debug, "Client Connect Failed");
        }
    }
}