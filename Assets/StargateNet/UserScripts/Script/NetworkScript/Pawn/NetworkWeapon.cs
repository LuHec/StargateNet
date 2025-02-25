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
    public int LastFireTick{ get; set; }   // 上次射击的Tick
    [Replicated]
    public int LastReloadTick{ get; set; }  // 上次装弹的Tick

    private float _secondsPerShot;  // 每次射击间隔
    private AttributeComponent _owner;
    private SgNetworkGalaxy _galaxy;

    public override void NetworkStart(SgNetworkGalaxy galaxy)
    {
        Init(galaxy);
        this._galaxy = galaxy;
    }

    public override void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
    {
        if (IsReloading)
        {
            if ((galaxy.tick.tickValue - LastReloadTick) * galaxy.FixedDeltaTime >= loadTime)
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

    /// <summary>
    /// 由于LastFireTick会在这个函数内赋值为servertick，所以服务端状态发过来时，客户端的currentTick就和lastfiretick相等了
    /// 这样重模拟时的Fire就无法触发。如果用lastTick的话，必须要下一帧再同步
    /// </summary>
    /// <param name="galaxy"></param>
    /// <param name="isFire"></param>
    /// <param name="isHoldFire"></param>
    /// <returns></returns>
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
        double pastTime = (currentTick.tickValue - LastFireTick) * galaxy.FixedDeltaTime;
        Debug.LogError($"TryFire:CurrentTick:{currentTick}, LastFireTick:{LastFireTick}, BurstCount:{BurstCount},PastTime:{pastTime}");
        if (pastTime > _secondsPerShot)
        {
            LastFireTick = currentTick.tickValue;
            AmmoCount--;
            this.BurstCount++;
            return true;
        }

        return false;
    }

    public void Reload(SgNetworkGalaxy galaxy)
    {
        IsReloading = true;
        LastReloadTick = galaxy.tick.tickValue;
    }

    public void Init(SgNetworkGalaxy galaxy)
    {
        _secondsPerShot = 60f / rpm;
        LastFireTick = Tick.InvalidTick.tickValue;
        AmmoCount = maxAmmo;
    }

    [NetworkCallBack(nameof(BurstCount), false)]
    public void OnBurstCountChanged(CallbackData callbackData)
    {
        if (IsServer || _owner == null) return;
        if (BurstCount > callbackData.GetPreviousData<int>())
        {
            // Debug.LogError($"CurrentTick:{_galaxy.tick}, BurstCount:{BurstCount}, Pre:{callbackData.GetPreviousData<int>()}");
            _owner.weaponModel.FireVFX();
        }
    }
}