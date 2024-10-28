using System;
using System.Collections.Generic;
using System.Text;
using Riptide;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public sealed class SgNetworkEngine
    {
        internal SimulationClock timer;
        internal Tick simTick = new Tick(10); // 是客户端/服务端已经模拟的本地帧数。客户端的simTick与同步无关，服务端的simtick会作为AuthorTick传给客户端
        internal float LastDeltaTime { get; private set; }
        internal float LastTimeScale { get; private set; }
        internal StargateConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }
        internal SgPeer Peer { get; private set; }
        internal SgClientPeer Client { get; private set; }
        internal SgServerPeer Server { get; private set; }
        internal bool IsServer => Peer.IsServer;
        internal bool IsClient => Peer.IsClient;
        internal InterestManager IM { get; private set; }
        internal Simulation Simulation { get; private set; }
        internal ServerSimulation ServerSimulation { get; private set; }
        internal ClientSimulation ClientSimulation { get; private set; }
        internal bool Simulated { get; private set; }
        internal bool IsConnected { get; set; }
        internal Dictionary<int, NetworkBehavior> networkBehaviors;
        internal Queue<int> paddingRemoveBehaviors;
        internal Queue<int> paddingAddSet;

        public SgNetworkEngine()
        {
        }

        public void Start(StartMode startMode, StargateConfigData stargateConfigData, ushort port)
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            this.ConfigData = stargateConfigData;
            this.timer = new SimulationClock(this, this.FixedUpdate);
            if (startMode == StartMode.Server)
            {
                this.Server = new SgServerPeer(this, stargateConfigData);
                this.Peer = this.Server;
                this.ServerSimulation = new ServerSimulation(this);
                this.Simulation = this.ServerSimulation;
                this.Server.StartServer(port, stargateConfigData.maxClientCount);
            }
            else
            {
                this.Client = new SgClientPeer(this, stargateConfigData);
                this.Peer = this.Client;
                this.ClientSimulation = new ClientSimulation(this);
                this.Simulation = this.ClientSimulation;
            }

            this.IM = new InterestManager();
            this.Simulated = true;
            this.IsRunning = true;
        }

        public void ServerStart(ushort port, ushort maxClient)
        {
            if (!this.IsRunning) return;
            this.Server.StartServer(port, maxClient);
        }

        public void Connect(string ip, ushort port)
        {
            if (!this.IsRunning) return;
            this.Client.Connect(ip, port);
        }

        internal void AddNetworkBehavior()
        {
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        internal void Update(float deltaTime, float timeScale)
        {
            if (!this.IsRunning) return;

            this.LastDeltaTime = deltaTime;
            this.LastTimeScale = timeScale;
            this.timer.PreUpdate();
            this.Peer.NetworkUpdate();
            this.Simulation.PreUpdate();
            this.Simulation.ExecuteNetworkUpdate();
            this.timer.Update();
        }

        /// <summary>
        /// Called every frame, after Step
        /// </summary>
        internal void Render()
        {
            if (!IsRunning) return;
            // Server can also have rendering
            if (this.IsServer && !this.ConfigData.runAsHeadless || this.IsConnected)
            {
                this.Simulation.ExecuteNetworkRender();
            }
        }

        /// <summary>
        /// Called every fixed time, after Update
        /// </summary>
        private void FixedUpdate()
        {
            if (!this.IsRunning)
                return;

            if (this.IsServer || (this.IsClient && this.IsConnected))
            {
                this.simTick++;
                // 对于客户端，先在这里处理回滚，然后再模拟下一帧
                this.Simulation.PreFixedUpdate();
                this.Simulation.FixedUpdate();
                if (this.IsServer)
                {
                    RiptideLogger.Log(LogType.Warning,
                        $"ServerTick:{this.simTick}, ClientInput targetTick:{this.ServerSimulation.currentInput.targetTick},input count:{this.ServerSimulation.clientInput.Count}");
                }
            }

            Send();
        }

        private void Send()
        {
            if (this.IsServer)
            {
                // Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToClient);
                // msg.AddInt(this.simTick.tickValue);
                // this.Server.SendMessageUnreliable(1, msg);
                Server.SendServerPak();
            }
            else if (this.IsConnected)
            {
                Client.SendClientPak();
            }
        }
    }
}