using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class NetworkTransform : NetworkBehavior
{
    [Networked] private Vector3 Movement { get; set; }
    public override void NetworkFixedUpdate()
    {
        if (this.FetchInput(out NetworkInput input))
        {
            Movement += new Vector3(0.1f, 0.1f, 0.1f);
            this.transform.position = Movement;   
        }
    }
}
