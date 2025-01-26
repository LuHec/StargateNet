using System.Collections.Generic;

namespace StargateNet
{
    public class StargateReflectionData
    {
        /// <summary>
        /// prefabId:meta
        /// </summary>
        internal Dictionary<int, NetworkObjectSharedMeta> NetworkObjectSharedMetas {private set; get; }
        
        public StargateReflectionData()
        {
            NetworkObjectSharedMetas = new Dictionary<int, NetworkObjectSharedMeta>(512);
        }

        internal NetworkObjectSharedMeta GetNetworkObjectSharedMeta(int hashCode)
        {
            if (!NetworkObjectSharedMetas.TryGetValue(hashCode, out NetworkObjectSharedMeta networkObjectSharedMeta))
            {
                NetworkObjectSharedMetas.Add(hashCode, new NetworkObjectSharedMeta());
            }

            return networkObjectSharedMeta;
        }
        
    }
}