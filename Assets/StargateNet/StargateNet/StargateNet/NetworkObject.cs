using System;
using UnityEngine;

namespace StargateNet
{
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour, INetworkEntity
    {
        public Entity Entity { get;  private set; }
        public NetworkObjectRef NetworkId => this.Entity.networkId;
        public IStargateNetworkScript[] NetworkScripts { get; private set; }
        public StargateEngine engine;
        
        /// <summary>
        /// 由编辑器生成的id
        /// </summary>
        public int PrefabId
        {
            get => _prefabId;
            set => this._prefabId = value;
        }
        
        [SerializeField] private int _prefabId = -1;

        public void Initialize(StargateEngine engine, Entity networkEntity, IStargateNetworkScript[] scripts)
        {
            this.engine = engine;   
            this.Entity = networkEntity;
            this.NetworkScripts = scripts;
            // TODO:暂时先写在这,后续可以在初始化的时候设置
            foreach (var sta in this.NetworkScripts)
            {
                sta.Initialize(networkEntity);
            }
        }
        
        
    }
}