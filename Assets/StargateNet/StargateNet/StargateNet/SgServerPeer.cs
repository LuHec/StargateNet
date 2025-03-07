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
        internal List<ClientConnection> clientConnections;
        private int _idCounter = 1;
        private Dictionary<int, int> _guidToIdMap = new Dictionary<int, int>(512);
        private Dictionary<int, int> _clinetIdToGuidMap = new Dictionary<int, int>(512);
        private HashSet<int> _pendingConnectionIds = new HashSet<int>(512);
        private Queue<int> _connectionId2Reuse = new(16);
        private HashSet<int> _cachedMetaIds = new(128); // 改用HashSet
        private List<SimulationInput> _cachedInputs = new List<SimulationInput>(128);
        private ReadWriteBuffer _writeBuffer;

        public SgServerPeer(StargateEngine engine, StargateConfigData configData) : base(engine, configData)
        {
            this.Server = new Server();
            this.Server.ClientConnected += this.OnConnect;
            this.Server.MessageReceived += this.OnReceiveMessage;
            this.Server.ClientDisconnected += this.OnDisConnect;
            this._writeBuffer = new ReadWriteBuffer(configData.maxSnapshotSendSize);
        }

        public void StartServer(ushort port, ushort maxClientCount)
        {
            this.Port = port;
            this.MaxClientCount = maxClientCount;
            this.Server.Start(port, maxClientCount, useMessageHandlers: false);
            this.clientConnections = new List<ClientConnection>(maxClientCount);
            this.clientConnections.Add(new ClientConnection(this.Engine)); // 用来占位的，connection从1开始
            this._cachedMetaIds = new(this.Engine.maxEntities);
            RiptideLogger.Log(LogType.Debug, "Server Start");
        }

        internal override void NetworkUpdate()
        {
            this.Server.Update();
            this.bytesIn.Update(this.Engine.SimulationClock.InternalUpdateTime);
            this.bytesOut.Update(this.Engine.SimulationClock.InternalUpdateTime);
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

        public unsafe void SendRpc()
        {
            if (!this.Engine.NetworkRPCManager.NeedSendRpc) return;
            for (int id = 1; id < clientConnections.Count; id++)
            {
                Message msg = Message.Create(MessageSendMode.Reliable, Protocol.ToServer);
                msg.AddUInt((uint)ToServerProtocol.Rpc);
                msg.AddShort((short)this.Engine.NetworkRPCManager.pramsToSend.Count);
                for (int i = 0; i < this.Engine.NetworkRPCManager.pramsToSend.Count; i++)
                {
                    NetworkRPCPram pram = this.Engine.NetworkRPCManager.pramsToSend[i];
                    msg.AddInt(pram.entityId);
                    msg.AddInt(pram.scriptId);
                    msg.AddInt(pram.rpcId);
                    msg.AddInt(pram.pramsBytes);
                    int t = 0;
                    while (t < pram.pramsBytes)
                    {
                        msg.AddByte(pram.prams[t]);
                        t++;
                    }
                }

                this.Server.Send(msg, (ushort)id);
                this.bytesOut.Add(msg.BytesInUse);
            }
            // 清除发送的RPC
            this.Engine.NetworkRPCManager.ClearSendedRpc();
        }

        public unsafe void SendServerPak()
        {
            PrepareToSend();
            Snapshot curSnapshot = this.Engine.WorldState.CurrentSnapshot;

            // 收集dirty的meta
            for (int idx = 0; idx < this.Engine.maxEntities; idx++)
            {
                if (curSnapshot.IsWorldMetaDirty(idx))
                    this._cachedMetaIds.Add(idx);
            }

            for (int i = 1; i < this.clientConnections.Count; i++)
            {
                ClientConnection clientConnection = this.clientConnections[i];
                if (!clientConnection.connected) continue;
                clientConnection.PrepareToWrite();
                this._writeBuffer.Clear();
                // 找到该客户端控制的玩家实体
                if (this.Engine.Simulation.entitiesTable.TryGetValue(
                        clientConnection.controlEntityRef,
                        out Entity playerEntity))
                {
                    // 计算该客户端的可见对象
                    clientConnection.CalculateVisibleObjects(this.Engine.IM, playerEntity);
                }
                // 判断是否发送多帧包。现在是只要客户端不回话，服务端就会一直发多帧包
                bool isMultiPak = clientConnection.clientData.isFirstPak || clientConnection.clientData.pakLoss;
                Tick authorTick = this.Engine.SimTick;
                Tick lastAckedAuthorTick = clientConnection.clientData.clientLastAuthorTick;
                // ------------------ Data ------------------ 除了ServerTick以外，都不会作为Header
                this._writeBuffer.AddInt(lastAckedAuthorTick.tickValue);
                this._writeBuffer.AddInt(clientConnection.clientData.LastTargetTick.tickValue);
                this._writeBuffer.AddDouble(clientConnection.clientData.deltaPakTime); // 两次收到客户端包的间隔
                this._writeBuffer.AddBool(isMultiPak);
                clientConnection.WriteMeta(this._writeBuffer, isMultiPak, _cachedMetaIds);
                clientConnection.WriteState(this._writeBuffer, isMultiPak);
                // 分包
                int fragmentCount = ((int)this._writeBuffer.GetUsedBytes() + MTU - 1) / MTU;
                short fragmentId = 1;
                if (fragmentCount == 1) fragmentId = -1; // -1表示单个包
                int lastFragmentSize = 0;
                // Debug.Log($"Tick{authorTick},total:{_writeBuffer.GetUsedBytes()}，{fragmentCount} packets");
                while (!this._writeBuffer.ReadEOF())
                {
                    // ------------------ Header ------------------
                    // long bytesRead = this._writeBuffer.BytesReadPosition(); // 这里只会发送完整的字节，所以可以无视bits
                    int sendRemainBytes = (int)this._writeBuffer.GetUsedBytes() - lastFragmentSize;
                    int fragmentBytes = sendRemainBytes >= MTU ? MTU : sendRemainBytes;
                    if (fragmentBytes == 0)
                    {
                        break;
                    }

                    Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToClient);
                    msg.AddUInt((uint)ToClientProtocol.Snapshot);
                    msg.AddInt(authorTick.tickValue); // server tick作为Header
                    msg.AddInt(fragmentBytes);
                    msg.AddInt(lastFragmentSize);
                    // msg.AddShort((short)fragmentCount);
                    msg.AddShort(fragmentId);
                    msg.AddBool(fragmentId == -1 || fragmentId == fragmentCount);
                    int temp = fragmentBytes;
                    while (temp-- > 0)
                    {
                        byte bt = _writeBuffer.GetByte();
                        msg.AddByte(bt);
                    }

                    this.Server.Send(msg, (ushort)i);
                    clientConnection.clientData.isFirstPak = false;
                    this.bytesOut.Add(msg.BytesInUse);
                    lastFragmentSize += fragmentBytes;
                    fragmentId++;
                }
            }
        }

        public override void Disconnect()
        {
            this.Server.Stop();
        }

        private void PrepareToReceive()
        {
            this._cachedInputs.Clear();
        }

        private void PrepareToSend()
        {
            this._cachedMetaIds.Clear();
            this._writeBuffer.Clear();
        }

        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            Message msg = args.Message;
            ToServerProtocol protocol = (ToServerProtocol)msg.GetUInt();
            if (protocol == ToServerProtocol.ConnectRequest)
            {
                AckClientConnectRequest(args);
            }
            else if (protocol == ToServerProtocol.Input)
            {
                OnReceiveInput(args);
            }
            else if (protocol == ToServerProtocol.Rpc)
            {
                OnReceiveRpc(msg);
            }
            this.bytesIn.Add(msg.BytesInUse);
        }

        private unsafe void OnReceiveRpc(Message msg)
        {
            NetworkRPCManager networkRPCManager = this.Engine.NetworkRPCManager;
            int rpcCount = msg.GetUShort();
            while (rpcCount-- > 0)
            {
                int entityId = msg.GetInt();
                int scriptId = msg.GetInt();
                int rpcId = msg.GetInt();
                int pramBytes = msg.GetInt();
                NetworkRPCPram networkRPCPram = networkRPCManager.RequireRpcPramToReceive(pramBytes);
                networkRPCPram.entityId = entityId;
                networkRPCPram.scriptId = scriptId;
                networkRPCPram.rpcId = rpcId;
                networkRPCPram.pramsBytes = pramBytes;
                int t = 0;
                while (t < pramBytes)
                {
                    networkRPCPram.prams[t] = msg.GetByte();
                    t++;
                }
                networkRPCManager.AddRpcPramToReceive(networkRPCPram);
            }
        }

        private void OnReceiveInput(MessageReceivedEventArgs args)
        {
            PrepareToReceive();
            var msg = args.Message;
            // header ------------------------
            bool clientLossPacket = msg.GetBool();
            int clientLastAuthorTick = msg.GetInt();
            int inputCount = msg.GetShort();
            if (!this._clinetIdToGuidMap.TryGetValue(args.FromConnection.Id, out int guid)) return;
            int playerId = this._guidToIdMap[guid];
            if (playerId >= this.clientConnections.Count) return;
            ClientConnection connection = this.clientConnections[playerId];
            if (connection == null || connection.clientData == null) return;
            ClientData clientData = this.clientConnections[playerId].clientData;
            clientData.deltaPakTime = this.Engine.SimulationClock.Time - clientData.lastPakTime;
            clientData.lastPakTime = this.Engine.SimulationClock.Time;
            clientData.clientLastAuthorTick = new Tick(clientLastAuthorTick);
            clientData.pakLoss = clientLossPacket;
            // input ------------------------
            for (int i = 0; i < inputCount; i++)
            {
                int authorTick = msg.GetInt();
                int targetTick = msg.GetInt();
                float alpha = msg.GetFloat();
                int remoteFromTick = msg.GetInt();
                int inputBlockCount = msg.GetShort();
                SimulationInput simulationInput =
                    this.Engine.ServerSimulation.CreateInput(new Tick(authorTick), new Tick(targetTick), alpha, new Tick(remoteFromTick));
                while (inputBlockCount-- > 0)
                {
                    int inputType = msg.GetInt();
                    this.Engine.Simulation.WriteInputBlock(simulationInput, inputType, msg);
                }

                this._cachedInputs.Add(simulationInput);
            }

            for (int i = this._cachedInputs.Count - 1; i >= 0; i--)
            {
                var simulationInput = this._cachedInputs[i];
                if (!clientData.ReceiveInput(simulationInput))
                {
                    this.Engine.Simulation.RecycleInput(simulationInput);
                }
            }
        }

        private void OnConnect(object sender, ServerConnectedEventArgs args)
        {
            this._pendingConnectionIds.Add(args.Client.Id);

            // ClientData clientData = this.Engine.ServerSimulation.clientDatas[args.Client.Id];
            // clientData.Reset();
            // ClientConnection clientConnection = new ClientConnection(this.Engine)
            //     { connected = true, connection = args.Client, clientData = clientData };
            // _clientConnections.Add(clientConnection);
            // args.Client.TimeoutTime = 50 * 1000;
            // this.Engine.Monitor.connectedClients = this._clientConnections.Count - 1; // 有一个idx为0的占位
            // this.Engine.NetworkEventManager.OnPlayerConnected(this.Engine.SgNetworkGalaxy, args.Client.Id);
        }

        private void OnDisConnect(object sender, ServerDisconnectedEventArgs args)
        {
            Connection connection = args.Client;
            if (connection.Id >= this.clientConnections.Count) return;
            ClientData clientData = this.Engine.ServerSimulation.clientDatas[connection.Id];
            ClientConnection clientConnection = clientConnections[connection.Id];
            clientData.Reset();
            clientConnection.Reset();
            this.Engine.Monitor.connectedClients--;
        }

        private void AckInput(Tick ackedTick, ushort clientId)
        {
            Message msg = Message.Create(MessageSendMode.Reliable, Protocol.ToClient);
            msg.AddBool(true);
            msg.AddInt(ackedTick.tickValue);
            this.Server.Send(msg, clientId);
        }

        private void AckClientConnectRequest(MessageReceivedEventArgs args)
        {
            Message msg = args.Message;
            Connection pendingConnection = args.FromConnection;
            int guid = msg.GetString().GetHashCode();
            if (this._guidToIdMap.TryGetValue(guid, out int playerId))
            {
                
                ClientConnection clientConnection = this.clientConnections[playerId];
                ClientData clientData = this.Engine.ServerSimulation.clientDatas[playerId];
                if (clientConnection.connected && clientConnection.connection.Id != pendingConnection.Id) // 防止重复连接，直接把原来的连接踢掉
                {
                    // this.Server.DisconnectClient(clientConnection.connection);
                }
                // Debug.LogError($"Same Id{playerId}, befor{clientConnection.connection.Id}, now{pendingConnection.Id}"); 
                clientData.Reset();
                clientConnection.connected = true;
                clientConnection.connection = pendingConnection;
                clientConnection.clientData = clientData;
                // pendingConnection.TimeoutTime = 3 * 1000;
            }
            else
            {
                this._guidToIdMap[guid] = this._idCounter++;
                playerId = this._guidToIdMap[guid];
                ClientData clientData = this.Engine.ServerSimulation.clientDatas[playerId];
                clientData.Reset();
                ClientConnection clientConnection = new ClientConnection(this.Engine)
                { connected = true, connection = pendingConnection, clientData = clientData };
                clientConnections.Add(clientConnection);
                // pendingConnection.TimeoutTime = 3 * 1000;
            }

            this._clinetIdToGuidMap[pendingConnection.Id] = guid;
            Message replyMsg = Message.Create(MessageSendMode.Reliable, Protocol.ToClient);
            replyMsg.AddUInt((uint)ToClientProtocol.ConnectReply);
            pendingConnection.Send(replyMsg);
            this.Engine.NetworkEventManager.OnPlayerConnected(this.Engine.SgNetworkGalaxy, playerId);
            _pendingConnectionIds.Remove(pendingConnection.Id);
            CalculateClientCount();
        }

        private void CalculateClientCount()
        {
            int count = 0;
            for (int i = 0; i < this.clientConnections.Count; i++)
            {
                if (this.clientConnections[i].connected) count++;
            }

            this.Engine.Monitor.connectedClients = count;
        }
    }
}