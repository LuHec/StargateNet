using System.Collections;
using System.Collections.Generic;
using StargateNet;
using Unity.Mathematics;
using UnityEngine;

public class BattleManager : NetworkBehavior
{
    public GameObject controlPointA;
    public GameObject controlPointB;
    public Vector3 controlPointAPos;
    public Vector3 controlPointBPos;
    private struct RespawnTimer
    {
        public float TriggerTime;
        public NetworkObjectRef entityId;
        public AttributeComponent Player;
    }

    private MinHeap<RespawnTimer> _respawnHeap;
    private float _currentTime;
    private float _timeAccumulator;  // 累计时间

    [Replicated]
    public int Minutes { get; set; }
    [Replicated]
    public int Seconds { get; set; }
    public List<FPSController> pendingRespawnPlayers = new List<FPSController>();

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        this.SetAlwaysSync(true);
        if (this.IsServer)
        {
            SetAlwaysSync(true);
            _respawnHeap = new MinHeap<RespawnTimer>(timer => timer.TriggerTime);
            // galaxy.NetworkSpawn(controlPointA, controlPointAPos, Quaternion.Euler(90,0,0));
            // galaxy.NetworkSpawn(controlPointB, controlPointBPos, Quaternion.Euler(90,0,0));
        }
        Minutes = 0;
        Seconds = 0;
    }

    public void AddRespawnTimer(float delay, AttributeComponent player)
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

        _currentTime += galaxy.FixedDeltaTime;
        _timeAccumulator += galaxy.FixedDeltaTime;

        // 每过1秒更新一次时间
        while (_timeAccumulator >= 1f)
        {
            _timeAccumulator -= 1f;  // 减去1秒
            
            Seconds++;
            if (Seconds >= 60)
            {
                Minutes++;
                Seconds = 0;
            }
        }


        // 处理复活计时器
        while (IsServer && !_respawnHeap.IsEmpty && _respawnHeap.Peek().TriggerTime <= _currentTime)
        {
            var timer = _respawnHeap.Pop();
            RespawnPlayer(timer.Player);
        }
    }

    private void RespawnPlayer(AttributeComponent player)
    {
        player.OnResapwn();
    }

    [NetworkCallBack(nameof(Minutes), false)]
    public void OnMinutesChanged(CallbackData callbackData)
    {
        if (IsServer) return;
        UIManager.Instance.GetUIPanel<UIBattleInterface>().SetTime(Minutes, Seconds);
    }

    [NetworkCallBack(nameof(Seconds), false)]
    public void OnSecondsChanged(CallbackData callbackData)
    {
        if (IsServer) return;
        UIManager.Instance.GetUIPanel<UIBattleInterface>().SetTime(Minutes, Seconds);
    }

    // 添加重置时间的方法
    public void ResetTime()
    {
        Minutes = 0;
        Seconds = 0;
        _timeAccumulator = 0f;
    }
}
