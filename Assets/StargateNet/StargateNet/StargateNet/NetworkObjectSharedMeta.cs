using System.Collections.Generic;

namespace StargateNet
{
    /// <summary>
    /// 每种NetworkObject共享一个Meta
    /// </summary>
    public class NetworkObjectSharedMeta
    {
        // 每个NetworkObject的属性内存地址不会相同，可以保证key唯一性
        public Dictionary<int, CallbackWrapper> callbacks = new Dictionary<int, CallbackWrapper>();
        
    }
}