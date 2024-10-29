using System;

namespace StargateNet
{
    public sealed class SimulationClock
    {
        internal double Time { get; private set; }
        internal bool IsFirstCall { get; private set; }
        private Action _action;
        private SgNetworkEngine _engine;
        private float _deltaTime; // update delta time(not fixed)
        private float _fixedDelta; // config ms/frame
        private float _scaledDelta; // scaled ms/frame
        private double _accumulator; // 累计的帧时间，消耗该时间可以tick一次，帧数过低时，这个值在两帧之间会变大

        internal SimulationClock(SgNetworkEngine engine, Action action)
        {
            this._engine = engine;
            this._action = action;
            this._fixedDelta = 1.0f / engine.ConfigData.tickRate;
            this._scaledDelta = _fixedDelta;
        }

        public void PreUpdate()
        {
            this._deltaTime = this._engine.LastDeltaTime / this._engine.LastTimeScale;
            this._accumulator += this._deltaTime;
            this.Time += this._deltaTime;
        }

        public void Update()
        {
            IsFirstCall = true;
            // 10次只是一个阈值，用来限制处理低帧率的次数。在60tick的情况下，得低于6帧才会在一帧内处理10次，在帧数足够的情况下只会触发一次
            for (int i = 0; i < 10 && this._accumulator > this._scaledDelta; i++)
            {
                this._accumulator -= this._scaledDelta;
                this._action?.Invoke();
                IsFirstCall = false;
            }

            this._engine.Monitor.deltaTime = this._scaledDelta;
            // 客户端会根据延迟来加速自己的模拟
            if (!this._engine.IsClient || this._engine.Client.Client.RTT == -1) return;
            // AdjustClock(this._engine.Client.Client.RTT);
        }

        private void AdjustClock(float clientRTT, float serverSnapshotTimeAvg, Tick clientTick, Tick serverTick)
        {
            // 有关延迟：Client RTT, Server Pack Time, Last Pack Time, 有关Tick:ClientTick, ServerTick
            // 用各种延迟计算出一个Tick的合理区间然后比较当前的Tick差，最后三种结果：加速，减速，不变
            float pakTime = UnityEngine.Time.time - this._engine.Client.lastReceiveTime;
            
            if (true)
            {
                
            }
            else if (true)
            {
                
            }
            else
            {
                this._scaledDelta = this._fixedDelta;
            }
        }
    }
}