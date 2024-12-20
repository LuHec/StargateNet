using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class Plate : NetworkBehavior
{
    private NetworkTransform _networkTransform;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        this._networkTransform = this.GetComponent<NetworkTransform>();
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        Vector3 position = _networkTransform.Position;
        position += galaxy.FixedDeltaTime * new Vector3(1, 1, 0);
        _networkTransform.Position = position;
    }
} 