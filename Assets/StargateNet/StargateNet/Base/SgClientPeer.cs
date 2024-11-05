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
        public override bool IsServer => false;
        public override bool IsClient => true;
        public string ServerIP { private set; get; }
        public ushort Port { private set; get; }
        public Client Client { private set; get; }
        public bool HeavyPakLoss { get; set; }

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

            var ds = this.Engine.ClientSimulation.snapShots;
            var msg = args.Message;
            if (msg.UnreadBits < 8 * sizeof(int)) return;
            Tick srvtick = new Tick(msg.GetInt());
            this.Engine.ClientSimulation.OnRcvPak(srvtick);
            Ack(srvtick);
            this.Engine.ClientSimulation.serverInputRcvTimeAvg = msg.GetDouble();
            // 接收NetworkObject
            int maxNetworkRef = msg.GetInt();
            int[] srvMap = new int[maxNetworkRef];
            for (int i = 0; i < maxNetworkRef / 32; i++)
            {
                srvMap[i] = msg.GetInt();
            }

            for (int i = 0; i < maxNetworkRef / 32; i++)
            {
                int delta = this.Engine.networkRefMap[i] | srvMap[i];
                int idx = 0;
                while (delta > 0)
                {
                    if ((delta & 1) == 1)
                    {
                        Dictionary<int, NetworkObject> prefabsTable = this.Engine.PrefabsTable;
                        Dictionary<NetworkObjectRef, NetworkObject> networkObjectsTable = this.Engine.NetworkObjectsTable;
                        NetworkObjectRef networkObjectRef = new NetworkObjectRef(i * 32 + idx);
                        int prefabId = msg.GetInt();
                        if (!networkObjectsTable.ContainsKey(networkObjectRef) &&
                            prefabsTable.ContainsKey(prefabId))
                        {
                            // 生成
                            var networkObject =  this.Engine.ObjectSpawner.Spawn(this.Engine.PrefabsTable[prefabId].gameObject, Vector3.zero,
                                Quaternion.identity).GetComponent<NetworkObject>();
                            networkObjectsTable.Add(networkObjectRef, networkObject);
                        }
                    }

                    idx++;
                    delta >>= 1;
                }
            }
            Client
        }

        public void SendClientPak()
        {
            Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToServer);
            msg.AddBits(0, 1);
            Tick clientTick = this.Engine.simTick;
            // 发送ACK到的Tick后所有的输入    
            List<SimulationInput> clientInputs = this.Engine.ClientSimulation.inputs;
            msg.AddInt(clientInputs.Count);
            for (int i = 0; i < clientInputs.Count; i++)
            {
                msg.AddInt(clientInputs[i].targetTick.tickValue);
            }

            this.Client.Send(msg);
        }
    }
}