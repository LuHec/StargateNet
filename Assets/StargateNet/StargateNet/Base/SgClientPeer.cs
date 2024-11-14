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

        public SgClientPeer(StargateEngine engine, StargateConfigData configData) : base(engine, configData)
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
            this.Engine.Monitor.rtt = this.Client.RTT;
            this.Engine.Monitor.smothRTT = this.Client.SmoothRTT;
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
            RiptideLogger.Log(LogType.Debug, "Client Connect Failed");
        }


        /// <summary>
        /// 客户端只会收到DS
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private unsafe void OnReceiveMessage(object sender, MessageReceivedEventArgs args)
        {
            this.HeavyPakLoss = false;
            this.PakLoss = false;
            var msg = args.Message;
            
            Tick srvTick = new Tick(msg.GetInt());
            if (!this.Engine.ClientSimulation.OnRcvPak(srvTick))
            {
                this.PakLoss = true;
                return;
            }
            
            this.Engine.ClientSimulation.serverInputRcvTimeAvg = msg.GetDouble();
            this.ReceiveMeta(msg); // 接收meta
            this.Engine.EntityMetaManager.OnMetaChanged(); // 处理改变的meta
            this.ReceiveState(msg); // 接收state
            // // 接收NetworkObject
            // int maxNetworkRef = msg.GetInt();
            // int[] srvMap = new int[maxNetworkRef];
            // for (int i = 0; i < maxNetworkRef / 32; i++)
            // {
            //     srvMap[i] = msg.GetInt();
            // }
            //
            // for (int i = 0; i < maxNetworkRef / 32; i++)
            // {
            //     int delta = this.Engine.Simulation.networkRefMap[i] | srvMap[i];
            //     int idx = 0;
            //     while (delta > 0)
            //     {
            //         if ((delta & 1) == 1)
            //         {
            //             Dictionary<int, NetworkObject> prefabsTable = this.Engine.PrefabsTable;
            //             Dictionary<NetworkObjectRef, NetworkObject> networkObjectsTable = this.Engine.Simulation.EntitiesTable;
            //             NetworkObjectRef networkObjectRef = new NetworkObjectRef(i * 32 + idx);
            //             int prefabId = msg.GetInt();
            //             if (!networkObjectsTable.ContainsKey(networkObjectRef) &&
            //                 prefabsTable.ContainsKey(prefabId))
            //             {
            //                 // 生成
            //                 var networkObject =  this.Engine.ObjectSpawner.Spawn(this.Engine.PrefabsTable[prefabId].gameObject, Vector3.zero,
            //                     Quaternion.identity).GetComponent<NetworkObject>();
            //                 networkObjectsTable.Add(networkObjectRef, networkObject);
            //             }
            //         }
            //
            //         idx++;
            //         delta >>= 1;
            //     }
            // }
            // Client
        }

        public void SendClientPak()
        {
            Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToServer);
            // 有没有丢ds包
            msg.AddBool(this.PakLoss);
            msg.AddInt(this.Engine.ClientSimulation.authoritativeTick.tickValue);
            // 发送ACK到的Tick后所有的输入    
            List<SimulationInput> clientInputs = this.Engine.ClientSimulation.inputs;
            msg.AddInt(clientInputs.Count);
            for (int i = 0; i < clientInputs.Count; i++)
            {
                msg.AddInt(clientInputs[i].targetTick.tickValue);
            }

            this.Client.Send(msg);
        }

        private void ReceiveMeta(Message msg)
        {
            while (true)
            {
                int wordMetaIdx = msg.GetInt();
                if (wordMetaIdx < 0) break;
                
                int networkId = msg.GetInt();
                int prefabId = msg.GetInt();
                int stateWordSize = msg.GetInt();
                bool destroyed = msg.GetBool();
                this.Engine.EntityMetaManager.changedMetas.TryAdd(wordMetaIdx, new NetworkObjectMeta()
                {
                    networkId = networkId,
                    prefabId = prefabId,
                    stateWordSize = stateWordSize,
                    destroyed = destroyed
                });
            }
        }
        
        private unsafe void ReceiveState(Message msg)
        {
            // 外层是找meta，内层找object state
            while (true)
            {
                int wordMetaIdx = msg.GetInt(); 
                if (wordMetaIdx < 0) break;
                int networkId = this.Engine.WorldState.CurrentSnapshot.worldObjectMeta[wordMetaIdx].networkId;
                Entity entity = this.Engine.Simulation.entitiesTable[new NetworkObjectRef(networkId)];
                int* targetState = entity.stateBlock;
                while (true)
                {
                    int dirtyStateId = msg.GetInt();
                    if (dirtyStateId < 0) break;
                    int data = msg.GetInt();
                    targetState[dirtyStateId] = data; // 客户端直接设置即可，dirty没什么用
                }
            }
        }
    }
}