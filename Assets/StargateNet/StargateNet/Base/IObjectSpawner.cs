using UnityEngine;

namespace StargateNet
{
    public interface IObjectSpawner
    {
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
        public void Despawn(GameObject go);
    }
}