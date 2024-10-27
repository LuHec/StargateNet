using System;
using System.Collections.Generic;
using System.Text;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public sealed class SgNetworkEngine
    {
        internal SimulationClock timer;
        internal float LastDeltaTime { get; private set; }
        internal float LastTimeScale { get; private set; }
        internal StargateConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }
        internal SgPeer Peer { get; private set; }
        internal SgClientPeer ClientPeer { get; private set; }
        internal SgServerPeer ServerPeer { get; private set; }
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
                this.ServerPeer = new SgServerPeer(this, stargateConfigData);
                this.Peer = this.ServerPeer;
                this.ServerSimulation = new ServerSimulation(this);
                this.Simulation = this.ServerSimulation;
                this.ServerPeer.StartServer(port, stargateConfigData.maxClientCount);
            }
            else
            { 
                this.ClientPeer = new SgClientPeer(this, stargateConfigData);
                this.Peer = this.ClientPeer;
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
            this.ServerPeer.StartServer(port, maxClient);
        }

        public void Connect(string ip, ushort port)
        {
            if (!this.IsRunning) return;
            this.ClientPeer.Connect(ip, port);
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
            this.Simulation.PreUpdate();
            this.Peer.NetworkUpdate();
            this.Simulation.ExecuteNetworkUpdate();
            this.timer.PreUpdate();
            this.timer.Update();
        }

        /// <summary>
        /// Called every frame, after Step
        /// </summary>
        internal void Render()
        {
            if (!IsRunning) return;
            // Server can also have rendering
            if (this.IsServer || this.IsConnected)
            {
                this.Simulation.ExecuteNetworkRender();
            }
        }

        public int _simTick = 1; // 是客户端/服务端已经模拟的本地帧数，和同步无关
        /// <summary>
        /// Called every fixed time, after Update
        /// </summary>
        private void FixedUpdate()
        {
            if (!this.IsRunning)
                return;

            // 服务器和已连接的客户端都需要跑
            if (this.IsServer || this.IsConnected)
                this.Simulation.FixedUpdate();

            if (this.IsClient && this.IsConnected)
            {
                this.ClientPeer.SendMessageUnreliable(Encoding.UTF8.GetBytes((this._simTick ++).ToString()));   
            }
            else if(this.IsServer && this.ServerPeer.clientConnections.Count > 0)
            {

                this.ServerPeer.SendMessageUnreliable(1, Encoding.UTF8.GetBytes((this._simTick).ToString()));
                if(this.ServerPeer.clientConnections.ContainsKey(2))
                    this.ServerPeer.SendMessageUnreliable(2, Encoding.UTF8.GetBytes((this._simTick).ToString()));
                _simTick++;
            }
            // 接收是Update的，发snapshot则是帧的
            Send();
        }

        private void Send()
        {
        }

        private void Recive()
        {
        }
    }
}