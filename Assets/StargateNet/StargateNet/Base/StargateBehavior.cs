using UnityEngine;

namespace StargateNet
{
    public abstract class StargateBehavior : MonoBehaviour, IStargateNetworkScript, IStargateScript
    {
        public unsafe int* StateBlock { get; internal set; } // 由Entity构造时分发
        public Entity Entity { get; internal set; }
        public virtual void NetworkStart()
        {
            
        }

        public virtual void NetworkUpdate()
        {
            
        }

        public virtual void NetworkFixedUpdate()
        {
            
        }

        public virtual void NetworkRender()
        {
            
        }

        public virtual void NetworkDestroy()
        {
            
        }
    }
}