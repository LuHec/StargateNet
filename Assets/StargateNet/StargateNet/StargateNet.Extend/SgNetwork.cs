using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Collections;

namespace StargateNet
{
    /// <summary>
    /// 网络部分运行载体
    /// </summary>
    public sealed class SgNetwork : MonoBehaviour
    {
        public static SgNetwork Instance => _instance;
        public Monitor monitor;
        private static SgNetwork _instance;
        private bool _started;

        internal List<SgNetworkGalaxy> galaxies = new List<SgNetworkGalaxy>(4);

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

        private void AddGalaxy(SgNetworkGalaxy galaxy)
        {
            this.galaxies.Add(galaxy);
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
                AoIBound = config.AoIBound,
                AoIRange = config.AoIRange,
                AoIUnloadRange = config.AoIUnloadRange,
                WorldSize = config.WorldSize,
                networkPrefabs = config.NetworkObjects,
                maxObjectStateBytes = config.maxObjectStateBytes,
                networkInputsTypes = config.networkInputsTypes,
                networkInputsBytes = config.networkInputsBytes,
            };
        }

        public static SgNetworkGalaxy StartAsServer(ushort port)
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

        public static SgNetworkGalaxy StartAsServerAndBot(ushort port)
        {
            var config = Resources.Load<StargateConfig>("StargateConfig");
            return SgNetwork.Launch(StartMode.ServerAndBot, new LaunchConfig()
            {
                configData = CreateConfigData(config),
                port = port
            });
        }

        private void OnApplicationQuit()
        {
        }

        public static SgNetworkGalaxy Launch(StartMode startMode,
            LaunchConfig launchConfig,
            IMemoryAllocator allocator = null,
            IObjectSpawner spawner = null)
        {
            if (SgNetwork.Instance == null)
            {
                SgNetwork.Init(launchConfig);
            }

            Scene currentScene = SceneManager.GetActiveScene();

            // 处理 ServerAndBot 模式
            if (startMode == StartMode.ServerAndBot)
            {
                // var serverGalaxy = CreateGalaxy(StartMode.Server, currentScene, launchConfig, allocator, spawner);
                Instance.StartCoroutine(LoadBotScenes(currentScene, launchConfig, allocator, spawner));
                return null;
            }

            return CreateGalaxy(startMode, currentScene, launchConfig, allocator, spawner);
        }

        private static IEnumerator LoadBotScenes(Scene currentScene,
            LaunchConfig launchConfig,
            IMemoryAllocator allocator = null,
            IObjectSpawner spawner = null)
        {
            for (int i = 0; i < 2; i++)
            {
                var operation = SceneManager.LoadSceneAsync(currentScene.buildIndex, new LoadSceneParameters()
                {
                    loadSceneMode = LoadSceneMode.Additive,
                    localPhysicsMode = LocalPhysicsMode.Physics3D,
                });

                // 等待场景加载完成
                while (!operation.isDone)
                {
                    yield return null;
                }

                Scene botScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
                // 确保主场景保持激活状态
                SceneManager.SetActiveScene(currentScene);

                GameObject[] rootObjects = botScene.GetRootGameObjects();
                foreach (var obj in rootObjects)
                {
                    if (obj.TryGetComponent<GameStarter>(out var gameStarter))
                    {
                        gameStarter.IsBotScene = true;
                        var botGalaxy = CreateGalaxy(StartMode.Bot, botScene, launchConfig, allocator, spawner);
                        botGalaxy.Connect("127.0.0.1", launchConfig.port);
                        Debug.Log($"Bot scene {i} created and connecting");
                        break;
                    }
                }

                // 可选：添加一个短暂延迟，避免同时创建太多连接
                yield return new WaitForSeconds(0.1f);
            }

            Debug.Log("All bot scenes loaded and connected");
        }

        private static SgNetworkGalaxy CreateGalaxy(StartMode startMode, Scene scene,
            LaunchConfig launchConfig,
            IMemoryAllocator allocator = null,
            IObjectSpawner spawner = null)
        {
            var galaxyObj = new GameObject($"SgNetworkGalaxy_{Instance.galaxies.Count}");
            var galaxy = galaxyObj.AddComponent<SgNetworkGalaxy>();
            DontDestroyOnLoad(galaxy);
            Instance.galaxies.Add(galaxy);

            Monitor monitor = new Monitor();
            if (startMode != StartMode.Bot)
                Instance.monitor = monitor;

            // 如果是 Bot 场景，禁用所有输入相关组件
            if (startMode == StartMode.Bot)
            {
                DisableBotSceneComponents(scene);
            }

            // 获取当前场景的 NetworkEventManager
            NetworkEventManager networkEventManager = null;
            var managers = UnityEngine.Object.FindObjectsOfType<NetworkEventManager>();
            foreach (var manager in managers)
            {
                if (manager.gameObject.scene == scene)
                {
                    networkEventManager = manager;
                    break;
                }
            }

            if (networkEventManager == null)
            {
                var eventManagerObj = new GameObject("NetworkEventManager");
                networkEventManager = eventManagerObj.AddComponent<NetworkEventManager>();
                SceneManager.MoveGameObjectToScene(eventManagerObj, scene);
            }


            galaxy.Init(startMode,
                scene,
                launchConfig.configData,
                launchConfig.port,
                monitor,
                new LagCompensateComponent(),
                allocator ?? new UnityAllocator(),
                spawner ?? new UnityObjectSpawner(),
                networkEventManager);

            return galaxy;
        }

        private static void DisableBotSceneComponents(Scene botScene)
        {
            var rootObjects = botScene.GetRootGameObjects();
            foreach (var rootObj in rootObjects)
            {
                // 禁用相机
                var cameras = rootObj.GetComponentsInChildren<Camera>(true);
                foreach (var camera in cameras)
                {
                    camera.enabled = false;
                }

                // 禁用音频监听器
                var listeners = rootObj.GetComponentsInChildren<AudioListener>(true);
                foreach (var listener in listeners)
                {
                    listener.enabled = false;
                }

                // 禁用输入系统
                var eventSystems = rootObj.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true);
                foreach (var eventSystem in eventSystems)
                {
                    eventSystem.enabled = false;
                }

                // 禁用UI输入模块
                var inputModules = rootObj.GetComponentsInChildren<UnityEngine.EventSystems.BaseInputModule>(true);
                foreach (var inputModule in inputModules)
                {
                    inputModule.enabled = false;
                }

                // 禁用画布
                var canvases = rootObj.GetComponentsInChildren<Canvas>(true);
                foreach (var canvas in canvases)
                {
                    if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                    {
                        canvas.enabled = false;
                    }
                }

                // 禁用粒子系统
                var particleSystems = rootObj.GetComponentsInChildren<ParticleSystem>(true);
                foreach (var ps in particleSystems)
                {
                    ps.Stop();
                    ps.gameObject.SetActive(false);
                }
            }
        }

        public static void Init(LaunchConfig launchConfig)
        {
            if (SgNetwork.Instance != null) return;
            GameObject sgNet = new GameObject("StargateNetwork");
            sgNet.AddComponent<SgNetwork>();
            DontDestroyOnLoad(sgNet);
            SgNetwork.Instance._started = true;
        }

        /// <summary>
        /// Run SgNetwork Logic
        /// </summary>
        private void Update()
        {
            if (this._started)
            {
                foreach (var galaxy in galaxies)
                {
                    galaxy.NetworkUpdate();
                }
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