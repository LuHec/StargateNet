using UnityEngine;

namespace StargateNet
{
    public class NetworkEventManager
    {
        internal StargateEngine engine;

        public NetworkEventManager(StargateEngine engine)
        {
            this.engine = engine;
        }
        
        internal void OnPlayerConnected(int playerId)
        {
            this.engine.NetworkSpawn(this.engine.PrefabsTable[0].gameObject, Vector3.zero, Quaternion.identity, playerId);
        }
    }
}