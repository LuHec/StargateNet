using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class Plate : NetworkBehavior, IHitable
{
    public GameObject hitVfx;
    public float speed = 5.0f;
    public float distance = 10.0f;
    private Vector3 _oPosition;
    [Replicated] public Vector3 TestV { set; get; }

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        _oPosition = transform.position;
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsClient) return;
        float movement = Mathf.Sin((float)galaxy.ClockTime * speed) * distance;
        transform.position = _oPosition + new Vector3(movement, 0f, 0f);
        TestV += Vector3.up;
    }

    public void OnHit(int damage, Vector3 hitPoint, Vector3 hitNormal, FPSController hitter)
    {
        if (IsServer)
        {
            HitVfx(hitPoint, hitNormal);
        }
    }

    [NetworkRPC(NetworkRPCFrom.ServerCall)]
    public void HitVfx(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (hitVfx != null)
        {
            GameObject vfx = Instantiate(hitVfx, hitPoint, Quaternion.LookRotation(hitNormal));
            Destroy(vfx, 2.0f);
        }
    }

}