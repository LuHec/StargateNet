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
        private int _clockLevel = 1;

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
                this._engine.Monitor.clockLevel = this._clockLevel;
                this._accumulator -= this._scaledDelta;
                this._action?.Invoke();
                IsFirstCall = false;
            }

            this._engine.Monitor.deltaTime = this._scaledDelta;
            // 客户端会根据延迟来加速自己的模拟
            if (!this._engine.IsClient || this._engine.Client.Client.RTT == -1 ||
                !this._engine.ClientSimulation.currentTick.IsValid ||
                !this._engine.ClientSimulation.authoritativeTick.IsValid) return;
            if (this._engine.Client.HeavyPakLoss)
                this._scaledDelta = this._fixedDelta;
            AdjustClock(this._engine.Client.Client.RTT, 0, this._engine.ClientSimulation.currentTick.tickValue,
                this._engine.ClientSimulation.authoritativeTick.tickValue);
        }

        private float _lastAdjustTime = 0;
        private void AdjustClock(float clientRTT, float serverSnapshotTimeAvg, int clientTick, int serverTick)
        {
            // 有关延迟：Client RTT, Server Pack Time, Last Pack Time, 有关Tick:ClientTick, ServerTick
            // 用各种延迟计算出一个Tick的合理区间然后比较当前的Tick差，最后三种结果：加速，减速，不变
            float pakTime = UnityEngine.Time.time - this._engine.ClientSimulation.lastReceiveTime;
            float targetDelayTick = clientRTT / this._deltaTime;
            float delayTime = (serverTick + targetDelayTick - clientTick) / this._fixedDelta;
            float delayStd = 0.4f * this._deltaTime; // 标准差值
            //[-std, std]这个范围内都是正常区间
            // 比标准值大，说明慢了，要加速
            if (delayTime > delayStd)
            {
                this._clockLevel = 2;
                // 分为两种情况：直接追帧和加速
                // 和理论值差了3帧以上，就直接让下一帧多模拟几次追上去
                if (delayTime > this._fixedDelta * 3.0f && this._accumulator < this._fixedDelta * 3.0f)
                {
                    this._accumulator += this._fixedDelta * 5.0f;
                }
                else
                    this._scaledDelta = 0.99f * this._fixedDelta;
            }
            // 小于0，说明快了，要减速
            else if (delayTime < 0 && delayTime < -delayStd)
            {
                this._clockLevel = 0;
                this._scaledDelta = 1.02f * this._fixedDelta;
            }
            //在正常区间
            else
            {
                this._clockLevel = 1;
                this._scaledDelta = this._fixedDelta;
            }
        }
    }
}