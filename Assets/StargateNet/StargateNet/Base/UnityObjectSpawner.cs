using UnityEngine;

namespace StargateNet
{
    public class UnityObjectSpawner : IObjectSpawner
    {
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Object.Instantiate(prefab, position, rotation);
        }

        public void Despawn(GameObject go)
        {
            Object.Destroy(go);
        }
    }
}