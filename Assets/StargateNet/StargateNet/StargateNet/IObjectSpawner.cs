using UnityEngine;

namespace StargateNet
{
    public interface IObjectSpawner
    {
        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Object;
        public void Despawn(GameObject go);
    }
}