using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class NetworkAutoDestroy : NetworkBehavior
{
    public float lifeTime = 10f;

    private bool countDown = false;
    private bool destroyed = false;
    private float timer = 0f;

    public void StartCountDown(bool value)
    {
        this.countDown = value;
        if(!countDown)
        {
            timer = 0f;
        }
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsClient || destroyed || !countDown) return;
        timer += galaxy.FixedDeltaTime;
        if(timer >= lifeTime)
        {
            galaxy.NetworkDestroy(this.gameObject);
            destroyed = true;
        }
    }
}
