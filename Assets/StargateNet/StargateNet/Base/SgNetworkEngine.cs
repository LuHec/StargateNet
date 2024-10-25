using System.Collections.Generic;
using System.Text;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public sealed class SgNetworkEngine
    {
        private SimulationClock _timer;
        internal float LastDeltaTime { get; private set; }
        internal float LastTimeScale { get; private set; }
        internal StargateConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }
        internal SgPeer Peer { get; private set; }
        internal SgClientPeer ClientPeer { get; private set; }
        internal SgServerPeer ServerPeer { get; private set; }
        internal InterestManager IM { get; private set; }
        internal Simulation Simulation { get; private set; }
        internal ServerSimulation ServerSimulation { get; private set; }
        internal ClientSimulation ClientSimulation { get; private set; }
        internal bool Simulated { get; private set; }
        internal bool IsServer => Peer.IsServer;
        internal bool IsClient => Peer.IsClient;
        internal bool IsConnected { get; private set; }
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
            this._timer = new SimulationClock(this, FixedUpdate);
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
            this._timer.PreUpdate();
            this._timer.Update();
        }

        /// <summary>
        /// Called every frame, after Step
        /// </summary>
        internal void Render()
        {
            if (!IsRunning) return;
            // Server can also has rendering
            if (this.IsServer || this.IsConnected)
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

            // 服务器和已连接的客户端都需要跑
            if (this.IsServer || this.IsConnected)
                this.Simulation.FixedUpdate();

            string message = this.IsServer ? "Server" : "Client";
            this.Peer.SendMessageUnreliable(Encoding.UTF8.GetBytes($"From {message} At {this._timer.Time}"));
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