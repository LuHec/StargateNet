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
        public List<ClientConnection> clientConnections; // 暂时先用List(有隐患，Riptide给Client的id是递增的，一个CLient断线重连后获得的id和以前不一样)
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
            this.clientConnections.Add(new ClientConnection());
            this._cachedMetaIds = new(this.Engine.maxNetworkRef);
            this._cachedObjectIds = new(this.Engine.maxNetworkRef);
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
            Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToClient);
            // 塞Tick
            msg.AddInt(this.Engine.simTick.tickValue);
            ClientData[] clientDatas = this.Engine.ServerSimulation.clientDatas;
            Snapshot curSnapshot = this.Engine.WorldState.CurrentSnapshot;
            this._cachedMetaIds.Clear();
            this._cachedObjectIds.Clear();
            for (int i = 0; i < this.Engine.maxNetworkRef; i++)
            {
                if (curSnapshot.dirtyObjectMetaMap[i] == 1)
                    this._cachedMetaIds.Add(i);
            }

            foreach (var pair in this.Engine.ObjectAllocator.pools)
            {
                int id = pair.Key;
                int mapSize = curSnapshot.worldObjectMeta[id].stateWordSize / 2;
                StargateAllocator.MemoryPool pool = pair.Value;
                void* data = pool.data;
                int* map = (int*)data;
                int* states = map + mapSize;
                for (int i = 0; i < mapSize; i++)
                {
                    if (map[i] == 1)
                    {
                        this._cachedObjectIds.Add(id);
                        break;
                    }
                }
            }

            for (int i = 1; i < this.clientConnections.Count; i++)
            {
                if (this.clientConnections[i].connected)
                {
                    // 塞pakTime
                    msg.AddDouble(clientDatas[i].deltaPakTime);
                    // meta
                    msg.AddInt(this._cachedMetaIds.Count); // 数量
                    foreach (var id in _cachedMetaIds)
                    {
                        NetworkObjectMeta meta = curSnapshot.worldObjectMeta[id];
                        msg.AddInt(meta.networkId);
                        msg.AddInt(meta.prefabId);
                        msg.AddInt(meta.stateWordSize);
                        msg.AddBool(meta.destroyed);
                    }
                    // sync var,meta为非destroyed的数据全放入

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
            ClientData clientData = this.Engine.ServerSimulation.clientDatas[args.Client.Id];
            clientData.Reset();
            clientConnections.Add(new ClientConnection()
                { connected = true, connection = args.Client, clientData = clientData });

            this.Engine.Monitor.connectedClients = this.clientConnections.Count;
        }
    }
}