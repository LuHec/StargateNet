using UnityEngine;

namespace StargateNet
{
    public sealed class SgNetEngine
    {
        private SimulationClock _timer;
        internal float LastDeltaTime { get; private set; }
        internal float LastTimeScale { get; private set; }

        internal SgNetConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }

        private SgTransport _transport;

        public SgNetEngine()
        {
        }

        public void Start(SgTransport transport, SgNetConfigData sgNetConfigData)
        {
            ConfigData = sgNetConfigData;
            _transport = transport;
            _timer = new SimulationClock(this, Step);
            IsRunning = true;
        }

        public void Update(float deltaTime, float timeScale)
        {
            if (!IsRunning) return;

            LastDeltaTime = deltaTime;
            LastTimeScale = timeScale;

            _timer.PreUpdate();
            _timer.Update();
        }

        /// <summary>
        /// 渲染插值部分
        /// </summary>
        public void Render()
        {
        }

        private void Step()
        {
            Debug.Log($"IsClient:{_transport.IsClient}:{LastDeltaTime}" );
        }
    }
}