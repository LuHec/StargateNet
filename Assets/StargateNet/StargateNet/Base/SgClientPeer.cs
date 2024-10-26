using System;
using Riptide;
using Riptide.Utils;

namespace StargateNet
{
    public class SgClientPeer : SgPeer
    {
        public override bool IsServer => false;
        public override bool IsClient => true;
        public string ServerIP { private set; get; }
        public ushort Port { private set; get; }
        public Client Client { private set; get; }

        public SgClientPeer(SgNetworkEngine engine, StargateConfigData configData) : base(engine, configData)
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
            // RiptideLogger.Log(LogType.Info, $"{Client.RTT}");
        }

        public void SendMessageUnreliable(byte[] bytes)
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)Protocol.ToServer);
            message.AddBytes(bytes);
            this.Client.Send(message);
        }

        public override void Disconnect()
        {
            this.Client.Disconnect();
        }

        public void Ack()
        {
        }

        private void OnConnected(object sender, EventArgs e)
        {
            RiptideLogger.Log(LogType.Debug, "Client Connected");
            this.Engine.IsConnected = true;
        }

        private void OnConnectionFailed(object sender, ConnectionFailedEventArgs args)
        {
            RiptideLogger.Log(LogType.Debug, "Client Connect Failed");
        }

        bool _firstRecive = true;
        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            var msg = args.Message;
            int authorTick = int.Parse(msg.GetString());
            RiptideLogger.Log(LogType.Debug,
                $"Client Tick:{this.Engine._simTick}:" +
                $", From Server at AuthorTick {authorTick}, RTT:{args.FromConnection.RTT}");
            if (_firstRecive)
            {
                // 测试:对齐Tick，当前Tick应当为authorTick加上已经模拟的Tick，这样保证客户端是先行的
                // 为什么要保证客户端先行？因为预测很大概率是不会出错的，所以客户端已经模拟的操作完全可以上传‘
                // 假设RTT =200ms，服务端Tick为10，服务端到客户端连接消耗了200ms，此时客户端Tick为6，此时服务端的的ds才传到，
                // 那么客户端在这个时候重新模拟并上传操作，同时初次同步将客户端Tick变为13
                // 这三帧都以AuthorTick加Predicate Tick最终上传，消耗100ms。传到服务端时，服务端Tick为13，客户端Tick为16
                // 疑问：当延迟大的时候，客户端的操作到达服务端后操作的Tick比当前AuthorTick小
                // 首先这些操作肯定会被延迟应用，服务端也会记录收到的最新ClientTick
                _firstRecive = false; 
                this.Engine._simTick += authorTick;
            }
        }
    }
}