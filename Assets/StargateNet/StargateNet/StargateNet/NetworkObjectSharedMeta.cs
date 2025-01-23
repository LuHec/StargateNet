using System.Collections.Generic;

namespace StargateNet
{
    public class NetworkObjectSharedMeta
    {
        public Dictionary<int, CallbackWrapper> callbacks = new Dictionary<int, CallbackWrapper>();
        
    }
}