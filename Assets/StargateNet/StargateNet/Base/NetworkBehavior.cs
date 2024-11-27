using UnityEngine;

namespace StargateNet
{
    public abstract class NetworkBehavior : StargateBehavior
    {
        public virtual int StateBlockSize => 0; // 字节数，ILProcessor会算出大小

        protected bool FetchInput<T>(out T input) where T : INetworkInput
        {
            return this.Entity.FetchInput(out input);
        }
    }
}