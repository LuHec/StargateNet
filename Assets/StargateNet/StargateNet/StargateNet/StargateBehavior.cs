using UnityEngine;

namespace StargateNet
{
    
    public abstract class StargateBehavior : MonoBehaviour, IStargateNetworkScript, IStargateScript
    {
        public unsafe int* StateBlock { get; internal set; } // 由Entity构造时分发,是分块的内存
        public Entity Entity { get; internal set; }
        public int ScriptIdx { get; set; }
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

        public virtual void NetworkLaterStart(SgNetworkGalaxy galaxy)
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
        
        public virtual void InternalInit()
        {
            
        }

        public virtual void InternalReset()
        {
        }

        public virtual void InternalRegisterRPC()
        {
            
        }

        public void SetAlwaysSync(bool alawaysSyncSet)
        {
            Entity.SetAlwaysSync(alawaysSyncSet);
        }
    }
}