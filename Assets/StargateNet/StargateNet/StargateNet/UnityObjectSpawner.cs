using UnityEngine;
using UnityEngine.SceneManagement;

namespace StargateNet
{
    public class UnityObjectSpawner : IObjectSpawner
    {
        public SgNetworkGalaxy Galaxy {get;set;}

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            GameObject go = Object.Instantiate<GameObject>(prefab, position, rotation);
            if(go.scene != Galaxy.Scene) 
            {
                SceneManager.MoveGameObjectToScene(go, Galaxy.Scene);
            }
            return go;
        }

        public void Despawn(GameObject go)
        {
            Object.Destroy(go);
        }
    }
}