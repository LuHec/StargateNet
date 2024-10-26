using System.Collections.Generic;
using System.Text;
using Riptide;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public class SgServerPeer : SgPeer
    {
        public override bool IsServer => true;
        public override bool IsClient => false;

        public ushort Port { private set; get; }
        public ushort MaxClientCount { private set; get; }
        public Server Server { private set; get; }
        public Dictionary<ushort, Connection> clientConnections = new();

        public SgServerPeer(SgNetworkEngine engine, StargateConfigData configData) : base(engine, configData)
        {
            this.Server = new Server();
            this.Server.ClientConnected += this.OnConnect;
            this.Server.MessageReceived += this.OnReceiveMessage;
        }

        public void StartServer(ushort port, ushort maxClientCount)
        {
            this.Port = port;
            this.MaxClientCount = maxClientCount;
            this.Server.Start(port, maxClientCount, useMessageHandlers: false);
            RiptideLogger.Log(LogType.Debug, "Server Start");
        }

        public override void NetworkUpdate()
        {
            this.Server.Update();
        }

        /// <summary>
        /// 发送一定的字节，尽量压缩到1400字节左右防止分包。不可靠。
        /// </summary>
        /// <param name="clientId">客户端的id</param>
        /// <param name="data">数据</param>
        public void SendMessageUnreliable(ushort clientId, byte[] data)
        {
            Message message = Message.Create(MessageSendMode.Unreliable, (ushort)Protocol.ToClient);
            message.AddBytes(data);
            this.Server.Send(message, clientConnections[clientId]);
        }

        public override void Disconnect()
        {
            this.Server.Stop();
        }

        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            var msg = args.Message;
            // msg.BytesInUse
            RiptideLogger.Log(LogType.Debug,
                $"Server Tick:{this.Engine._simTick}:" +
                $", From {args.FromConnection.Id} at ClientTick {msg.GetString()}, RTT:{args.FromConnection.RTT}");
        }

        private void OnConnect(object sender, ServerConnectedEventArgs args)
        {
            if (clientConnections.TryAdd(args.Client.Id, args.Client))
            {
            }
        }
    }
}