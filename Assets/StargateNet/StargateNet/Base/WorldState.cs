using System.Collections.Generic;
using StargateNet;


public class WorldState
{
    public int MaxSnapshotsCount { private set; get; }

    public int FromSnapshotIdx => fromTick.IsValid ? fromTick.tickValue % MaxSnapshotsCount : -1;
    internal Snapshot FromSnapshot => fromTick.IsValid ? snapshots[FromSnapshotIdx] : null;

    /// <summary>
    ///  每一帧修改的主Snapshot，对于客户端来说这个用来存最新收到的服务端权威状态；服务端是本帧运算结果，其他的Snapshot都是对这个的拷贝。
    /// </summary>
    internal Snapshot CurrentSnapshot
    {
        private set => this._currentSnapshot = value;
        get => _currentSnapshot.snapshotTick == Tick.InvalidTick ? null : _currentSnapshot;
    }

    private Snapshot _currentSnapshot;

    /// <summary>
    /// 存放过去Snapshot，客户端拷贝时间在收到新的权威Snapshot时，服务端拷贝时间在一帧的结束。
    /// </summary>
    internal List<Snapshot> snapshots;

    internal Tick fromTick = Tick.InvalidTick;
    internal bool HasInitialized { private set; get; }


    internal WorldState(int maxSnapCnt, Snapshot currentSnapshot)
    {
        this.MaxSnapshotsCount = maxSnapCnt;
        this._currentSnapshot = currentSnapshot;
        this.snapshots = new List<Snapshot>(maxSnapCnt);
        this.HasInitialized = false;
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
        this.fromTick = tick;
        this._currentSnapshot.Init(tick);
    }

    /// <summary>
    /// 更新Snapshot和Tick。
    /// FromTick指的是本帧，FromTickSnapshot指的是本帧开始时的状态，CurrentSnapshot此时是上一帧的最后结果。此函数将CurrentSnapshot拷贝到FromTick，作为本帧的初始状态
    /// 接下来StargateEngine就会更新CurrentSnapshot，下一帧时CurrentSnapshot会再次拷贝到新的FromTickSnapshot。
    /// 对于客户端，真正的权威是WorldState::snapshots，客户端预测时可以修改CurrentSnapshot
    /// </summary>
    /// <param name="tick"></param>
    internal void Update(Tick tick)
    {
        this.fromTick = tick;
        this.FromSnapshot.Init(tick);
        this._currentSnapshot.snapshotTick = tick;
        if (!this.HasInitialized)
        {
            this.HasInitialized = true;
            return;
        }
        
        this.CurrentSnapshot.CopyStateTo(this.FromSnapshot);
        this.CurrentSnapshot.CleanMap();
    }

    /// <summary>
    /// 获取上一帧最终状态/本帧初始状态
    /// </summary>
    /// <returns></returns>
    internal Snapshot GetFromTick()
    {
        return this.FromSnapshot;
    }

    /// <summary>
    /// 获取以往的快照，需要注意的是从FromTick开始计算，而不是本帧
    /// </summary>
    /// <param name="minus">倒回几帧</param>
    /// <returns></returns>
    internal Snapshot GetHistoryTick(int minus)
    {
        if (minus > this.MaxSnapshotsCount) return null;
        int fromTickValue = this.fromTick.tickValue;
        return this.snapshots[(fromTickValue - minus) % this.MaxSnapshotsCount];
    }
}