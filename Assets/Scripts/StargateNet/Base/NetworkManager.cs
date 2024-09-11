using System;
using Riptide.Utils;
using UnityEngine;

namespace StargateNet
{
    public class NetworkManager : MonoBehaviour
    {
        private static NetworkManager _networkManager;

        public static NetworkManager Singleton
        {
            get { return _networkManager; }
            private set
            {
                if (_networkManager == null)
                {
                    _networkManager = value;
                }
                else if (_networkManager != value)
                {
                    Debug.Log($"{nameof(NetworkManager)} instance has already exists, destroying duplicate");
                    Destroy(value);
                }
            }
        }

        [Header("Server")] [SerializeField] private ushort serverPort;
        [SerializeField] private ushort maxClientCount;

        [Header("Client")] [SerializeField] private string toServerIp;

        private void Awake()
        {
            Singleton = this;
        }

        public void StartAsServer()
        {
            SgNetwork.StartAsServer(serverPort, maxClientCount);
        }

        public void StartAsClient()
        {
            SgNetwork.StartAsClient(toServerIp, serverPort);
        }
        
    
        private NetworkManager()
        {
            
        }
    }
}