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
    public NetworkWeapon networkWeapon;
    public WeaponModel weaponModel;


    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        if (IsServer)
        {
            OnResapwn();
        }
    }

    public void ChangeHp(int value)
    {
        HPoint = Mathf.Clamp(HPoint + value, 0, 100);
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
            if (weaponModel != null)
            {
                Destroy(weaponModel.gameObject);
                weaponModel = null;
            }

            if (networkWeapon != null)
            {
                networkWeapon.gameObject.SetActive(true);
                var rigidBody = networkWeapon.transform.GetComponent<Rigidbody>();

                // 设置武器的初始位置和旋转
                networkWeapon.transform.position = transform.position + transform.forward * 0.5f; // 向前偏移一点
                networkWeapon.transform.rotation = transform.rotation;

                rigidBody.velocity = Vector3.zero; // 清除可能存在的速度
                rigidBody.angularVelocity = Vector3.zero; // 清除可能存在的角速度
                rigidBody.AddForce(transform.forward * 5, ForceMode.Impulse);

                networkWeapon.OnThrow();
                networkWeapon = null;
            }
        }

        // 再生成新的武器
        if (WeaponRef == -1) return;
        NetworkObject networkObject = Entity.Engine.GetNetworkObject(new NetworkObjectRef(WeaponRef));
        networkWeapon = networkObject.GetComponent<NetworkWeapon>();
        if (networkWeapon == null) return;

        weaponModel = Instantiate(networkWeapon.weaponModel, owner.handPoint).GetComponent<WeaponModel>();
        networkWeapon.OnEquip(this);
        networkObject.gameObject.SetActive(false);
    }

    /// <summary>
    /// HP不会去做预测
    /// </summary>
    /// <param name="callbackData"></param>
    [NetworkCallBack(nameof(HPoint), true)]
    public void OnHpointChanged(CallbackData callbackData)
    {
        Debug.LogWarning($"Hp from {callbackData.GetPreviousData<int>()} To {HPoint}");

        if (HPoint <= 0)
        {
            OnDead();
        }

        if (IsClient)
        {
            int lastValue = callbackData.GetPreviousData<int>();
            if (lastValue > HPoint)
            {
                PlayClientDamageVFX();
            }

        }
    }

    public void OnDead()
    {
        owner.OnDead();
    }
    public void OnResapwn()
    {
        HPoint = 100;
        Armor = 100;
        ThrowWeapon();
    }

    private void PlayClientDamageVFX()
    {
        if (owner.IsLocalPlayer())
        {
            UIManager.Instance.GetUIPanel<UIPostProcessing>().OpenWithDuration(0.5f);
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().UpdateHP(HPoint);
        }
        else
        {

        }
    }
}