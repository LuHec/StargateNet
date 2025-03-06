using UnityEngine;

public class WeaponModel : MonoBehaviour
{
    [Header("Fire Effects")]
    public Transform firePoint;
    public ParticleSystem muzzleFlashVfx;
    private ParticleSystem _muzzleFlashVfx;

    [Header("Visual Bullet")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 100f;
    [SerializeField] private float bulletLifeTime = 3f;
    
    private Transform _cameraPoint;
    private bool isLocal;

    public void Init(FPSController fPSController)
    {
        _cameraPoint = fPSController.cameraPoint;
        isLocal = fPSController.IsLocalPlayer();
    }

    public void FireVFX()
    {
        // 枪口特效
        if(_muzzleFlashVfx == null)
        {
            _muzzleFlashVfx = Instantiate(muzzleFlashVfx, firePoint.position, firePoint.rotation);
            _muzzleFlashVfx.transform.parent = firePoint;
            _muzzleFlashVfx.transform.forward = transform.forward;
        }
        _muzzleFlashVfx.Play();

        // 生成假子弹
        SpawnVisualBullet();
    }

    private void SpawnVisualBullet()
{
    if(!isLocal) return;
    if (bulletPrefab == null || firePoint == null || _cameraPoint == null) return;

    // 计算从枪口到视线上的方向
    Ray ray = new Ray(_cameraPoint.position, _cameraPoint.forward);
    Vector3 targetPoint = ray.GetPoint(100f);
    Vector3 shootDirection = (targetPoint - firePoint.position).normalized;
    
    // 从枪口位置生成子弹，使用 LookRotation 计算朝向
    GameObject bullet = Instantiate(bulletPrefab, firePoint.position, 
        Quaternion.LookRotation(shootDirection));
    
    // 初始化子弹行为
    BulletBehaviour bulletBehaviour = bullet.AddComponent<BulletBehaviour>();
    bulletBehaviour.Initialize(shootDirection * bulletSpeed, bulletLifeTime);
}

    void OnDestroy()
    {
        if (_muzzleFlashVfx != null)
        {
            Destroy(_muzzleFlashVfx.gameObject);
        }
    }
}