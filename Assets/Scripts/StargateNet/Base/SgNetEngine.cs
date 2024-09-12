using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public sealed class SgNetEngine
    {
        private SimulationClock _timer;
        internal float LastDeltaTime { get; private set; }
        internal float LastTimeScale { get; private set; }

        internal SgNetConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }

        internal SgTransport Transport { get; private set; }

        internal bool IsServer => Transport.IsServer;
        internal bool IsClient => Transport.IsClient;

        public SgNetEngine()
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
            }
            else
            {
                this.Transport = new SgClientTransport(sgNetConfigData);
            }
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

        public void Update(float deltaTime, float timeScale)
        {
            if (!this.IsRunning) return;

            this.LastDeltaTime = deltaTime;
            this.LastTimeScale = timeScale;
            
            this.Transport.NetworkUpdate();
            
            this._timer.PreUpdate();
            this._timer.Update();
        }

        /// <summary>
        /// 渲染插值部分
        /// </summary>
        public void Render()
        {
        }

        private void Step()
        {
            RiptideLogger.Log(LogType.Debug, $"IsClient:{Transport.IsClient}:{LastDeltaTime}");
        }
    }
}