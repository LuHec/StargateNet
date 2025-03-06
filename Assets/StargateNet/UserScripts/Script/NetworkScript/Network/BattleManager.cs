using System;
using System.Collections;
using System.Collections.Generic;
using StargateNet;
using Unity.Mathematics;
using UnityEngine;

public class BattleManager : NetworkBehavior
{
    public GameObject vectorPrefab;
    public GameObject controlPointA;
    public GameObject controlPointB;
    public Vector3 controlPointAPos;
    public Vector3 controlPointBPos;
    private List<Vector3> spawnPos;
    private SgNetworkGalaxy _galaxy;
    private HashSet<NetworkObjectRef> teamA = new HashSet<NetworkObjectRef>();
    private HashSet<NetworkObjectRef> teamB = new HashSet<NetworkObjectRef>();
    private int _playerCount = 0;
    private int _localPlayerEntityId = -1;

    [Header("Respawn Settings")]
    [SerializeField] private float initialRadius = 5f;    // 初始搜索半径
    [SerializeField] private float maxRadius = 20f;       // 最大搜索半径
    [SerializeField] private float radiusIncrement = 5f;  // 每次扩大的半径
    [SerializeField] private int pointsPerCircle = 8;     // 每圈测试点数
    [SerializeField] private float playerHeight = 2f;     // 玩家高度
    [SerializeField] private LayerMask obstacleLayer;     // 障碍物层级

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
    [Replicated]
    public int TeamAPt { get; set; }
    [Replicated]
    public int TeamBPt { get; set; }

    public List<FPSController> pendingRespawnPlayers = new List<FPSController>();

    public void PosInit(Transform[] pos)
    {
        spawnPos = new List<Vector3>(pos.Length);
        for (int i = 0; i < pos.Length; i++)
        {
            spawnPos.Add(pos[i].position);
        }
    }

    public void SetLocalPlayerEntityId(int id)
    {
        _localPlayerEntityId = id;
    }

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        _galaxy = galaxy;
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
        TeamAPt = 0;
        TeamBPt = 0;
    }

    public void AddRespawnTimer(float delay, AttributeComponent player, FPSController killer)
    {
        if (!IsServer) return;
        if (killer.attributeComponent.TeamTag != player.TeamTag)
        {
            if (killer.attributeComponent.TeamTag == 0)
            {
                TeamAPt += 1;
            }
            else
            {
                TeamBPt += 1;
            }
        }

        var timer = new RespawnTimer
        {
            TriggerTime = _currentTime + delay,
            entityId = player.Entity.NetworkId,
            Player = player
        };
        _respawnHeap.Push(timer);
        if (killer != null)
            BoardCastKillInfo(killer.Entity.NetworkId.refValue, player.Entity.NetworkId.refValue);
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

    private Vector3 FindValidSpawnPosition(Vector3 basePosition)
    {
        float currentRadius = initialRadius;
        int maxAttempts = 3; // 每个半径尝试的圈数
        
        while (currentRadius <= maxRadius)
        {
            // 每圈尝试多次
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                // 在当前半径上螺旋式采样点
                for (int i = 0; i < pointsPerCircle; i++)
                {
                    float angle = i * (360f / pointsPerCircle);
                    float rad = angle * Mathf.Deg2Rad;
                    
                    Vector3 offset = new Vector3(
                        Mathf.Cos(rad) * currentRadius,
                        0f,
                        Mathf.Sin(rad) * currentRadius
                    );
                    
                    Vector3 testPoint = basePosition + offset;
                    
                    // 检查位置是否有效
                    if (IsValidSpawnPoint(testPoint))
                    {
                        return testPoint;
                    }
                }
            }
            
            // 扩大搜索半径
            currentRadius += radiusIncrement;
        }
        
        // 如果没找到合适的点，返回原始位置
        return basePosition;
    }

    private bool IsValidSpawnPoint(Vector3 position)
    {
        // 向上射线检测确保头顶没有障碍物
        bool clearAbove = !Physics.Raycast(
            position, 
            Vector3.up, 
            playerHeight, 
            obstacleLayer
        );

        // 向下射线检测确保有地面支撑
        bool hasGround = Physics.Raycast(
            position + Vector3.up * playerHeight,
            Vector3.down,
            out RaycastHit hit,
            playerHeight + 1f,
            obstacleLayer
        );

        // 检查周围是否有足够空间
        bool hasSpace = !Physics.CheckSphere(
            position + Vector3.up * (playerHeight * 0.5f),
            0.5f,
            obstacleLayer
        );

        return clearAbove && hasGround && hasSpace;
    }

    private void RespawnPlayer(AttributeComponent player)
    {
        // 获取一个基础重生点
        Vector3 basePosition = spawnPos[UnityEngine.Random.Range(0, spawnPos.Count)];
        
        // 寻找有效的重生位置
        Vector3 validPosition = FindValidSpawnPosition(basePosition);
        
        // 设置玩家位置并重生
        player.transform.position = validPosition;
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

    [NetworkCallBack(nameof(TeamAPt), false)]
    public void OnTeamAPtChanged(CallbackData callbackData)
    {
        if (IsServer) return;
        UIManager.Instance.GetUIPanel<UIBattleInterface>().SetAPoint(TeamAPt);
    }

    [NetworkCallBack(nameof(TeamBPt), false)]
    public void OnTeamBPtChanged(CallbackData callbackData)
    {
        if (IsServer) return;
        UIManager.Instance.GetUIPanel<UIBattleInterface>().SetBPoint(TeamBPt);
    }

    // 添加重置时间的方法
    public void ResetTime()
    {
        Minutes = 0;
        Seconds = 0;
        _timeAccumulator = 0f;
    }

    [NetworkRPC(NetworkRPCFrom.ServerCall)]
    public void BoardCastKillInfo(int killer, int victim)
    {
        if(killer == _localPlayerEntityId)
        {
            UIManager.Instance.GetUIPanel<UIEliminateInfo>().AddMessage(victim);
        }
    }

    public NetworkObject SpawnPlayer(GameObject prefab, int inputSource)
    {
        var player = _galaxy.NetworkSpawn(prefab, spawnPos[UnityEngine.Random.Range(0, spawnPos.Count)], Quaternion.identity, inputSource);
        player.GetComponent<AttributeComponent>().TeamTag = _playerCount % 2;
        _playerCount++;
        return player;
    }


    internal NetworkObject RequireWeapon(AttributeComponent attributeComponent)
    {
        NetworkObject networkWeapon = _galaxy.NetworkSpawn(vectorPrefab, attributeComponent.transform.position, Quaternion.identity);
        return networkWeapon;
    }
}
