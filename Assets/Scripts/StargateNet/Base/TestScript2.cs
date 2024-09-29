using UnityEngine;

namespace StargateNet
{
    public class TestScript2 : TestScript
    {
        [Networked] private int A { get; set; }
        [Networked] private float B { get; set; }
        [Networked] private NetworkBool N { get; set; }
        [Networked] private Vector3 V3 { get; set; }
        [Networked] private int R { get; set; }
        [Networked] private Vector3 V2 { get; set; }
        [Networked] private int T { get; set; }
        [Networked] private Vector3 V1 { get; set; }
        [Networked] private long Y { get; set; }

        [Networked] private int E { get; set; }
    }
}