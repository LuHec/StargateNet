using System;
using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    /// 远端同步的插值，只在客户端存在。
    /// 和LocalInterpolation不同，RemoteInterpolation会随着网络变化而变化。为了抵抗loss、lag，会设置一个缓冲区存放snapshot，而不是立刻应用到来的Snapshot
    /// 否则当loss或lag时等超出了一帧fixedUpdate时间的情况下，就很容易出现卡顿等情况(由于snapshot到来晚了，没有目标可以插值，就只能停在原地。在网络延迟波动的状态下，更容易观察到这点，远端玩家看起来一顿一顿的)。
    /// 用buffer存放snapshot，并设置一个插值延迟，延迟几fixed帧去更新，就能尽可能保证客户端看到的画面没有闪现。
    /// </summary>
    public class InterpolationRemote : Interpolation
    {
        private const float Scaler = 0.2f;
        private const float MaxDelay = 200f;
        internal override Tick FromTick => this._fromTick;
        internal override Tick ToTick => this._toTick;
        public override bool HasSnapshot => this.FromSnapshot != null && this.ToSnapshot != null;
        public override float Alpha => _alpha;
        internal override float InterpolationTime { get; }

        /// <summary>
        /// 帧时间+距离上次收包过了多久-积攒插值时间，这个值会被用于判断插值时间是否已经超时。||含义是还差多少时间才能把包全部消耗完,即interploate的延迟||
        /// 延迟和丢包与bufferTime成正比
        /// </summary>
        internal float CurrentBufferTime => this._bufferAsTime + (float)(this.Engine.SimulationClock.Time - this._lastTimeAddSnapshot) - this._currentLerpTime;

        private Queue<Snapshot> _snapshotsPool;
        private readonly int _maxSnapshots;
        private float _maxInterpolateRatio;
        private DoubleStats _packetTime;
        private double _lastTimeAddSnapshot;
        private float _currentLerpTime; // 插值时间，由Time.deltaTime叠加得到。表示从上一个插值帧开始过了多久。alpha = _currentLerpTime / threshold
        private float _bufferAsTime; // 将tick换算为时间的值，代表缓冲区里所有的snapshot的时间(tick * deltaTime)
        private float _alpha;
        private RingQueue<Snapshot> _snapshotBuffer;
        private Tick _fromTick;
        private Tick _toTick;
        private int _useAbleTicks = 0;

        public InterpolationRemote(StargateEngine stargateEngine, long stateByteSize, int metaCnt) : base(stargateEngine)
        {
            int count = stargateEngine.ConfigData.savedSnapshotsCount;
            this._maxSnapshots = count;
            this._snapshotBuffer = new RingQueue<Snapshot>(count);
            this._snapshotsPool = new Queue<Snapshot>(count + 10);
            for (int i = 0; i < count + 1; i++)
            {
                this._snapshotsPool.Enqueue(new Snapshot(stateByteSize, metaCnt, stargateEngine.Monitor));
            }

            this._maxInterpolateRatio = 1.2f;
            this._packetTime = new DoubleStats();
        }

        /// <summary>
        /// 更新远端插值的资源。
        /// 通过计算整体的时间比例来获得alpha值
        /// </summary>
        internal override void Update()
        {
            float fixedDeltaTime = this.Engine.SimulationClock.FixedDeltaTime;
            // double threshold = Math.Max(fixedDeltaTime, this._packetTime.Average) + fixedDeltaTime * 0.4 + this._packetTime.StdDeviation * 4.0;
            double threshold = Math.Max(fixedDeltaTime, 0) + fixedDeltaTime * 0.4 + this._packetTime.StdDeviation * 4.0;
            if (this._snapshotBuffer.Count < 2)
            {
                this._useAbleTicks = 0;
                return;
            }

            if (this._useAbleTicks < 2)
            {
                return;
            }

            this._fromTick = this._snapshotBuffer[0].snapshotTick;
            this._toTick = this._snapshotBuffer[1].snapshotTick;
            float time = (this._toTick - this._fromTick) * fixedDeltaTime;
            double delta = this.CurrentBufferTime - threshold;
            double maxThreshold = this._maxInterpolateRatio * fixedDeltaTime;
            // 对deltaTime进行缩放，应对网络波动.延迟丢包越大scale越大，延迟大的时候_currentLerpTime增加的就少，这样帧数消耗的就慢
            float scale = 1.0f;
            if (delta > maxThreshold)
            {
                scale = delta > fixedDeltaTime * 3.0f ? 1.2f : 1.01f;
            }
            else if (delta < maxThreshold)
            {
                scale = delta < -fixedDeltaTime * 3.0f ? 0.89f : 0.99f;
            }
            
            if (this._currentLerpTime < time)
            {
                this._currentLerpTime += Time.deltaTime * scale;
                this._alpha = this._currentLerpTime / time;
            }
            
            
            
            if (this._currentLerpTime > time)
            {
                while (this._alpha > 1) // 消耗插值时间，如果上一帧来得太慢，为了追赶进度就要退出以前的帧
                {
                    if (this._snapshotBuffer.Count == 0)
                    {
                        this._currentLerpTime = 0;
                        break;
                    }

                    // 退出缓存，并减去对应的插值时间
                    this.Dequeue();
                    this._currentLerpTime -= time;
                    this._bufferAsTime -= time;
                    this._alpha -= 1f;
                }

                if (this._snapshotBuffer.Count < 2)
                {
                    this._bufferAsTime = 0;
                    this._currentLerpTime = 0;
                }
            }

            this._alpha = this._currentLerpTime / time;
            if (this._snapshotBuffer.Count >= 2)
            {
                this.FromSnapshot = this._snapshotBuffer[0];
                this.ToSnapshot = this._snapshotBuffer[1];
                this._fromTick = this._snapshotBuffer[0].snapshotTick;
                this._toTick = this._snapshotBuffer[1].snapshotTick;
            }
        }

        internal void AddSnapshot(Tick tick, Snapshot remoteSnapshot)
        {
            double time = this.Engine.SimulationClock.Time;
            this._packetTime.Update(time - this._lastTimeAddSnapshot);
            this._lastTimeAddSnapshot = time;
            Snapshot snapshot = this._snapshotsPool.Dequeue();
            snapshot.Init(remoteSnapshot.snapshotTick);
            remoteSnapshot.CopyTo(snapshot);
            if (this._snapshotBuffer.Count == 0)
            {
                this._bufferAsTime += this.Engine.SimulationClock.FixedDeltaTime;
                this._snapshotBuffer.Enqueue(snapshot);
            }
            else
            {
                this._useAbleTicks++;
                this._bufferAsTime += (tick - _snapshotBuffer.Last.snapshotTick) * this.Engine.SimulationClock.FixedDeltaTime;
                this._snapshotBuffer.Enqueue(snapshot);
            }

            if (this._snapshotBuffer.IsFull)
                this.Reset();
        }

        internal void Dequeue()
        {
            this._snapshotsPool.Enqueue(this._snapshotBuffer[0]);
            this._snapshotBuffer.RemoveFromStart(1);
        }

        internal void Reset()
        {
            this._fromTick = Tick.InvalidTick;
            this._toTick = Tick.InvalidTick;
            this._bufferAsTime = 0;
            this._lastTimeAddSnapshot = 0;
            this._alpha = 0;
            this._currentLerpTime = 0;
            this._useAbleTicks = 0;
            while (this._snapshotBuffer.Count > 0)
            {
                this.Dequeue();
            }

            this._snapshotBuffer.FastClear();
        }
    }
}