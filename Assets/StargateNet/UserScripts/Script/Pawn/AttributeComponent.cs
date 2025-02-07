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

    public FPSController owner;

    private GameObject _weaponModel;
    private NetworkWeapon _networkWeapon;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        WeaponRef = -1;
    }

    public void SetNetworkWeapon(NetworkObject networkObject)
    {
        NetworkWeapon networkWeapon = networkObject.GetComponent<NetworkWeapon>();
        if (networkWeapon == null) return;
        WeaponRef = networkObject.NetworkId.refValue;
    }

    public void ThrowWeapon()
    {
        
    }

    [NetworkCallBack(nameof(WeaponRef), true)]
    public void OnWeaponRefChanged(CallbackData callbackData)
    {
        NetworkObject networkObject = Entity.engine.GetNetworkObject(new NetworkObjectRef(WeaponRef));
        _networkWeapon = networkObject.GetComponent<NetworkWeapon>();
        if(_networkWeapon== null) return;

        _weaponModel = Instantiate(_networkWeapon.weaponModel, owner.handPoint);
    }
}