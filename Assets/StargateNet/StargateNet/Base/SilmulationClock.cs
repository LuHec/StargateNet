using System;
using Riptide.Utils;

namespace StargateNet
{
    public sealed class SimulationClock
    {
        internal double Time { get; private set; }
        internal bool IsFirstCall { get; private set; }
        internal bool IsLastCall { get; private set; }
        public float FixedDeltaTime => this._fixedDelta;
        private Action _action;

        private StargateEngine _engine;

        // 时间单位全都是秒
        private float _deltaTime; // update delta time(not fixed)
        private float _fixedDelta; // config ms/frame
        private float _scaledDelta; // scaled ms/frame
        private double _accumulator; // 累计的帧时间，消耗该时间可以tick一次，帧数过低时，这个值在两帧之间会变大

        private int _clockLevel = 1;

        // private float _lastAdjustTime = 0;
        private bool _initAdjust = true;
        private double _connectTime = -1;
        private double _lastPacketTime = -1;
        private double _lastAdjustTime = 0;

        internal SimulationClock(StargateEngine engine, Action action)
        {
            this._engine = engine;
            this._action = action;
            this._fixedDelta = 1.0f / engine.ConfigData.tickRate;
            this._scaledDelta = _fixedDelta;
        }

        internal void PreUpdate()
        {
            this._deltaTime = this._engine.LastDeltaTime / this._engine.LastTimeScale;
            this._accumulator += this._deltaTime;
            this.Time += this._deltaTime;
        }

        internal void Update()
        {
            this.IsFirstCall = true;
            this.IsLastCall = false;
            // 10次只是一个阈值，用来限制处理低帧率的次数。在60tick的情况下，得低于6帧才会在一帧内处理10次，在帧数足够的情况下只会触发一次
            for (int i = 0; i < 10 && this._accumulator > this._scaledDelta; i++)
            {
                this._engine.Monitor.clockLevel = this._clockLevel;
                this._accumulator -= this._scaledDelta;
                this.IsLastCall = this._accumulator < this._scaledDelta;
                // 只有每帧的第一次模拟会触发回滚+Resim
                this._action?.Invoke();
                this.IsFirstCall = false;
            }

            this._engine.Monitor.deltaTime = this._scaledDelta;
            // 客户端会根据延迟来加速自己的模拟
            if (!this._engine.IsClient || this._engine.Client.Client.RTT == -1 ||
                !this._engine.ClientSimulation.currentTick.IsValid ||
                !this._engine.ClientSimulation.authoritativeTick.IsValid)
                return;
            
            if (this._engine.Client.HeavyPakLoss)
            {
                this._scaledDelta = this._fixedDelta;
                this._engine.Monitor.clockLevel = 1;
                return;
            }

            AdjustClock(this._engine.Client.Client.SmoothRTT * 0.001,
                this._engine.ClientSimulation.serverInputRcvTimeAvg,
                this._engine.ClientSimulation.currentTick.tickValue,
                this._engine.ClientSimulation.authoritativeTick.tickValue);
        }

        private void AdjustClock(double latency, double serverInputRcvTimeAvg, double currentTick, double serverTick)
        {
            // 目前算出来的是在最小帧率附近的值，需要加上一定的提前量

            // 有关延迟：Client RTT, Server Pack Time, Last Pack Time, 有关Tick:ClientTick, ServerTick
            // 用各种延迟计算出一个Tick的合理区间然后比较当前的Tick差，最后三种结果：加速，减速，不变
            // 如果想让客户端在高延迟下多预测几帧，优先应该调整的是【delayTime】
            double pakTime = this.Time - this._lastPacketTime;
            double targetDelayTick =
                (pakTime + latency) / this._fixedDelta; //  从上一次收到包时间到包发到服务端后，服务端的增加帧数(RTT+PakTimeDelta)
            double serverBiasTick = (serverInputRcvTimeAvg - _fixedDelta) / this._fixedDelta; // 基于服务端的接受延迟和一帧时间差值的调整值，
            double delayTime = (serverTick + targetDelayTick + serverBiasTick + 2 - currentTick) * this._fixedDelta;
            double delayStd = 0.4 * this._fixedDelta; // 标准差值
            // RiptideLogger.Log(LogType.Error,
            //     $"Delay Time {delayTime}, Delay std {delayStd}， Client Tick {currentTick}, Server Tick{serverTick}, target Delay Tick {targetDelayTick}, pak Time {pakTime}, client RTT {latency}");
            //[-std, std]这个范围内都是正常区间
            // 比标准值大，说明慢了，要加速
            if (delayTime > delayStd)
            {
                this._clockLevel = 2;
                // 分为两种情况：直接追帧和加速
                // 第一次连上服务端，追帧
                if (this.Time - this._lastAdjustTime > 0.5 && this._initAdjust && this.Time - this._connectTime > 0.5)
                {
                    this._accumulator += latency * 2;
                    this._initAdjust = false;
                }

                // 和理论值差了3帧以上，就直接让下一帧多模拟几次追上去
                if (this.Time - this._lastAdjustTime > 0.5 && delayTime > this._fixedDelta * 3.0 &&
                    this._accumulator < this._fixedDelta * 3.0)
                {
                    this._lastAdjustTime = this.Time;
                    this._accumulator += this._fixedDelta * 5.0f;
                }
                else
                {
                    this._scaledDelta = 0.95f * this._fixedDelta;
                }
            }
            // 小于0，说明快了，要减速
            else if (delayTime < 0 && delayTime < -delayStd)
            {
                this._clockLevel = 0;
                // 两个速度间选一个
                this._scaledDelta = delayTime < -this._fixedDelta * 3.0f
                    ? 1.04f * this._fixedDelta
                    : 1.01f * this._fixedDelta;
            }
            //在正常区间
            else
            {
                this._clockLevel = 1;
                this._scaledDelta = this._fixedDelta;
            }
        }

        internal void OnRecvPak()
        {
            this._lastPacketTime = this.Time;
        }

        internal void OnConnect()
        {
            this._connectTime = this.Time;
        }
    }
}