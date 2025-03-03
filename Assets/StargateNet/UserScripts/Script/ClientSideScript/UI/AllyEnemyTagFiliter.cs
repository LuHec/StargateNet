using UnityEngine;
using System.Collections.Generic;
using StargateNet;

public class AllyEnemyTagFiliter : MonoBehaviour
{
    [Header("视野设置")]
    [SerializeField] private float viewDistance = 100f;
    [SerializeField] private float viewAngle = 90f;
    [SerializeField] private float updateInterval = 0.2f;

    private Camera mainCamera;
    private UIAllyPanel uiAllyPanel;
    private HashSet<int> visiblePlayers;
    private HashSet<int> newVisiblePlayers;
    private float updateTimer;

    // 缓存组件引用
    private List<FPSController> playerCache = new List<FPSController>(32);
    private Vector3 vectorCache = new Vector3();

    private void Start()
    {
        uiAllyPanel = UIManager.Instance.GetUIPanel<UIAllyPanel>();
        // 预分配容量
        visiblePlayers = new HashSet<int>(32);
        newVisiblePlayers = new HashSet<int>(32);
    }

    public void Init(Camera camera)
    {
        mainCamera = camera;
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            CheckVisiblePlayers();
        }
    }

    private void CheckVisiblePlayers()
    {
        newVisiblePlayers.Clear();
        playerCache.Clear();

        // 获取所有 FPSController
        var players = FindObjectsOfType<FPSController>();
        playerCache.AddRange(players);

        var camTransform = mainCamera.transform;
        var camPosition = camTransform.position;
        var camForward = camTransform.forward;

        foreach (var player in playerCache)
        {
            if (player.IsLocalPlayer() || player.IsDead) continue;

            var playerTransform = player.transform;
            var playerPosition = playerTransform.position;

            // 使用缓存的Vector3计算方向
            vectorCache.x = playerPosition.x - camPosition.x;
            vectorCache.y = playerPosition.y - camPosition.y;
            vectorCache.z = playerPosition.z - camPosition.z;
            float sqrDistance = vectorCache.sqrMagnitude;

            // 使用平方距离比较，避免开方运算
            if (sqrDistance > viewDistance * viewDistance) continue;

            float distance = Mathf.Sqrt(sqrDistance);
            vectorCache.x /= distance;
            vectorCache.y /= distance;
            vectorCache.z /= distance;

            float angle = Vector3.Angle(camForward, vectorCache);
            if (angle > viewAngle * 0.5f) continue;

            // if (Physics.Raycast(camPosition, vectorCache, out RaycastHit hit, distance))
            // {
            //     if (hit.collider.gameObject != player.gameObject) continue;
            // }

            int playerId = player.InputSource;
            newVisiblePlayers.Add(playerId);

            if (!visiblePlayers.Contains(playerId))
            {
                if (IsAlly(player))
                {
                    uiAllyPanel.AddAlly(playerId, player.transform);
                }
                else
                {
                    uiAllyPanel.AddEnemy(playerId, player.transform);
                }
            }
        }

        // 使用foreach避免迭代器分配
        foreach (int oldId in visiblePlayers)
        {
            if (!newVisiblePlayers.Contains(oldId))
            {
                uiAllyPanel.RemoveAlly(oldId);
                uiAllyPanel.RemoveEnemy(oldId);
            }
        }

        // 交换引用而不是创建新对象
        var temp = visiblePlayers;
        visiblePlayers = newVisiblePlayers;
        newVisiblePlayers = temp;
    }

    private bool IsAlly(FPSController player)
    {
        // 根据你的游戏逻辑判断是否是队友
        // 这里只是示例，你需要根据实际情况实现
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        // 在编辑器中绘制视野范围
        if (mainCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, viewDistance);

            Vector3 forward = mainCamera.transform.forward;
            Quaternion leftRot = Quaternion.AngleAxis(-viewAngle * 0.5f, Vector3.up);
            Quaternion rightRot = Quaternion.AngleAxis(viewAngle * 0.5f, Vector3.up);
            Vector3 leftDir = leftRot * forward;
            Vector3 rightDir = rightRot * forward;

            Gizmos.DrawRay(transform.position, leftDir * viewDistance);
            Gizmos.DrawRay(transform.position, rightDir * viewDistance);
        }
    }
}