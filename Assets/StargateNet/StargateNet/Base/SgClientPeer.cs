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

        /// <summary>
        /// 每次客户端接收新的snapshot，都会发一个ack表示自己收到了哪一帧的ds。
        /// 即使这个ack丢包了，使得服务端传来的ds有多余信息，客户端也可以根据Tick进行差分得到正确的信息
        /// </summary>
        /// <param name="srvTick"></param>
        public void Ack(Tick srvTick)
        {
            Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToServer);
            msg.AddInt(srvTick.tickValue);
            this.Client.Send(msg);
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
        
         
        /// <summary>
        /// 客户端只会收到两种信息：1.DS，2.input ack
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            var ds = this.Engine.ClientSimulation.snapShots;
            var msg = args.Message;
            if (msg.UnreadBits < 8 * sizeof(int)) return;
            Tick srvtick = new Tick(msg.GetInt());
            this.Engine.ClientSimulation.OnRcvPak(srvtick);
            Ack(srvtick);
            
            // int authorTick = int.Parse(msg.GetString());
            // RiptideLogger.Log(LogType.Debug,
            //     $"Client Tick:{this.Engine.simTick}:" +
            //     $", From Server at AuthorTick {authorTick}, RTT:{args.FromConnection.RTT}");
            //
            // if (_firstRecive)
            // {
            //     // 测试:对齐Tick，当前Tick应当为authorTick加上已经模拟的Tick，这样保证客户端是先行的
            //     // 为什么要保证客户端先行？因为预测很大概率是不会出错的，所以客户端已经模拟的操作完全可以上传‘
            //     // 假设RTT =200ms，服务端Tick为10，服务端到客户端连接消耗了200ms，此时客户端Tick为6，此时服务端的的ds才传到，
            //     // 那么客户端在这个时候重新模拟并上传操作，同时初次同步将客户端Tick变为13
            //     // 这三帧都以AuthorTick加Predicate Tick最终上传，消耗100ms。传到服务端时，服务端Tick为13，客户端Tick为16
            //     // 疑问：当延迟大的时候，客户端的操作到达服务端后操作的Tick比当前AuthorTick小
            //     // 首先这些操作肯定会被延迟应用，服务端也会记录收到的最新ClientTick
            //     _firstRecive = false; 
            //     this.Engine.simTick += authorTick;
            // }
        }
    }
}