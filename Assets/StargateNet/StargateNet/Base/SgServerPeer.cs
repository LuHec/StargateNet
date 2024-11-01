using System;
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
        public List<ClientConnection> clientConnections;


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
            this.clientConnections = new List<ClientConnection>(maxClientCount);
            RiptideLogger.Log(LogType.Debug, "Server Start");
        }

        public override void NetworkUpdate()
        {
            this.Server.Update();
        }

        /// <summary>
        /// 尽量压缩到1400字节左右防止分包。
        /// </summary>
        /// <param name="clientId">客户端的id</param>
        /// <param name="msg">数据</param>
        public void SendMessageUnreliable(ushort clientId, Message msg)
        {
            if (clientConnections[clientId].connected)
                this.Server.Send(msg, clientConnections[clientId].connection);
        }

        public void SendServerPak()
        {
            Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToClient);
            msg.AddInt(this.Engine.simTick.tickValue);
            ClientData[] clientDatas = this.Engine.ServerSimulation.clientDatas;
            for (int i = 1; i < clientDatas.Length; i++)
            {
                if (clientDatas[i].Started)
                {
                    msg.AddDouble(clientDatas[i].deltaPakTime);
                    this.Server.Send(msg, (ushort)i);
                }
            }
        }

        public override void Disconnect()
        {
            this.Server.Stop();
        }

        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            var msg = args.Message;
            msg.GetBits(1, out byte flag);
            // 1为ack，0为input
            if ((flag & 1) == 0)
            {
                int inputCount = msg.GetInt();
                ClientData clientData = this.clientConnections[args.FromConnection.Id].clientData;
                clientData.deltaPakTime = this.Engine.Timer.Time - clientData.lastPakTime;
                clientData.lastPakTime = this.Engine.Timer.Time;
                for (int i = 0; i < inputCount; i++)
                {
                    int targetTick = msg.GetInt();
                    if (targetTick < clientData.LastTick.tickValue)
                    {
                        continue;
                    }

                    SimulationInput simulationInput =
                        this.Engine.ServerSimulation.CreateInput(Tick.InvalidTick, new Tick(targetTick));
                    while (clientData.clientInput.Count >= clientData.maxClientInput)
                    {
                        this.Engine.ServerSimulation.RecycleInput(clientData.clientInput.Dequeue());
                    }

                    clientData.ReciveInput(simulationInput);
                }

                RiptideLogger.Log(LogType.Error,
                    $"recv count:{inputCount}, actully input count get from pak:{this.clientConnections[args.FromConnection.Id].clientData.clientInput.Count}");
            }
            else
            {
                int confirmTick = msg.GetInt();
                RiptideLogger.Log(LogType.Debug,
                    $"Server Tick:{this.Engine.simTick}:" +
                    $", Acked from {args.FromConnection.Id} at Tick {confirmTick}, RTT:{args.FromConnection.RTT}");
            }
        }

        private void OnConnect(object sender, ServerConnectedEventArgs args)
        {
            if (clientConnections[args.Client.Id].connected == false)
            {
                this.clientConnections[args.Client.Id].connected = true;
                this.clientConnections[args.Client.Id].connection = args.Client;
                ClientData clientData = this.clientConnections[args.Client.Id].clientData =
                    this.Engine.ServerSimulation.clientDatas[args.Client.Id];
                clientData.Reset();
            }

            this.Engine.Monitor.connectedClients = this.clientConnections.Count;
        }
    }
}