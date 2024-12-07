using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class NetworkTransform : NetworkBehavior
{
    [Networked] public Vector3 Position { get; set; }
    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        this.transform.position = this.Position;
    }
}
