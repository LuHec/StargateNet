using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class MyNetworkEventManager : NetworkEventManager
{
    [SerializeField] private GameObject playerPawn;
    private Vector2 _viewAngles;
    private Camera _mainCamera;

    public override void OnPlayerConnected(SgNetworkGalaxy galaxy, int playerId)
    {
        galaxy.NetworkSpawn(playerPawn, Vector3.zero, Quaternion.identity, playerId);
    }

    public override void OnPlayerPawnLoad(SgNetworkGalaxy galaxy, int playerId, NetworkObject networkObject)
    {
        if (galaxy.IsClient && playerId == galaxy.PlayerId)
        {
            Transform cameraParent = FindChildRecursive(networkObject.transform, "CameraPoint");
            if (cameraParent != null && Camera.main != null)
                Camera.main.transform.SetParent(cameraParent);
        }
    }

    private Transform FindChildRecursive(Transform parent, string targetName)
    {
        foreach (Transform child in parent)
        {
            Transform result = FindChildRecursive(child, targetName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }
}