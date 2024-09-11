using System;

namespace StargateNet
{
    public sealed class SimulationClock
    {
        internal double Time { get; private set; }
        private Action _action;
        private SgNetEngine _engine;
        private float _deltaTime;                   // update delta time(not fixed)
        private float _scaledFixedDelta;            // ms/frame
        private float _realScaledFixedDelta;        // scaled ms/frame
        private double _accumulator;                // 累计的帧时间，消耗该时间可以tick一次

        internal SimulationClock(SgNetEngine engine, Action action)
        {
            _engine = engine;
            _action = action;
            _scaledFixedDelta = 1 / engine.ConfigData.tickRate;
            _realScaledFixedDelta = _scaledFixedDelta;
        }

        public void PreUpdate()
        {
            _deltaTime = _engine.LastDeltaTime / _engine.LastTimeScale;
            _accumulator += (double)_deltaTime;
            Time += _deltaTime;
        }

        public void Update()
        {
            // 10次只是一个阈值，用来限制处理低帧率的次数。在60tick的情况下，得低于6帧才会在一帧内处理10次
            for (int i = 0; i < 10 && _accumulator > _realScaledFixedDelta; i++)
            {
                _accumulator -= _realScaledFixedDelta;
                _action?.Invoke();
            }
        }
    }
}