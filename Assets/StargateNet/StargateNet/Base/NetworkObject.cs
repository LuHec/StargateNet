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
        }

        private void Start()
        {
            Debug.Log(PrefabId);
        }
    }
}