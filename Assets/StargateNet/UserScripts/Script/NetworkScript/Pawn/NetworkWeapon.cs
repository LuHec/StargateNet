using System;
using Riptide;
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
    [Replicated]
    public Tick LastFireTick;   // 上次射击的Tick
    [Replicated]
    public Tick LastReloadTick;  // 上次装弹的Tick

    private float _secondsPerShot;  // 每次射击间隔
    private AttributeComponent _owner;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        Init(galaxy);
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsReloading)
        {
            if ((galaxy.tick - LastReloadTick) * galaxy.FixedDeltaTime >= loadTime)
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
        if (!isRifile && !isFire) return false;
        if (IsReloading) IsReloading = false;

        Tick currentTick = galaxy.tick;
        double pastTime = (currentTick - LastFireTick) * galaxy.FixedDeltaTime;
        if (pastTime > _secondsPerShot)
        {
            LastFireTick = currentTick;
            AmmoCount--;
            this.BurstCount++;
            return true;
        }

        return false;
    }

    public void Reload(SgNetworkGalaxy galaxy)
    {
        IsReloading = true;
        LastReloadTick = galaxy.tick;
    }

    public void Init(SgNetworkGalaxy galaxy)
    {
        _secondsPerShot = 60f / rpm;
        LastFireTick = galaxy.tick;
        AmmoCount = maxAmmo;
    }

    int t = 0;
    [NetworkCallBack(nameof(BurstCount), false)]
    public void OnBurstCountChanged(CallbackData callbackData)
    {
        if (IsServer || _owner == null) return;
        if (BurstCount > callbackData.GetPreviousData<int>())
        {
            t = t + 1;
            Debug.LogError($"BurstCount:{BurstCount}, Pre:{callbackData.GetPreviousData<int>()}");
            _owner.weaponModel.FireVFX();
        }
    }
}