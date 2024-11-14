using UnityEngine;

namespace StargateNet
{
    public class UnityObjectSpawner : IObjectSpawner
    {
        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Object
        {
            return Object.Instantiate<T>(prefab, position, rotation);
        }

        public void Despawn(GameObject go)
        {
            Object.Destroy(go);
        }
    }
}