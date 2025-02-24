using UnityEngine;

public class WeaponModel : MonoBehaviour
{
    public Transform firePoint;
    public ParticleSystem muzzleFlashVfx;
    private ParticleSystem _muzzleFlashVfx;

    public void Start()
    {
    
    }

    public void FireVFX()
    {
        if(_muzzleFlashVfx == null)
        {
            _muzzleFlashVfx = Instantiate(muzzleFlashVfx, firePoint.position, firePoint.rotation);
            _muzzleFlashVfx.transform.parent = firePoint;
            _muzzleFlashVfx.transform.forward = transform.forward;
        }
        
        _muzzleFlashVfx.Play();
    }

    void OnDestroy()
    {
        Destroy(_muzzleFlashVfx);
    }
}