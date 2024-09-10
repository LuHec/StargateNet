using System;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    /// 网络部分运行载体
    /// </summary>
    public sealed class SgNetwork : MonoBehaviour
    {
        public static SgNetwork Instance => _instance;
        private NetworkGalaxy _networkGalaxy;
        private static SgNetwork _instance;
        private bool _started;

        internal SgNetwork()
        {
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                UnityEngine.Object.Destroy(_instance);
            }
            else
            {
                SgNetwork._instance = this;
            }
        }

        public void StartAsServer(ushort port, ushort maxClientCount)
        {
            _networkGalaxy = new ServerNetworkGalaxy(port, maxClientCount);
            SgNetwork.Launch(StartMode.Server);
        }

        public void StartAsClient(string ip, ushort port)
        {
            _networkGalaxy = new ClientNetworkGalaxy(ip, port);
            SgNetwork.Launch(StartMode.Client);
        }
        
        private void OnApplicationQuit()
        {
            _networkGalaxy.OnQuit();
        }

        public static void Launch(StartMode startMode)
        {
            SgNetwork.Instance._networkGalaxy.NetworkStart();
            SgNetwork.Instance._networkGalaxy.Connect();
        }

        private void Update()
        {
            if (_networkGalaxy != null && _started)
            {
                _networkGalaxy.NetworkUpdate();
            }
        }

        private void LateUpdate()
        {
            
        }

        private void FixedUpdate()
        {
            
        }
    }
}