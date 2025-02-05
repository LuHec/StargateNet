using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class AttributeComponent : NetworkBehavior
{
    [Replicated]
    public int HPoint { get; set; }

    [Replicated]
    public int Armor { get; set; }

    [Replicated]
    public int WeaponRef { get; set; }

    private GameObject _weapon;

    [NetworkCallBack(nameof(WeaponRef), false)]
    public void OnWeaponRefChanged(CallbackData callbackData)
    {
        
    }
}