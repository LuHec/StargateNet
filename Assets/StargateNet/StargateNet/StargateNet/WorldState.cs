using System;
using System.Collections.Generic;
using StargateNet;


public class WorldState
{
    internal StargateEngine Engine { private get; set; }
    internal int MaxSnapshotsCount { private set; get; }

    internal int FromSnapshotIdx => fromTick.IsValid ? fromTick.tickValue % MaxSnapshotsCount : -1;

    /// <summary>
    /// 获取上一帧最终状态/本帧初始状态
    /// </summary>
    internal Snapshot FromSnapshot => fromTick.IsValid ? snapshots[FromSnapshotIdx] : null;

    /// <summary>
    ///  每一帧修改的主Snapshot，对于客户端来说这个用来存最新收到的服务端权威状态；服务端是本帧运算结果，其他的Snapshot都是对这个的拷贝。
    /// </summary>
    public Snapshot CurrentSnapshot
    {
        private set => this._currentSnapshot = value;
        get => _currentSnapshot.snapshotTick == Tick.InvalidTick ? null : _currentSnapshot;
    }

    /// <summary>
    /// 存放过去Snapshot，客户端拷贝时间在收到新的权威Snapshot时，服务端拷贝时间在一帧的结束。
    /// </summary>
    internal List<Snapshot> snapshots;

    internal Tick fromTick = Tick.InvalidTick;

    // internal bool HasInitialized { private set; get; }
    internal int HistoryCount => _tickCount > this.MaxSnapshotsCount ? MaxSnapshotsCount : _tickCount;
    private int _tickCount = 0;
    private Snapshot _currentSnapshot;


    internal WorldState(StargateEngine engine, int maxSnapCnt, Snapshot currentSnapshot)
    {
        this.Engine = engine;
        this.MaxSnapshotsCount = maxSnapCnt;
        this._currentSnapshot = currentSnapshot;
        this.snapshots = new List<Snapshot>(maxSnapCnt);
        // this.HasInitialized = false;
    }

    internal void HandledRelease()
    {
        foreach (var snapshot in this.snapshots)
        {
            snapshot.NetworkStates.HandledRelease();
        }
    }

    internal void Init(Tick tick)
    {
        // this.HasInitialized = true;
        // this.fromTick = tick;
        this._currentSnapshot.Init(tick);
    }

    /// <summary>
    /// 更新Snapshot和Tick。
    /// FromTick指的是本帧，FromTickSnapshot指的是本帧开始时的状态，CurrentSnapshot此时是上一帧的最后结果。此函数将CurrentSnapshot拷贝到FromTick，作为本帧的初始状态
    /// 接下来StargateEngine就会更新CurrentSnapshot，下一帧时CurrentSnapshot会再次拷贝到新的FromTickSnapshot。
    /// </summary>
    /// <param name="tick"></param>
    internal void ServerUpdateState(Tick tick)
    {
        if (this.Engine.IsClient) throw new Exception("Can't update world state in a client");
        this.fromTick = tick;
        this.FromSnapshot.Init(tick);
        this._currentSnapshot.snapshotTick = tick;
        this._tickCount++;
        // this.CurrentSnapshot.CopyStateTo(this.FromSnapshot);
        this.CurrentSnapshot.CopyTo(this.FromSnapshot);
        this.CurrentSnapshot.CleanMap();
    }

    /// <summary>
    /// 对于客户端，真正的权威是WorldState::Snapshots，客户端预测时可以修改CurrentSnapshot
    /// 客户端如果发生了丢包，那过去的帧数就不一定是连续的。但是这个不会影响客户端
    /// </summary>
    /// <param name="tick"></param>
    /// <param name="buffer"></param>
    internal void ClientUpdateState(Tick tick, Snapshot buffer)
    {
        if (this.Engine.IsServer) throw new Exception("Can't update world state in a server");
        this.fromTick = tick;
        this.FromSnapshot.Init(tick);
        buffer.CopyTo(this.FromSnapshot);
        this._tickCount++;
    }

    /// <summary>
    /// 获取以往的快照，从FromTick开始计算
    /// </summary>
    /// <param name="minus">倒回几帧</param>
    /// <returns></returns>
    public Snapshot GetHistoryTick(int minus)
    {
        if (minus > this.MaxSnapshotsCount) return null;
        int fromTickValue = this.fromTick.tickValue;
        int targetTickValue = fromTickValue - minus;
        Snapshot res = this.snapshots[(targetTickValue) % this.MaxSnapshotsCount];
        return res.snapshotTick.tickValue == targetTickValue ? res : null;
    }
}