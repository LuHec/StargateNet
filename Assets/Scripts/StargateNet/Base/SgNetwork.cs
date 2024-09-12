using System;
using Riptide.Utils;
using Unity.VisualScripting;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    /// 网络部分运行载体
    /// </summary>
    public sealed class SgNetwork : MonoBehaviour
    {
        public static SgNetwork Instance => _instance;
        private SgNetworkGalaxy _sgNetworkGalaxy;
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

        public static SgNetworkGalaxy StartAsServer(ushort port, ushort maxClientCount)
        {
            return SgNetwork.Launch(StartMode.Server, new LaunchConfig()
            {
                configData = new SgNetConfigData()
                {
                    tickRate = 33.3333f,
                    maxClientCount = maxClientCount
                },
                port = port,
            });
        }

        public static SgNetworkGalaxy StartAsClient(ushort port)
        {
            return SgNetwork.Launch(StartMode.Client, new LaunchConfig()
            {
                configData = new SgNetConfigData()
                {
                    tickRate = 33.3333f,
                },
                port = port
            });
        }

        private void OnApplicationQuit()
        {
        }

        public static SgNetworkGalaxy Launch(StartMode startMode, LaunchConfig launchConfig)
        {
            if ((UnityEngine.Object)SgNetwork.Instance == (UnityEngine.Object)null)
            {
                SgNetwork.Init(launchConfig);
            }

            SgNetwork.Instance._sgNetworkGalaxy = new SgNetworkGalaxy();
            SgNetwork.Instance._sgNetworkGalaxy.Init(startMode, launchConfig.configData, launchConfig.port);
            return SgNetwork.Instance._sgNetworkGalaxy;
        }

        public static void Init(LaunchConfig launchConfig)
        {
            if (SgNetwork.Instance != null) return;
            GameObject sgNet = new GameObject("SgNetwork");
            sgNet.AddComponent<SgNetwork>();
            UnityEngine.Object.DontDestroyOnLoad(sgNet);
            SgNetwork.Instance._started = true;
        }


        private void Update()
        {
            if (this._sgNetworkGalaxy != null && this._started)
            {
                this._sgNetworkGalaxy.Engine.NetworkUpdate(Time.deltaTime, Time.timeScale);
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