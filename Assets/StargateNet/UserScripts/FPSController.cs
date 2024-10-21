using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class FPSController : NetworkBehavior
{
    [Networked] private float Pitch { get; set; }
    [Networked] private Vector3 Movement { get; set; }
    [Networked] private NetworkBool IsFiring { get; set; }

    void Start()
    {
        Debug.Log(this.StateBlockSize);
    }
}