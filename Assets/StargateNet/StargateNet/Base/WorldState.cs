using System.Collections.Generic;
using StargateNet;


public class WorldState
{
    // ------------------ public ------------------ //
    public int MaxSnapshotsCount { private set; get; }

    public int CurrentSnapIdx => currentTick.IsValid ? currentTick.tickValue % MaxSnapshotsCount : -1;

    // ------------------ engine ------------------ //
    internal List<Snapshot> snapshots; // 过去Tick的快照
    internal Tick currentTick = Tick.InvalidTick;
    internal Snapshot CurrentSnapshot=>currentTick.IsValid ? snapshots[CurrentSnapIdx] : null;

    internal WorldState(int maxSnapCnt)
    {
        this.MaxSnapshotsCount = maxSnapCnt;
        this.snapshots = new List<Snapshot>(maxSnapCnt);
    }

    internal void Init(Tick tick)
    {
        this.currentTick = tick;
    }

    internal void FlushTick(Tick tick)
    {
        
    }
}