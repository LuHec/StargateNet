using System;
using System.Collections.Generic;
using Riptide;
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
        private ReadWriteBuffer _readBuffer;
        private ReadWriteBuffer _fragmentBuffer;
        private List<int> _fragmentIndex = new List<int>(8);
        private int _fragmentCount = -1;

        public SgClientPeer(StargateEngine engine, StargateConfigData configData) : base(engine, configData)
        {
            this.Client = new Client();
            this.Client.ConnectionFailed += this.OnConnectionFailed;
            this.Client.Connected += this.OnConnected;
            this.Client.ClientDisconnected += this.OnDisConnected;
            this.Client.MessageReceived += this.OnReceiveMessage;
            this._readBuffer = new ReadWriteBuffer(configData.maxSnapshotSendSize);
            this._fragmentBuffer = new ReadWriteBuffer(MTU + 300);
        }


        public void Connect(string serverIP, ushort port)
        {
            this.ServerIP = serverIP;
            this.Port = port;
            this.Client.Connect($"{ServerIP}:{Port}", useMessageHandlers: false);
            RiptideLogger.Log(LogType.Info, "Client Connecting");
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
            this.Engine.IsConnected = true;
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
            this._fragmentBuffer.Reset();
            // 更新数据
            this.bytesIn.Add(args.Message.BytesInUse);
            this.HeavyPakLoss = false;
            this.PakLoss = false;

            // 收包
            var msg = args.Message;
            // ------------------------------------Msg Header ------------------------------------
            Tick srvTick = new Tick(msg.GetInt());
            if (srvTick < this.Engine.ClientSimulation.authoritativeTick) return;
            if (srvTick > this.Engine.ClientSimulation.authoritativeTick)
            {
                this._readBuffer.Reset();
                this._totalPacketBytes = 0;
            }
            int fragmentBytes = msg.GetInt();
            int lastFragmentBytes = msg.GetInt();
            int fragmentCount = msg.GetShort();
            int fragmentId = msg.GetShort();
            this._fragmentCount = fragmentCount;
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
            if (this._fragmentCount != this._fragmentIndex.Count) return;
            this._fragmentCount = 0;
            this._fragmentIndex.Clear();
            
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

        /// <summary>
        /// 客户端发送输入。TODO：当前的输入是暂时用来测试的，只有一个类型，后续会改成unmanaged，支持任意类型写入
        /// </summary>
        public void SendClientPak()
        {
            Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToServer);
            // 有没有丢ds包，如果丢包了就要求服务端发从上一次客户端收到的authorTick之后的所有包
            msg.AddBool(this.PakLoss);
            msg.AddInt(this.Engine.ClientSimulation.authoritativeTick.tickValue);
            // 发送ACK到的Tick后所有的输入    
            // TODO:修改，优先发送最新的输入，并设置发送上限
            ClientSimulation clientSimulation = this.Engine.ClientSimulation;
            List<SimulationInput> clientInputs = clientSimulation.inputs;
            int threhold = Mathf.Max(clientInputs.Count - 5, 0);
            msg.AddShort((short)(clientInputs.Count - threhold));
            for (int index = clientInputs.Count - 1; index >= threhold; index--)
            {
                //TODO:多余了
                msg.AddInt(clientInputs[index].clientAuthorTick.tickValue);
                msg.AddInt(clientInputs[index].clientTargetTick.tickValue);
                msg.AddFloat(clientInputs[index].clientInterpolationAlpha);
                msg.AddInt(clientInputs[index].clientRemoteFromTick.tickValue);
                msg.AddShort((short)clientInputs[index].inputBlocks.Count);
                // 写入Input，暂时只有NetworkInput
                var blocks = clientInputs[index].inputBlocks;
                for (int j = 0; j < blocks.Count; j++)
                {
                    SimulationInput.InputBlock inputBlock = blocks[j];
                    NetworkInput networkInput = (NetworkInput)inputBlock.input;
                    msg.AddShort(inputBlock.type);
                    msg.AddFloat(networkInput.Input.x);
                    msg.AddFloat(networkInput.Input.y);
                    msg.AddFloat(networkInput.YawPitch.x);
                    msg.AddFloat(networkInput.YawPitch.y);
                    msg.AddBool(networkInput.IsJump);
                    msg.AddBool(networkInput.IsFire);
                    msg.AddBool(networkInput.IsInteract);
                }
            }

            this.Client.Send(msg);
            this.bytesOut.Add(msg.BytesInUse);
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
            // 外层是找meta，内层找object state
            // while (true)
            // {
            //     int worldMetaIdx = msg.GetInt();
            //     if (worldMetaIdx < 0) break;
            //     int networkId = this.Engine.WorldState.CurrentSnapshot.GetWorldObjectMeta(worldMetaIdx).networkId;
            //     Entity entity = this.Engine.Simulation.entitiesTable[new NetworkObjectRef(networkId)];
            //     while (true)
            //     {
            //         int dirtyStateId = msg.GetInt();
            //         if (dirtyStateId < 0) break;
            //         int data = msg.GetInt();
            //         entity.SetState(dirtyStateId, data); // 客户端直接设置即可，dirty没什么用
            //     }
            // }
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