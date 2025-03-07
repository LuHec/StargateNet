using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class MyNetworkEventManager : NetworkEventManager
{
    [SerializeField] private Transform[] spawnPos;
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private GameObject playerPawn;
    private HashSet<int> _playerIds = new HashSet<int>(128);
    private BattleManager _battleManager;

    public override void OnNetworkEngineStart(SgNetworkGalaxy galaxy)
    {
        Camera.main.gameObject.AddComponent<ObsCamera>();
        if (galaxy.IsServer)
            {
                _battleManager = galaxy.NetworkSpawn(battleManager.gameObject, Vector3.zero, Quaternion.identity).GetComponent<BattleManager>();
                _battleManager.PosInit(spawnPos);
            }

        if (galaxy.IsClient)
        {
            UIManager.Instance.ShowUI<UIBattleInterface>();
        }
    }

    public override void OnPlayerConnected(SgNetworkGalaxy galaxy, int playerId)
    {
        if (!_playerIds.Add(playerId)) return;
        if (galaxy.IsServer)
            _battleManager.SpawnPlayer(playerPawn, playerId);
    }

    // public override void OnPlayerPawnLoad(SgNetworkGalaxy galaxy, int playerId, NetworkObject networkObject)
    // {
    //     if (galaxy.IsClient && playerId == galaxy.PlayerId)
    //     {
    //         Transform cameraParent = FindChildRecursive(networkObject.transform, "CameraPoint");
    //         if (cameraParent != null && Camera.main != null)
    //             Camera.main.transform.SetParent(cameraParent);
    //     }
    // }

    // private Transform FindChildRecursive(Transform parent, string targetName)
    // {
    //     foreach (Transform child in parent)
    //     {
    //         Transform result = FindChildRecursive(child, targetName);
    //         if (result != null)
    //         {
    //             return result;
    //         }
    //     }

    //     return null;
    // }
}