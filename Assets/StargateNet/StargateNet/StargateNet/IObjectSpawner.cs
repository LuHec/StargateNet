using UnityEngine;
using UnityEngine.SceneManagement;

namespace StargateNet
{
    public interface IObjectSpawner
    {
        public SgNetworkGalaxy Galaxy{get;set;}
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation);
        public void Despawn(GameObject go);
    }
}