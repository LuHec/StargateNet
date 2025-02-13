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

        internal List<ClientConnection> clientConnections; // 暂时先用List(有隐患，Riptide给Client的id是递增的，一个CLient断线重连后获得的id和以前不一样)

        private Queue<int> _connectionId2Reuse = new(16);
        private List<int> _cachedMetaIds;
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

        public unsafe void SendServerPak()
        {
            PrepareToSend();
            Snapshot curSnapshot = this.Engine.WorldState.CurrentSnapshot;
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
                this._writeBuffer.Reset();
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
                int fragmentId = 0;
                int fragmentCount = ((int)this._writeBuffer.GetUsedBytes() + MTU - 1) / MTU;
                int lastFragmentSize = 0;
                Debug.Log($"Tick{authorTick},total:{_writeBuffer.GetUsedBytes()}");
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
                    msg.AddInt(authorTick.tickValue); // server tick作为Header
                    msg.AddInt(fragmentBytes);
                    msg.AddInt(lastFragmentSize);
                    // msg.AddShort((short)fragmentCount);
                    msg.AddShort((short)fragmentId ++);
                    msg.AddBool(fragmentId == fragmentCount);
                    int temp = fragmentBytes;
                    while (temp -- > 0)
                    {
                        byte bt = _writeBuffer.GetByte();
                        msg.AddByte(bt);
                    }
                    this.Server.Send(msg, (ushort)i);
                    clientConnection.clientData.isFirstPak = false;
                    this.bytesOut.Add(msg.BytesInUse);
                    lastFragmentSize += fragmentBytes;
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
            this._writeBuffer.Reset();
        }
        
        private void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            PrepareToReceive();
            var msg = args.Message;
            this.bytesIn.Add(msg.BytesInUse);
            // header ------------------------
            bool clientLossPacket = msg.GetBool();
            int clientLastAuthorTick = msg.GetInt();
            int inputCount = msg.GetShort();
            RiptideLogger.Log(LogType.Warning, $"client send input count: {inputCount}");
            ClientData clientData = this.clientConnections[args.FromConnection.Id].clientData;
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
                    SimulationInput.InputBlock inputBlock = new()
                    {
                        type = msg.GetShort(),
                        input = new NetworkInput
                        {
                            Input = new Vector2
                            {
                                x = msg.GetFloat(),
                                y = msg.GetFloat(),
                            },
                            YawPitch = new Vector2
                            {
                                x = msg.GetFloat(),
                                y = msg.GetFloat(),
                            },
                            IsJump = msg.GetBool(),
                            IsFire = msg.GetBool(),
                            IsInteract = msg.GetBool(),
                        }
                    };

                    simulationInput.AddInputBlock(inputBlock);
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
            ClientData clientData = this.Engine.ServerSimulation.clientDatas[args.Client.Id];
            clientData.Reset();
            ClientConnection clientConnection = new ClientConnection(this.Engine)
                { connected = true, connection = args.Client, clientData = clientData };
            clientConnections.Add(clientConnection);
            args.Client.TimeoutTime = 50 * 1000;
            this.Engine.Monitor.connectedClients = this.clientConnections.Count - 1; // 有一个idx为0的占位
            this.Engine.NetworkEventManager.OnPlayerConnected(this.Engine.SgNetworkGalaxy, args.Client.Id);
        }

        private void OnDisConnect(object sender, ServerDisconnectedEventArgs args)
        {
            Connection connection = args.Client;
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
    }
}