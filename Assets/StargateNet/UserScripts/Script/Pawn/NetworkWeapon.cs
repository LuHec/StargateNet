using StargateNet;
using UnityEngine;

public class NetworkWeapon : NetworkBehavior
{
    // 客户端模型和数据分离
    public GameObject weaponModel;
    public int maxAmmo = 31;
    public float fireRate = 0.1f;
    [Replicated]
    public int AmmoCount{get;set;}
}