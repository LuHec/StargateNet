using System.Collections;
using System.Collections.Generic;
using StargateNet;
using Unity.VisualScripting;
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
        if (IsServer)
        {
            HPoint = 100;
            Armor = 100;
            WeaponRef = -1;
        }
    }

    public void SetNetworkWeapon(NetworkObject networkObject)
    {
        if (networkObject == null) return;
        NetworkWeapon networkWeapon = networkObject.GetComponent<NetworkWeapon>();
        if (networkWeapon == null) return;
        WeaponRef = networkObject.NetworkId.refValue;
    }

    public void ThrowWeapon()
    {
        WeaponRef = -1;
    }

    [NetworkCallBack(nameof(WeaponRef), true)]
    public void OnWeaponRefChanged(CallbackData callbackData)
    {
        Debug.LogWarning($"ChangeWeapon To {WeaponRef}");
        // 先丢弃之前的武器
        if (callbackData.GetPreviousData<int>() != -1)
        {
            if (_weaponModel != null)
            {
                Destroy(_weaponModel);
                _weaponModel = null;
            }

            if (_networkWeapon != null)
            {
                _networkWeapon.gameObject.SetActive(true);
                _networkWeapon.transform.GetComponent<Rigidbody>().AddForce(owner.transform.forward * 5, ForceMode.Impulse);
                _networkWeapon = null;
            }
        }

        // 再生成新的武器
        if (WeaponRef == -1) return;
        NetworkObject networkObject = Entity.engine.GetNetworkObject(new NetworkObjectRef(WeaponRef));
        _networkWeapon = networkObject.GetComponent<NetworkWeapon>();
        if (_networkWeapon == null) return;

        _weaponModel = Instantiate(_networkWeapon.weaponModel, owner.handPoint);
        networkObject.gameObject.SetActive(false);
    }

    [NetworkCallBack(nameof(HPoint), true)]
    public void OnHpointChanged(CallbackData callbackData)
    {
        Debug.LogWarning($"Hp from {callbackData.GetPreviousData<int>()} To {HPoint}");
    }
}