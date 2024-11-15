using System.Collections.Generic;
using StargateNet;


public class WorldState
{
    public int MaxSnapshotsCount { private set; get; }

    public int FromSnapshotIdx => fromTick.IsValid ? fromTick.tickValue % MaxSnapshotsCount : -1;
    internal Snapshot FromSnapshot => fromTick.IsValid ? snapshots[FromSnapshotIdx] : null;
    internal List<Snapshot> snapshots; // 过去Tick的快照
    internal Tick fromTick = Tick.InvalidTick;
    internal Snapshot ToSnapshot { private set; get; }

    internal WorldState(int maxSnapCnt)
    {
        this.MaxSnapshotsCount = maxSnapCnt;
        this.snapshots = new List<Snapshot>(maxSnapCnt);
    }

    internal void Init(Tick tick)
    {
        this.fromTick = tick;
    }

    internal void FlushTick(Tick tick)
    {
    }

    internal void UpdateFromTick(Tick tick)
    {
        this.fromTick = tick;
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
    internal Snapshot GetHistroyTick(int minus)
    {
        if (minus > this.MaxSnapshotsCount) return null;
        int fromTickValue = this.fromTick.tickValue;
        return this.snapshots[(fromTickValue - minus) % this.MaxSnapshotsCount];
    }
}