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
        internal SgNetConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }
        internal SgTransport Transport { get; private set; }
        internal InterestManager IM { get; private set; }
        internal Simulation Simulation { get; private set; }
        internal ServerSimulation ServerSimulation { get; private set; }
        internal ClientSimulation ClientSimulation { get; private set; }
        internal bool Simulated { get; private set; }
        internal bool IsServer => Transport.IsServer;
        internal bool IsClient => Transport.IsClient;
        internal bool IsConnected { get; private set; }

        public SgNetworkEngine()
        {
        }

        public void Start(StartMode startMode, SgNetConfigData sgNetConfigData, ushort port)
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            this.ConfigData = sgNetConfigData;
            this._timer = new SimulationClock(this, Step);
            if (startMode == StartMode.Server)
            {
                SgServerTransport serverTransport = new SgServerTransport(sgNetConfigData);
                serverTransport.StartServer(port, sgNetConfigData.maxClientCount);
                this.Transport = serverTransport;
                this.ServerSimulation = new ServerSimulation(this);
                this.Simulation = this.ServerSimulation;
            }
            else
            {
                this.Transport = new SgClientTransport(sgNetConfigData);
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
            ((SgServerTransport)this.Transport).StartServer(port, maxClient);
        }

        public void Connect(string ip, ushort port)
        {
            if (!this.IsRunning) return;
            ((SgClientTransport)this.Transport).Connect(ip, port);
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        public void Update(float deltaTime, float timeScale)
        {
            if (!this.IsRunning) return;

            this.LastDeltaTime = deltaTime;
            this.LastTimeScale = timeScale;
            this.Simulation.PreUpdate();
            this.Transport.NetworkUpdate();
            this.Simulation.ExecuteNetworkUpdate();
            this._timer.PreUpdate();
            this._timer.Update();
        }

        /// <summary>
        /// Called every frame, after Step
        /// </summary>
        public void Render()
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
        private void Step()
        {
            if (!this.IsRunning)
                return;
            
            if(this.IsServer || this.IsConnected)
                this.Simulation.Step();
            
            string message = this.IsServer ? "Server" : "Client";
            this.Transport.SendMessage($"From {message} At {this._timer.Time}");
        }
    }
}