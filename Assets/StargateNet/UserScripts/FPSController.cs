using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class FPSController : NetworkBehavior
{
    [Networked] private float Pitch { get; set; }
    [Networked] private NetworkBool IsFiring { get; set; }
    [Networked] private Vector3 Movement { get; set; }

    public override void NetworkFixedUpdate()
    {
        if (this.IsServer)
        {
            Pitch += 10;
            IsFiring = false;
            Movement += new Vector3(0.1f, 0.1f, 0.1f);
        }
        this.transform.position = Movement;
    }
}