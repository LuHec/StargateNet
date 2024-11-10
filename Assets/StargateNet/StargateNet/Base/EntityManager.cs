using System.Collections.Generic;

namespace StargateNet
{
    public class EntityManager
    {
        public List<int> changedMeta;
        public Dictionary<NetworkObjectRef, NetworkObject> networkObjects;

        public EntityManager(int maxEntities)
        {
            changedMeta = new List<int>(maxEntities);
            networkObjects = new(maxEntities);
        }
    }
}