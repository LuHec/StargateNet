using UnityEngine;

namespace StargateNet
{
    public class NetworkEventManager : MonoBehaviour
    {
        public virtual void OnReadInput(SgNetworkGalaxy galaxy)
        {
            
        }
        
        public virtual void OnPlayerConnected(SgNetworkGalaxy galaxy, int playerId)
        {
            
        }
    }
}