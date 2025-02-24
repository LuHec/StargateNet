using StargateNet;
using UnityEngine;

public class NetworkWeapon : NetworkBehavior
{
    [Header("武器配置")]
    public GameObject weaponModel;
    public int maxAmmo = 31;
    public float rpm = 850f;
    public float loadTime = 2f;

    [Replicated]
    public int AmmoCount { get; set; }
    [Replicated]
    public NetworkBool IsReloading { get; set; }

    private float _secondsPerShot;  // 每次射击间隔
    private Tick _lastFireTick;   // 上次射击的Tick
    private Tick _lastReloadTick;  // 上次装弹的Tick

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

    public void OnEquip()
    {

    }

    public void OnThrow()
    {

    }

    public bool TryFire(SgNetworkGalaxy galaxy)
    {
        if (AmmoCount <= 0)
        {
            Reload(galaxy);
            return false;
        }

        if (IsReloading) IsReloading = false;

        Tick currentTick = galaxy.tick;
        double pastTime = (currentTick - _lastFireTick) * galaxy.FixedDeltaTime;
        Debug.LogWarning($"PastTime: {pastTime}");
        if (pastTime > _secondsPerShot)
        {
            _lastFireTick = currentTick;
            AmmoCount--;
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
}