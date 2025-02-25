using System;
using System.Collections.Generic;
using Riptide;
using Riptide.Transports.Udp;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public class SgClientPeer : SgPeer
    {
        internal override bool IsServer => false;
        internal override bool IsClient => true;
        internal string ServerIP { private set; get; }
        internal ushort Port { private set; get; }
        internal Client Client { private set; get; }
        internal bool HeavyPakLoss { get; set; }
        internal bool PakLoss { private set; get; }
        internal Tick LastReceivedTick { private set; get; }
        private readonly string _clientGuid;
        private ReadWriteBuffer _readBuffer;
        private ReadWriteBuffer _fragmentBuffer;
        private List<int> _fragmentIndex = new List<int>(8);
        /// <summary>
        /// 可以通过id值来推算总的分片数量，比如到达id为2，不是最后一个分片，那期望的就是3，以此类推，如果是最后i一个分片，id为6，那期望的分片就是6
        /// </summary>
        private int _expectedFragmentCount = 1;

        public SgClientPeer(StargateEngine engine, StargateConfigData configData) : base(engine, configData)
        {
            this.Client = new Client();
            this.Client.ConnectionFailed += this.OnConnectionFailed;
            this.Client.Connected += this.OnConnected;
            this.Client.ClientDisconnected += this.OnDisConnected;
            this.Client.MessageReceived += this.OnReceiveMessage;
            this._readBuffer = new ReadWriteBuffer(configData.maxSnapshotSendSize);
            this._fragmentBuffer = new ReadWriteBuffer(MTU + 300);
            this.LastReceivedTick = Tick.InvalidTick;

            this._clientGuid = Guid.NewGuid().ToString();
        }


        public void Connect(string serverIP, ushort port)
        {
            this.ServerIP = serverIP;
            this.Port = port;
            this.Client.Connect($"{ServerIP}:{Port}", useMessageHandlers: false);
            UdpConnection udpConnection = (UdpConnection)this.Client.Connection;
            RiptideLogger.Log(LogType.Info, 
            $"Client Connecting,Local Address:{udpConnection.RemoteEndPoint.Address}:{udpConnection.RemoteEndPoint.Port}");
        }

        internal override void NetworkUpdate()
        {
            this.Client.Update();
            this.Engine.Monitor.rtt = this.Client.RTT;
            this.Engine.Monitor.smothRTT = this.Client.SmoothRTT;
            this.bytesIn.Update(this.Engine.SimulationClock.InternalUpdateTime);
            this.bytesOut.Update(this.Engine.SimulationClock.InternalUpdateTime);
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
            msg.AddBits(1, 1);
            msg.AddInt(srvTick.tickValue);
            this.Client.Send(msg);
        }

        private void OnConnected(object sender, EventArgs e)
        {
            RiptideLogger.Log(LogType.Debug, "Client Connected");
            SendRequestMessage();
        }

        private void OnConnectionFailed(object sender, ConnectionFailedEventArgs args)
        {
            RiptideLogger.Log(LogType.Debug, "Client Connect Failed,trying again");
            this.Client.Connect($"{ServerIP}:{Port}", useMessageHandlers: false);
        }

        private void OnDisConnected(object sender, ClientDisconnectedEventArgs e)
        {
            RiptideLogger.Log(LogType.Debug, "Client Connect break,trying again");
            this.Client.Connect($"{ServerIP}:{Port}", useMessageHandlers: false);
        }

        private int _totalPacketBytes = 0;

        /// <summary>
        /// 客户端只会收到DS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private unsafe void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            // 收包
            var msg = args.Message;
            ToClientProtocol protocol = (ToClientProtocol)msg.GetUInt();
            // 更新数据
            this.bytesIn.Add(args.Message.BytesInUse);
            this.HeavyPakLoss = false;
            this.PakLoss = false;
            if (protocol == ToClientProtocol.ConnectReply)
            {
                OnReceiveConnectReply();
            }
            else
            {
                OnReceiveSnapshot(msg);
            }
        }

        /// <summary>
        /// 客户端发送输入。TODO：当前的输入是暂时用来测试的，只有一个类型，后续会改成unmanaged，支持任意类型写入
        /// </summary>
        public unsafe void SendClientInput()
        {
            Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToServer);
            msg.AddUInt((uint)ToServerProtocol.Input);
            // 有没有丢ds包，如果丢包了就要求服务端发从上一次客户端收到的authorTick之后的所有包
            msg.AddBool(this.PakLoss);
            msg.AddInt(this.Engine.ClientSimulation.authoritativeTick.tickValue);
            // 发送ACK到的Tick后所有的输入    
            // TODO:修改，优先发送最新的输入，并设置发送上限
            ClientSimulation clientSimulation = this.Engine.ClientSimulation;
            List<SimulationInput> clientInputs = clientSimulation.inputs;
            int threhold = Mathf.Max(clientInputs.Count - 5, 0);
            msg.AddShort((short)(clientInputs.Count - threhold));
            for (int inputIndex = clientInputs.Count - 1; inputIndex >= threhold; inputIndex--)
            {
                //TODO:多余了
                msg.AddInt(clientInputs[inputIndex].clientAuthorTick.tickValue);
                msg.AddInt(clientInputs[inputIndex].clientTargetTick.tickValue);
                msg.AddFloat(clientInputs[inputIndex].clientInterpolationAlpha);
                msg.AddInt(clientInputs[inputIndex].clientRemoteFromTick.tickValue);
                msg.AddShort((short)clientInputs[inputIndex].inputBlocks.Count);
                // 写入Input，暂时只有NetworkInput
                var blocks = clientInputs[inputIndex].inputBlocks;
                for (int blockIdx = 0; blockIdx < blocks.Count; blockIdx++)
                {
                    // 只需要传入type和数据就行，输入大小服务端也有
                    InputBlock inputBlock = blocks[blockIdx];
                    int inputBytes = blocks[blockIdx].inputSizeBytes;
                    int inputType = blocks[blockIdx].type;
                    msg.AddInt(inputType);
                    for(int dataIdx = 0; dataIdx < inputBytes; dataIdx ++)
                    {
                        msg.AddByte(inputBlock.inputBlockPtr[dataIdx]);
                    }
                }
            }

            this.Client.Send(msg);
            this.bytesOut.Add(msg.BytesInUse);
        }

        private void SendRequestMessage()
        {
            Message msg = Message.Create(MessageSendMode.Reliable, Protocol.ToServer);
            msg.AddUInt((uint)ToServerProtocol.ConnectRequest);
            msg.AddString(this._clientGuid);
            this.Client.Send(msg);
        }

        private void OnReceiveConnectReply()
        {
            this.Engine.IsConnected = true;
        }

        private void OnReceiveSnapshot(Message msg)
        {
            this._fragmentBuffer.Clear();
            // ------------------------------------Msg Header ------------------------------------
            Tick srvTick = new Tick(msg.GetInt());
            if (srvTick < this.LastReceivedTick) return;
            if (srvTick > this.LastReceivedTick)
            {
                this._readBuffer.Clear();
                this._totalPacketBytes = 0;
                this._expectedFragmentCount = 1;
                this._fragmentIndex.Clear();
            }

            this.LastReceivedTick = srvTick;
            int fragmentBytes = msg.GetInt();
            int lastFragmentBytes = msg.GetInt();
            short fragmentId = msg.GetShort();
            bool isLastFragment = msg.GetBool();
            if (!isLastFragment) 
            {
                //不是最后一个分片，那么期望的分片数量就是id+1
                _expectedFragmentCount = Mathf.Max(fragmentId + 1, _expectedFragmentCount);
            }
            else
            {
                _expectedFragmentCount = fragmentId;
            }
            if (this._fragmentIndex.Contains(fragmentId)) return;
            _totalPacketBytes += fragmentBytes;
            this._fragmentIndex.Add(fragmentId);
            int temp = fragmentBytes;
            while (temp-- > 0)
            {
                byte bt = msg.GetByte();
                this._fragmentBuffer.AddByte(bt);
            }

            this._fragmentBuffer.CopyTo(this._readBuffer, lastFragmentBytes, fragmentBytes);
            // ------------------------------------ ReadBuffer Data ------------------------------------
            this._readBuffer.ResetRead();
            if (fragmentId != -1 && this._expectedFragmentCount != this._fragmentIndex.Count) return;// -1表示没有分包。在分包时，如果包数量不对就返回。
            Tick srvRcvedClientTick = new Tick(this._readBuffer.GetInt());
            Tick srvRcvedClientInputTick = new Tick(this._readBuffer.GetInt());
            this.Engine.ClientSimulation.serverInputRcvTimeAvg = this._readBuffer.GetDouble();
            bool isMultiPacket = this._readBuffer.GetBool();
            bool isFullPacket = this._readBuffer.GetBool();
            this.Engine.SimulationClock.OnRecvPak();
            if (!this.Engine.ClientSimulation.OnRcvPak(srvTick, srvRcvedClientTick, srvRcvedClientInputTick,
                    isMultiPacket, isFullPacket))
            {
                this.PakLoss = true;
                return;
            }

            // 用服务端下发的结果更新环形队列
            // if (!this.Engine.WorldState.HasInitialized) this.Engine.WorldState.Init(srvTick);
            Snapshot rcvBuffer = this.Engine.ClientSimulation.rcvBuffer;
            rcvBuffer.Init(srvTick);
            this.ReceiveMeta(this._readBuffer, isFullPacket);
            this.Engine.EntityMetaManager.OnMetaChanged(); // 处理改变的meta，处理服务端生成和销毁的物体
            this.CopyMetaToBuffer(srvTick);
            this.CopyFromStateToBuffer(); // 这一步也需要ChangedMeta
            this.ReceiveStateToBuffer(this._readBuffer); // 这里不直接把状态更新到WorldState中
            this.Engine.EntityMetaManager.PostChanged(); // 清除Changed
            this.Engine.WorldState.CurrentSnapshot.CleanMap(); // CurrentSnapshot将作为本帧的开始，必须要清理干净，否则下次收到包，delta就出错了
            this.Engine.WorldState.ClientUpdateState(srvTick, rcvBuffer); // 对于客户端来说FromTick才是权威，CurrentTick可以被修改
            this.Engine.InterpolationRemote.AddSnapshot(srvTick, rcvBuffer);
            this._totalPacketBytes = 0;
        }

        private void ReceiveMeta(ReadWriteBuffer readBuffer, bool isFullPak)
        {
            while (true)
            {
                int wordMetaIdx = readBuffer.GetInt();
                if (wordMetaIdx < 0) break;

                int networkId = readBuffer.GetInt();
                int prefabId = readBuffer.GetInt();
                int inputSource = readBuffer.GetInt();
                bool destroyed = readBuffer.GetBool();
                this.Engine.EntityMetaManager.changedMetas.TryAdd(wordMetaIdx, new NetworkObjectMeta()
                {
                    networkId = networkId,
                    prefabId = prefabId,
                    inputSource = inputSource,
                    destroyed = destroyed
                });
            }

            // 全量包的额外处理。服务端全量包只会发存在的物体，需要将所有服务端不存在的资源全部删除
            if (isFullPak)
            {
                for (int metaIdx = 0; metaIdx < this.Engine.ConfigData.maxNetworkObjects; metaIdx++)
                {
                    this.Engine.EntityMetaManager.changedMetas.TryAdd(metaIdx, NetworkObjectMeta.Invalid);
                }
            }
        }

        /// <summary>
        /// 把完整的内存构造拷贝到buffer
        /// </summary>
        private void CopyMetaToBuffer(Tick srvTick)
        {
            Snapshot buffer = this.Engine.ClientSimulation.rcvBuffer;
            Snapshot currentSnapshot = this.Engine.WorldState.CurrentSnapshot;
            currentSnapshot.CopyTo(buffer);
            buffer.snapshotTick = srvTick;
        }

        /// <summary>
        /// 拷贝上一server帧的数据(只拷贝meta存在的物体)。这一步之前新的Meta对应的状态是空的
        /// 如果是首次接收，那就不用这一步。状态会在Reconcile时写入CurrentSnapshot。
        /// 这一步以Buffer为基准，排除ChangedMeta,拷贝所有从上一帧继承下来的物体状态。
        /// </summary>
        private unsafe void CopyFromStateToBuffer()
        {
            Snapshot buffer = this.Engine.ClientSimulation.rcvBuffer;
            Snapshot fromSnapshot = this.Engine.WorldState.FromSnapshot;
            if (fromSnapshot == null) return;

            var changedMetas = this.Engine.EntityMetaManager.changedMetas;
            var destPools = buffer.NetworkStates.pools;
            var fromPools = fromSnapshot.NetworkStates.pools;
            var entitiesTable = this.Engine.Simulation.entitiesTable;
            foreach (var pair in entitiesTable)
            {
                NetworkObjectRef networkObjectRef = pair.Key;
                Entity entity = pair.Value;
                if (changedMetas.ContainsKey(entity.worldMetaId)) continue;

                int poolId = entity.poolId;
                // From和buffer的关系是服务端前后帧，同一个物体的poolId肯定是一致的
                int* fromStatePtr = (int*)fromPools[poolId].dataPtr + entity.entityBlockWordSize;
                int* destStatePtr = (int*)destPools[poolId].dataPtr + entity.entityBlockWordSize;

                for (int i = 0; i < entity.entityBlockWordSize; i++)
                {
                    destStatePtr[i] = fromStatePtr[i];
                }
            }
        }

        private unsafe void ReceiveStateToBuffer(ReadWriteBuffer readBuffer)
        {
            Snapshot buffer = this.Engine.ClientSimulation.rcvBuffer;
            while (true)
            {
                int worldMetaIdx = readBuffer.GetInt();
                if (worldMetaIdx < 0) break;
                int networkId = buffer.GetWorldObjectMeta(worldMetaIdx).networkId;
                Entity entity = this.Engine.Simulation.entitiesTable[new NetworkObjectRef(networkId)];
                int* dataPtr = (int*)buffer.NetworkStates.pools[entity.poolId].dataPtr + entity.entityBlockWordSize;
                while (true)
                {
                    int dirtyStateId = readBuffer.GetInt();
                    if (dirtyStateId < 0) break;
                    int data = readBuffer.GetInt();
                    dataPtr[dirtyStateId] = data;
                }
            }
        }
    }
}