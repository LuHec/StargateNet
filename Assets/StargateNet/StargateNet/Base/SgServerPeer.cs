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
        internal override bool IsServer => true;
        internal override bool IsClient => false;
        internal ushort Port { private set; get; }
        internal ushort MaxClientCount { private set; get; }
        internal Server Server { private set; get; }

        internal List<ClientConnection>
            clientConnections; // 暂时先用List(有隐患，Riptide给Client的id是递增的，一个CLient断线重连后获得的id和以前不一样)

        private List<int> _cachedMetaIds;
        private List<int> _cachedObjectIds;

        public SgServerPeer(StargateEngine engine, StargateConfigData configData) : base(engine, configData)
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
            // this.clientConnections.Add(new ClientConnection(this.Engine));
            this._cachedMetaIds = new(this.Engine.maxEntities);
            this._cachedObjectIds = new(this.Engine.maxEntities);
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

        public unsafe void SendServerPak()
        {
            ClientData[] clientDatas = this.Engine.ServerSimulation.clientDatas;
            Snapshot curSnapshot = this.Engine.WorldState.CurrentSnapshot;
            this._cachedMetaIds.Clear();
            this._cachedObjectIds.Clear();
            for (int i = 0; i < this.Engine.maxEntities; i++)
            {
                if (curSnapshot.dirtyObjectMetaMap[i] == 1)
                    this._cachedMetaIds.Add(i);
            }

            for (int i = 1; i < this.clientConnections.Count; i++)
            {
                if (this.clientConnections[i].connected)
                {
                    Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToClient);
                    msg.AddInt(this.Engine.simTick.tickValue); // Author Tick
                    msg.AddDouble(clientDatas[i].deltaPakTime); // pakTime
                    foreach (var id in _cachedMetaIds) // meta
                    {
                        NetworkObjectMeta meta = curSnapshot.worldObjectMeta[id];
                        msg.AddInt(id);
                        msg.AddInt(meta.networkId);
                        msg.AddInt(meta.prefabId);
                        msg.AddInt(meta.stateWordSize);
                        msg.AddBool(meta.destroyed);
                    }

                    msg.AddInt(-1); // meta写入终止符号
                    this.clientConnections[i].WritePacket(msg); // state
                    msg.AddInt(-1); // 状态写入终止符号
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
            bool clientPakLoss = msg.GetBool();
            int clientLastAuthorTick = msg.GetInt();
            int inputCount = msg.GetInt();
            ClientData clientData = this.clientConnections[args.FromConnection.Id].clientData;
            clientData.deltaPakTime = this.Engine.SimulationClock.Time - clientData.lastPakTime;
            clientData.lastPakTime = this.Engine.SimulationClock.Time;
            for (int i = 0; i < inputCount; i++)
            {
                int targetTick = msg.GetInt();
                if (targetTick <= clientData.LastTick.tickValue)
                {
                    continue;
                }

                // 优先保留旧输入，以免被冲掉
                if (clientData.clientInput.Count < clientData.maxClientInput)
                {
                    SimulationInput simulationInput =
                        this.Engine.ServerSimulation.CreateInput(Tick.InvalidTick, new Tick(targetTick));
                    clientData.ReciveInput(simulationInput);
                }
            }

            RiptideLogger.Log(LogType.Error,
                $"recv count:{inputCount}, actully input count get from pak:{this.clientConnections[args.FromConnection.Id].clientData.clientInput.Count}");

            // 1为ack，0为input。现在已经弃用，客户端只会发input，input即是ack
            // {
            //     int confirmTick = msg.GetInt();
            //     RiptideLogger.Log(LogType.Debug,
            //         $"Server Tick:{this.Engine.simTick}:" +
            //         $", Acked from {args.FromConnection.Id} at Tick {confirmTick}, RTT:{args.FromConnection.RTT}");
            // }
        }

        private void OnConnect(object sender, ServerConnectedEventArgs args)
        {
            ClientData clientData = this.Engine.ServerSimulation.clientDatas[args.Client.Id];
            clientData.Reset();
            clientConnections.Add(new ClientConnection(this.Engine)
                { connected = true, connection = args.Client, clientData = clientData });

            this.Engine.Monitor.connectedClients = this.clientConnections.Count;
        }
    }
}