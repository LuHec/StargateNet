using System;

namespace StargateNet
{
    public sealed class SimulationClock
    {
        internal double Time { get; private set; }
        internal bool IsFirstCall { get; private set; }
        private Action _action;
        private SgNetworkEngine _engine;
        private float _deltaTime;                   // update delta time(not fixed)
        private float _scaledFixedDelta;            // ms/frame
        private float _realScaledFixedDelta;        // scaled ms/frame
        private double _accumulator;                // 累计的帧时间，消耗该时间可以tick一次，帧数过低时，这个值在两帧之间会变大

        internal SimulationClock(SgNetworkEngine engine, Action action)
        {
            this._engine = engine;
            this._action = action;
            this._scaledFixedDelta = 1 / engine.ConfigData.tickRate;
            this._realScaledFixedDelta = _scaledFixedDelta;
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
            for (int i = 0; i < 10 && this._accumulator > this._realScaledFixedDelta; i++)
            {
                this._accumulator -= this._realScaledFixedDelta;
                this._action?.Invoke();
                IsFirstCall = false; 
            }
        }
    }
}