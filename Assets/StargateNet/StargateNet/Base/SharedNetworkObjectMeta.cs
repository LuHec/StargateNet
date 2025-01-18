using System.Collections.Generic;

namespace StargateNet
{
    public class SharedNetworkObjectMeta
    {
        public Dictionary<int, CallbackWrapper> callbacks = new Dictionary<int, CallbackWrapper>();  
    }
}