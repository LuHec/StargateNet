using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class MyNetworkEventManager : NetworkEventManager
{
    [SerializeField] private BattleManager battleManager;
    [SerializeField] private GameObject playerPawn;
    private HashSet<int> _playerIds = new HashSet<int>(128);

    public override void OnNetworkEngineStart(SgNetworkGalaxy galaxy)
    {
        Camera.main.gameObject.AddComponent<ObsCamera>();
        if(galaxy.IsServer)
            galaxy.NetworkSpawn(battleManager.gameObject, Vector3.zero, Quaternion.identity);
    }   

    public override void OnPlayerConnected(SgNetworkGalaxy galaxy, int playerId)
    {
        if (!_playerIds.Add(playerId)) return;
        galaxy.NetworkSpawn(playerPawn, Vector3.zero, Quaternion.identity, playerId);
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