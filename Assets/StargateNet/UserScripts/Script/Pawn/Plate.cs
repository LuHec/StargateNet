using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class Plate : NetworkBehavior
{
    public float speed = 5.0f;
    public float distance = 10.0f;
    private NetworkTransform _networkTransform;
    private Vector3 _oPosition;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        _oPosition = transform.position;
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsClient) return;
        float movement = Mathf.Sin((float)galaxy.ClockTime * speed) * distance;
        transform.position = _oPosition + new Vector3(0f, 0f, movement);
    }
}