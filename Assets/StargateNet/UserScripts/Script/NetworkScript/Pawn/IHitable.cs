using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IHitable 
{
    void OnHit(int damage, Vector3 hitPoint, Vector3 hitNormal, FPSController hitter);
}
