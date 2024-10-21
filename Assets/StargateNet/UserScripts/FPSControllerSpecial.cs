using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class FPSControllerSpecial : FPSController
{
    [Networked] private int Armor { get; set; }
    [Networked] private int Hp { get; set; }
}
