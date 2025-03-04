using System.Collections;
using System.Collections.Generic;
using StargateNet;
using UnityEngine;

public class AttributeComponent : NetworkBehavior, IHitable
{
    [Replicated]
    public int TeamTag { get; set; }

    [Replicated]
    public int HPoint { get; set; }

    [Replicated]
    public int Armor { get; set; }

    [Replicated]
    public int WeaponRef { get; set; }

    public FPSController owner;
    public NetworkWeapon networkWeapon;
    public WeaponModel weaponModel;

    private BattleManager battleManager;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        if (IsServer)
        {
            battleManager = galaxy.FindSceneComponent<BattleManager>();
            OnResapwn();
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

    [NetworkCallBack(nameof(WeaponRef), false)]
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
        if (networkObject == null) return;
        networkWeapon = networkObject.GetComponent<NetworkWeapon>();
        if (networkWeapon == null) return;

        weaponModel = Instantiate(networkWeapon.weaponModel, owner.handPoint).GetComponent<WeaponModel>();
        // 设置武器模型引用
        if (owner.IsLocalPlayer())
        {
            owner.weaponPresenter.SetWeaponModel(weaponModel.transform);
        }
        networkWeapon.OnEquip(this);
        networkObject.gameObject.SetActive(false);
        if (IsClient)
        {
            UIManager.Instance.GetUIPanel<UIPlayerInterface>().UpdateMag(networkWeapon.AmmoCount);
        }
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

        if (IsClient && HPoint < callbackData.GetPreviousData<int>())
        {
            PlayClientDamageVFX();
        }
    }

    public void OnDead()
    {
        if (IsServer)
        {
            ThrowWeapon();
            owner.SetDead(true);
            battleManager.AddRespawnTimer(7.0f, this, killer);
            this.killer = null;
        }
    }

    public void OnResapwn()
    {
        HPoint = 100;
        Armor = 100;
        ThrowWeapon();
        if (IsServer)
        {
            owner.SetDead(false);
            RequireWeapon();
        }
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

    public void RequireWeapon()
    {
        NetworkObject networkWeapon = battleManager.RequireWeapon(this);
        SetNetworkWeapon(networkWeapon);
    }

    public FPSController killer;
    public void OnHit(int damage, Vector3 hitPoint, Vector3 hitNormal, FPSController hitter)
    {
        if (TeamTag == hitter.attributeComponent.TeamTag) return;
        int temp = HPoint;
        killer = hitter;
        HPoint = Mathf.Clamp(HPoint + damage, 0, 100);
    }
}