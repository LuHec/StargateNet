using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class MyNetworkEventManager : NetworkEventManager
{
    public override void OnPlayerConnected(SgNetworkGalaxy galaxy, int playerId)
    {
        galaxy.TestOnPlayerConnected(playerId);
    }

    public override void OnReadInput(SgNetworkGalaxy galaxy)
    {
        NetworkInput networkInput = new NetworkInput();
        networkInput.input = new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        galaxy.SetInput(networkInput);
    }
}