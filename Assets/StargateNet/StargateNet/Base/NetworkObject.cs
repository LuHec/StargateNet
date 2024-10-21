using System;
using UnityEngine;

namespace StargateNet
{
    [DisallowMultipleComponent]
    public class NetworkObject : MonoBehaviour, INetworkEntity
    {
        public Entity Entity { get; private set; }
        public int Id => this.Entity.networkId;
        public IStargateScript[] NetworkScripts { get; private set; }

        public int PrefabId
        {
            get => _prefabId;
            set => this._prefabId = value;
        }

        [SerializeField] internal int _prefabId = -1;

        public void Initialize(SgNetworkEngine engine, IStargateScript[] networkScripts)
        {
            this.NetworkScripts = networkScripts;
        }

        private void Start()
        {
            Debug.Log(PrefabId);
        }
    }
}