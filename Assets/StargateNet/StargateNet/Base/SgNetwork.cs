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
        public Monitor monitor;
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
            var config = Resources.Load<StargateConfig>("StargateConfig");
            return SgNetwork.Launch(StartMode.Server, new LaunchConfig()
            {
                configData = new StargateConfigData()
                {
                    tickRate = config.FPS,
                    maxClientCount = config.MaxClientCount,
                    runAsHeadless = config.RunAsHeadless,
                    maxNetworkObjects = config.maxNetworkObject,
                    savedSnapshotsCount = config.SavedSnapshotsCount,
                    maxPredictedTicks = config.MaxPredictedTicks,
                    networkPrefabs = config.NetworkObjects,
                },
                port = port
            });
        }

        public static SgNetworkGalaxy StartAsClient(ushort port)
        {
            var config = Resources.Load<StargateConfig>("StargateConfig");
            return SgNetwork.Launch(StartMode.Client, new LaunchConfig()
            {
                configData = new StargateConfigData()
                {
                    tickRate = config.FPS,
                    maxClientCount = config.MaxClientCount,
                    runAsHeadless = config.RunAsHeadless,
                    maxNetworkObjects = config.maxNetworkObject,
                    savedSnapshotsCount = config.SavedSnapshotsCount,
                    maxPredictedTicks = config.MaxPredictedTicks,
                    networkPrefabs = config.NetworkObjects,
                },
                port = port
            });
        }

        private void OnApplicationQuit()
        {
        }

        public static SgNetworkGalaxy Launch(StartMode startMode, LaunchConfig launchConfig,
            IMemoryAllocator allocator = null)
        {
            if (SgNetwork.Instance == null)
            {
                SgNetwork.Init(launchConfig);
            }

            SgNetwork.Instance._sgNetworkGalaxy = new SgNetworkGalaxy();
            SgNetwork.Instance.monitor = new Monitor();
            SgNetwork.Instance._sgNetworkGalaxy.Init(startMode, launchConfig.configData, launchConfig.port,
                SgNetwork.Instance.monitor, allocator ?? new UnityAllocator());
            return SgNetwork.Instance._sgNetworkGalaxy;
        }

        public static void Init(LaunchConfig launchConfig)
        {
            if (SgNetwork.Instance != null) return;
            GameObject sgNet = new GameObject("StargateNetwork");
            sgNet.AddComponent<SgNetwork>();
            UnityEngine.Object.DontDestroyOnLoad(sgNet);
            SgNetwork.Instance._started = true;
        }

        /// <summary>
        /// Run SgNetwork Logic
        /// </summary>
        private void Update()
        {
            if (this._sgNetworkGalaxy != null && this._started)
            {
                this._sgNetworkGalaxy.NetworkUpdate();
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