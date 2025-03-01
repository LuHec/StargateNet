using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class BattleManager : NetworkBehavior
{
    private struct RespawnTimer
    {
        public float TriggerTime;
        public NetworkObjectRef entityId;
        public FPSController Player;
    }

    private MinHeap<RespawnTimer> _respawnHeap;
    private float _currentTime;

    [Replicated]
    public int Minutes { get; set; }
    [Replicated]
    public int Seconds { get; set; }
    public List<FPSController> pendingRespawnPlayers = new List<FPSController>();

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        base.NetworkStart(galaxy);
        if (this.IsServer)
        {
            SetAlwaysSync(true);
            _respawnHeap = new MinHeap<RespawnTimer>(timer => timer.TriggerTime);
        }
        Minutes = 0;
        Seconds = 0;
    }

    public void AddRespawnTimer(float delay, FPSController player)
    {
        if (!IsServer) return;
        var timer = new RespawnTimer
        {
            TriggerTime = _currentTime + delay,
            entityId = player.Entity.NetworkId,
            Player = player
        };
        _respawnHeap.Push(timer);
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (!IsServer) return;

        _currentTime += Time.fixedDeltaTime;

        if (Seconds >= 60)
        {
            Minutes++;
            Seconds = 0;
        }
        else
        {
            Seconds++;
        }

        // 处理复活计时器
        while (!_respawnHeap.IsEmpty && _respawnHeap.Peek().TriggerTime <= _currentTime)
        {
            var timer = _respawnHeap.Pop();
            RespawnPlayer(timer.Player);
        }
    }

    private void RespawnPlayer(FPSController player)
    {
        player.OnResawn();
    }
}
