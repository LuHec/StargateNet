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
        public SgNetworkGalaxy sgNetworkGalaxy;
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

        private static StargateConfigData CreateConfigData(StargateConfig config)
        {
            return new StargateConfigData()
            {
                tickRate = config.FPS,
                isPhysic2D = config.IsPhysic2D,
                maxClientCount = config.MaxClientCount,
                runAsHeadless = config.RunAsHeadless,
                maxNetworkObjects = config.maxNetworkObject,
                savedSnapshotsCount = config.SavedSnapshotsCount,
                maxPredictedTicks = config.MaxPredictedTicks,
                maxSnapshotSendSize = config.maxSnapshotSendSize,
                networkPrefabs = config.NetworkObjects,
                maxObjectStateBytes = config.maxObjectStateBytes,
                networkInputsTypes = config.networkInputsTypes,
                networkInputsBytes = config.networkInputsBytes,
            };
        }

        public static SgNetworkGalaxy StartAsServer(ushort port, ushort maxClientCount)
        {
            var config = Resources.Load<StargateConfig>("StargateConfig");
            return SgNetwork.Launch(StartMode.Server, new LaunchConfig()
            {
                configData = CreateConfigData(config),
                port = port
            });
        }

        public static SgNetworkGalaxy StartAsClient(ushort port)
        {
            var config = Resources.Load<StargateConfig>("StargateConfig");
            return SgNetwork.Launch(StartMode.Client, new LaunchConfig()
            {
                configData = CreateConfigData(config),
                port = port
            });
        }
        
        private void OnApplicationQuit()
        {
        }

        public static SgNetworkGalaxy Launch(StartMode startMode, LaunchConfig launchConfig, IMemoryAllocator allocator = null, IObjectSpawner spawner = null)
        {
            if (SgNetwork.Instance == null)
            {
                SgNetwork.Init(launchConfig);
            }

            SgNetwork.Instance.sgNetworkGalaxy = new SgNetworkGalaxy();
            SgNetwork.Instance.monitor = new Monitor();
            NetworkEventManager[] networkEventManagers = FindObjectsOfType<NetworkEventManager>();
            NetworkEventManager networkEventManager = networkEventManagers.Length > 0 ? networkEventManagers[0] : null;
            SgNetwork.Instance.sgNetworkGalaxy.Init(startMode, launchConfig.configData, launchConfig.port,
                SgNetwork.Instance.monitor, new LagCompensateComponent(), allocator ?? new UnityAllocator(), spawner ?? new UnityObjectSpawner(),
                networkEventManager);
            return SgNetwork.Instance.sgNetworkGalaxy;
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
            if (this.sgNetworkGalaxy != null && this._started)
            {
                this.sgNetworkGalaxy.NetworkUpdate();
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