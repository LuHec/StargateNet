using UnityEngine;

namespace StargateNet
{
    
    public abstract class StargateBehavior : MonoBehaviour, IStargateNetworkScript, IStargateScript
    {
        public unsafe int* StateBlock { get; internal set; } // 由Entity构造时分发
        public Entity Entity { get; internal set; }
        public int InputSource => this.Entity.inputSource;

        public void Initialize(Entity entity)
        {
            this.Entity = entity;
        }
        
        protected bool IsClient => Entity.engine.IsClient;
        protected bool IsServer => Entity.engine.IsServer;

        /// <summary>
        /// 给IL层注册回调函数
        /// </summary>
        public virtual void Init()
        {
        }

        public virtual void NetworkStart(SgNetworkGalaxy galaxy)
        {
        }

        public virtual void NetworkUpdate(SgNetworkGalaxy galaxy)
        {
        }

        public virtual void NetworkFixedUpdate(SgNetworkGalaxy galaxy)
        {
        }

        public virtual void NetworkRender(SgNetworkGalaxy galaxy)
        {
        }

        public virtual void NetworkDestroy(SgNetworkGalaxy galaxy)
        {
        }

        public virtual void SerializeToNetcode()
        {
            
        }

        public virtual void DeserializeToGameCode()
        {
            
        }
        
        public void InternalInit()
        {
            
        }

        public void InternalReset()
        {
        }

    }
}