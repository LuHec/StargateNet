using UnityEngine;

namespace StargateNet
{
    public abstract class NetworkBehavior : StargateBehavior
    {
        public virtual int StateBlockSize => 0; // 字节数，ILProcessor会算出大小

        protected bool FetchInput<T>(out T input) where T : unmanaged, INetworkInput
        {
            return this.Entity.FetchInput(out input);
        }

        protected bool IsLocalPlayer() => this.Entity.engine.IsClient && this.Entity.inputSource == this.Entity.engine.Client.Client.Id;
    }
}