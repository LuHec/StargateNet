using UnityEngine;

namespace StargateNet
{
    public class NetworkObject : MonoBehaviour, INetworkEntity
    {
        public Entity Entity { get; private set; }
        public int Id => this.Entity.networkId;
        public IStargateScript[] NetworkScripts { get; private set; }

        public void Initialize(SgNetworkEngine engine, IStargateScript[] networkScripts)
        {
            this.NetworkScripts = networkScripts;
        }
    }
}