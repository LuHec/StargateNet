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
            this.Client.ConnectionFailed += this.OnConnectionFailed;
            this.Client.Connected += this.OnConnected;
            this.Client.MessageReceived += this.OnReceiveMessage;
        }

        public void Connect(string serverIP, ushort port)
        {
            this.ServerIP = serverIP;
            this.Port = port;
            this.Client.Connect($"{ServerIP}:{Port}", useMessageHandlers: false);
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

        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            var msg = args.Message;
            RiptideLogger.Log(LogType.Debug, $"id:{args.MessageId}:" + msg.GetString());
        }
    }
}