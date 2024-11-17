using System;
using UnityEngine;

namespace StargateNet
{
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour, INetworkEntity
    {
        public Entity Entity { get;  private set; }
        public NetworkObjectRef NetworkId => this.Entity.networkId;
        public IStargateScript[] NetworkScripts { get; private set; }
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

        public void Initialize(StargateEngine engine, Entity networkEntity, IStargateScript[] scripts)
        {
            this.engine = engine;   
            this.Entity = networkEntity;
            this.NetworkScripts = scripts;
            // TODO:暂时先写在这,后续可以在初始化的时候设置
            IStargateNetworkScript[] stargateNetworkScripts = this.GetComponentsInChildren<IStargateNetworkScript>();
            foreach (var sta in stargateNetworkScripts)
            {
                sta.Initialize(networkEntity);
            }
        }

        private void Start()
        {
            Debug.Log(PrefabId);
        }
    }
}