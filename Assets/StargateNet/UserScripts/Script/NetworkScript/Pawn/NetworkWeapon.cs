using System;
using StargateNet;
using UnityEngine;

public class NetworkWeapon : NetworkBehavior
{
    [Header("武器配置")]
    public GameObject weaponModel;
    public int maxAmmo = 31;
    public float rpm = 850f;
    public float loadTime = 2f;
    public bool isRifile = false;

    [Replicated]
    public int AmmoCount { get; set; }
    [Replicated]
    public NetworkBool IsReloading { get; set; }
    /// <summary>
    /// 用于做开火特效的同步
    /// </summary>
    [Replicated]
    public int BurstCount { get; set; }

    private float _secondsPerShot;  // 每次射击间隔
    private Tick _lastFireTick;   // 上次射击的Tick
    private Tick _lastReloadTick;  // 上次装弹的Tick
    private AttributeComponent _owner;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        Init(galaxy);
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsReloading)
        {
            if ((galaxy.tick - _lastReloadTick) * galaxy.FixedDeltaTime >= loadTime)
            {
                AmmoCount = maxAmmo;
                IsReloading = false;
            }
        }
    }

    public void OnEquip(AttributeComponent onwer)
    {
        _owner = onwer;
    }

    public void OnThrow()
    {
        _owner = null;
    }

    public bool TryFire(SgNetworkGalaxy galaxy, bool isFire, bool isHoldFire)
    {
        if (AmmoCount <= 0)
        {
            Reload(galaxy);
            return false;
        }
        if(!isRifile && !isFire) return false;
        if (IsReloading) IsReloading = false;

        Tick currentTick = galaxy.tick;
        double pastTime = (currentTick - _lastFireTick) * galaxy.FixedDeltaTime;
        if (pastTime > _secondsPerShot)
        {
            _lastFireTick = currentTick;
            AmmoCount--;
            this.BurstCount++;
            return true;
        }

        return false;
    }

    public void Reload(SgNetworkGalaxy galaxy)
    {
        IsReloading = true;
        _lastReloadTick = galaxy.tick;
    }

    public void Init(SgNetworkGalaxy galaxy)
    {
        _secondsPerShot = 60f / rpm;
        _lastFireTick = galaxy.tick;
        AmmoCount = maxAmmo;
    }

    [NetworkCallBack(nameof(BurstCount), false)]
    public void OnBurstCountChanged(CallbackData callbackData)
    {
        if (IsServer) return;
        if (BurstCount > callbackData.GetPreviousData<int>())
        {
            _owner.weaponModel.FireVFX();
        }
    }
}