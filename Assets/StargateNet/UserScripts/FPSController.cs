using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class FPSController : NetworkBehavior
{
    [Networked] private float Pitch { get; set; }
    [Networked] private Vector3 Movement { get; set; }

    public override void NetworkFixedUpdate()
    {
        if (this.IsServer)
        {
            Pitch += 10;
            Movement = new Vector3(Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f), Random.Range(-1.0f, 1.0f));
        }

        // Debug.Log(Movement);
        // Debug.Log(IsFiring);
        // Debug.Log(Pitch);
        this.transform.position = Movement;
    }
}