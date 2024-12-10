using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class FPSController : NetworkBehavior
{
    protected NetworkTransform networkTransform;
    private Tick _lastTick = Tick.InvalidTick;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        networkTransform = GetComponent<NetworkTransform>();
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (this.FetchInput(out NetworkInput input))
        {
            Vector3 deltaMovement = new Vector3(input.input.x, 0, input.input.y) * galaxy.NetworkDeltaTime;
            networkTransform.Position += deltaMovement;
        }
        
        Debug.Log("FPS Controller At Engine Tick:" + galaxy.EngineTick + " CurrentTick:"  + galaxy.CurrentTick + ",Position:" + transform.position);
        _lastTick = galaxy.CurrentTick;
    }

    public override void NetworkUpdate(SgNetworkGalaxy galaxy)
    {
    }
}