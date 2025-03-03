using UnityEngine;

namespace StargateNet
{
    public class NetworkEventManager : MonoBehaviour
    {
        public virtual void OnNetworkEngineStart(SgNetworkGalaxy galaxy) { }
        
        public virtual void OnReadInput(SgNetworkGalaxy galaxy)
        {
            
        }
        
        public virtual void OnPlayerConnected(SgNetworkGalaxy galaxy, int playerId)
        {
            
        }

        public virtual void OnPlayerPawnLoad(SgNetworkGalaxy galaxy, int playerId, NetworkObject networkObject)
        {
            
        }

        public virtual void OnLocalPlayerSpawned()
        {
            
        }
    }
}