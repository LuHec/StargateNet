using System;
using Riptide.Utils;
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

        public static void StartAsServer(ushort port, ushort maxClientCount)
        {
            SgNetwork.Instance._networkGalaxy = new NetworkGalaxy();
            SgNetwork.Launch(StartMode.Server);
        }

        public static void StartAsClient(string ip, ushort port)
        {
            SgNetwork.Instance._networkGalaxy = new NetworkGalaxy();
            SgNetwork.Launch(StartMode.Client);
        }

        private void OnApplicationQuit()
        {
            _networkGalaxy.Transport.OnQuit();
        }

        public static void Launch(StartMode startMode)
        {
            SgNetwork.Instance.Launch();
            SgNetwork.Instance._networkGalaxy.Transport.TransportStart();
            SgNetwork.Instance._networkGalaxy.Transport.Connect();
        }

        public void Launch()
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
        }

        private void Update()
        {
            if (_networkGalaxy != null && _started)
            {
                _networkGalaxy.Transport.TransportUpdate();
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