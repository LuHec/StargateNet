using System;
using System.Collections.Generic;
using System.Text;
using Riptide;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public sealed class StargateEngine
    {
        public SgNetworkGalaxy SgNetworkGalaxy { get; private set; }
        internal WorldState WorldState { get; private set; }
        internal SimulationClock SimulationClock { get; private set; }
        internal Monitor Monitor { get; private set; }

        internal StargateAllocator WorldAllocator { get; private set; }
        internal Tick Tick => this.IsServer ? this.SimTick : this.ClientSimulation.currentTick;
        internal Tick SimTick { get; private set; } // 是客户端/服务端已经模拟的本地帧数。客户端的simTick仅代表EnginTick，服务端的SimTick就是AuthorTick
        internal float LastDeltaTime { get; private set; }
        internal float LastTimeScale { get; private set; }
        internal StargateConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }
        internal bool IsShutDown { get; private set; }
        internal SgPeer Peer { get; private set; }
        internal SgClientPeer Client { get; private set; }
        internal SgServerPeer Server { get; private set; }
        internal bool IsServer => Peer.IsServer;
        internal bool IsClient => Peer.IsClient;
        internal LagCompensate LagCompensate { get; private set; }
        internal StargatePhysic PhysicSimulationUpdate { get; private set; }
        internal EntityMetaManager EntityMetaManager { get; private set; }
        internal InterestManager IM { get; private set; }
        internal Simulation Simulation { get; private set; }
        internal ServerSimulation ServerSimulation { get; private set; }
        internal ClientSimulation ClientSimulation { get; private set; }
        internal InterpolationLocal InterpolationLocal { get; private set; }
        internal NetworkEventManager NetworkEventManager { get; private set; }
        internal bool Simulated { get; private set; }
        internal bool IsConnected { get; set; }
        internal IObjectSpawner ObjectSpawner { get; private set; }
        internal Dictionary<int, NetworkObject> PrefabsTable { private set; get; }
        internal int maxEntities;
        private int _networkIdCounter = -1;


        internal StargateEngine()
        {
        }

        internal unsafe void Start(SgNetworkGalaxy galaxy, StartMode startMode, StargateConfigData configData,
            ushort port, Monitor monitor,
            IMemoryAllocator allocator, IObjectSpawner objectSpawner, NetworkEventManager networkEventManager)
        {
            if (configData.isPhysic2D)
                Physics2D.simulationMode = SimulationMode2D.Script;
            else
                Physics.simulationMode = SimulationMode.Script;
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            MemoryAllocation.Allocator = allocator;
            this.SgNetworkGalaxy = galaxy;
            this.Monitor = monitor;
            this.ConfigData = configData;
            this.SimTick = new Tick(10);
            this.SimulationClock = new SimulationClock(this, this.FixedUpdate);
            this.NetworkEventManager = networkEventManager;
            this.ObjectSpawner = objectSpawner;
            // 给每一个Snapshot的分配器，上限是max snapshots
            // 初始化预制体的id
            this.PrefabsTable = new Dictionary<int, NetworkObject>(configData.networkPrefabs.Count);
            for (int i = 0; i < configData.networkPrefabs.Count; i++)
            {
                PrefabsTable.Add(i, configData.networkPrefabs[i].GetComponent<NetworkObject>());
            }

            this.PhysicSimulationUpdate = new StargatePhysic(this, configData.isPhysic2D);
            this.IM = new InterestManager(configData.maxNetworkObjects, this);
            // ------------------------ 申请所有需要用到的内存 ------------------------ //
            this.maxEntities = (configData.maxNetworkObjects & 1) == 1
                ? configData.maxNetworkObjects + 1
                : configData.maxNetworkObjects;
            this.maxEntities = StargateNetUtil.AlignTo(this.maxEntities, 32); // 对齐一个int,申请足够大小的内存给id map
            this.EntityMetaManager = new EntityMetaManager(this.maxEntities, this);
            int totalObjectMetaByteSize = configData.maxNetworkObjects * sizeof(NetworkObjectMeta);
            int totalObjectMapByteSize = this.maxEntities * 4;
            //全局的分配器, 存map和meta
            long worldAllocatedBytes =
                (totalObjectMetaByteSize + totalObjectMapByteSize * 2) * (this.ConfigData.savedSnapshotsCount + 3) *
                2; // 多乘个2是为了给control和header留空间，下同.snapshot数量：savedSnapshotsCount + buffer + from + to
            this.WorldAllocator = new StargateAllocator(worldAllocatedBytes, monitor);
            //用于物体Sync var的内存大小
            long totalObjectStateByteSize =
                configData.maxNetworkObjects * configData.maxObjectStateBytes * 2 * 2; // 2是因为还有dirtymap的占用
            this.WorldState = new WorldState(this, configData.savedSnapshotsCount, new Snapshot(
                (int*)this.WorldAllocator.Malloc(totalObjectMetaByteSize),
                (int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                new StargateAllocator(totalObjectStateByteSize, monitor), this.maxEntities)); //专门用于物体Sync var的分配器
            this.WorldState.Init(this.SimTick);
            for (int i = 0; i < this.WorldState.MaxSnapshotsCount; i++)
            {
                this.WorldState.snapshots.Add(new Snapshot(
                    (int*)this.WorldAllocator.Malloc(totalObjectMetaByteSize),
                    (int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                    new StargateAllocator(totalObjectStateByteSize, monitor), this.maxEntities)
                );
            }

            this.InterpolationLocal = new InterpolationLocal(this);
            if (startMode == StartMode.Server)
            {
                this.Server = new SgServerPeer(this, configData);
                this.Peer = this.Server;
                this.ServerSimulation = new ServerSimulation(this);
                this.Simulation = this.ServerSimulation;
                this.Server.StartServer(port, configData.maxClientCount);
            }
            else
            {
                this.Client = new SgClientPeer(this, configData);
                this.Peer = this.Client;
                this.ClientSimulation = new ClientSimulation(this);
                this.Simulation = this.ClientSimulation;
                this.ClientSimulation.rcvBuffer = new Snapshot(
                    (int*)this.WorldAllocator.Malloc(totalObjectMetaByteSize),
                    (int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                    new StargateAllocator(totalObjectStateByteSize, monitor), this.maxEntities);
                // 客户端的worldState需要在RecvBuffer时更新
            }

            // 给插值组件用
            this.Simulation.fromSnapshot = new Snapshot(
                (int*)this.WorldAllocator.Malloc(totalObjectMetaByteSize),
                (int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                new StargateAllocator(totalObjectStateByteSize, monitor), this.maxEntities);
            this.Simulation.toSnapshot = this.WorldState.CurrentSnapshot;

            this.Simulated = true;
            this.IsRunning = true;

            this.NetworkEventManager.OnNetworkEngineStart(this.SgNetworkGalaxy);
        }


        // ------------- Engine basic ------------- //
        internal void ServerStart(ushort port, ushort maxClient)
        {
            if (!this.IsRunning) return;
            this.Server.StartServer(port, maxClient);
        }

        internal void Connect(string ip, ushort port)
        {
            if (!this.IsRunning) return;
            this.Client.Connect(ip, port);
        }

        internal void ShutDown()
        {
            if (this.IsShutDown) return;

            this.IsShutDown = true;
            Peer.Disconnect();
            this.IsConnected = false;
            this.IsRunning = false;

            // clear resources
            this.WorldState.HandledRelease();
            this.WorldState.CurrentSnapshot.NetworkStates.HandledRelease();
            this.WorldAllocator.HandledRelease();
            this.Simulation.HandledRelease();
        }

        /// <summary>
        /// Called every frame.
        /// </summary>
        internal void Update(float deltaTime, float timeScale)
        {
            if (!this.IsRunning) return;
            this.LastDeltaTime = deltaTime;
            this.LastTimeScale = timeScale;
            this.SimulationClock.PreUpdate();
            this.Peer.NetworkUpdate();
            this.Simulation.PreUpdate();
            this.NetworkEventManager.OnReadInput(this.SgNetworkGalaxy);
            this.Simulation.ExecuteNetworkUpdate();
            this.SimulationClock.Update();
        }

        /// <summary>
        /// Called every frame, after Step
        /// </summary>
        internal void Render()
        {
            if (!IsRunning) return;
            this.InterpolationLocal.Update();
            if ((this.IsServer && !this.ConfigData.runAsHeadless) || (this.IsClient && this.IsConnected))
            {
                this.Simulation.ExecuteNetworkRender();
            }
        }

        /// <summary>
        /// Called every fixed time, after Update
        /// </summary>
        private void FixedUpdate()
        {
            if (!this.IsRunning)
                return;

            if (this.IsServer || (this.IsClient && this.IsConnected))
            {
                if (this.IsServer) // 更新FromTick
                {
                    this.WorldState.ServerUpdateState(this.SimTick);
                }

                if (this.IsClient)
                    this.Simulation.DeserializeToGamecode();
                this.Simulation.PreFixedUpdate(); // 对于客户端，先在这里处理回滚，然后再模拟下一帧
                this.Simulation.FixedUpdate();
                if (this.IsServer)
                    this.Simulation.SerializeToNetcode();
                this.SimTick++; // 当前是10帧模拟完，11帧的初始状态发往客户端的帧数应该是11帧
                if (this.SimulationClock.IsLastCall) // 此时toSnapshot已经是完整的信息了，清理前先发送
                    this.Send();
                this.Simulation.DrainPaddingAddedEntity(); // 发送后再添加到模拟中
                this.Simulation.DrainPaddingRemovedEntity(); // 发送后再清除Entity占用的内存和id

                if (this.IsClient && this.ClientSimulation.currentTick.IsValid)
                {
                    this.Simulation.SerializeToNetcode();
                    this.ClientSimulation.currentTick++; // 客户端tick增加。FixedUpdate会被时钟在一帧调用多次，相应的currentTick也要更新
                }

                this.Simulation.PostFixedUpdate();
                if (this.IsClient)
                    Debug.Log($"input:{this.ClientSimulation.inputs.Count}");
            }
        }

        private void Send()
        {
            if (this.IsServer)
            {
                this.Server.SendServerPak();
            }
            else if (this.IsClient && this.IsConnected)
            {
                this.Client.SendClientPak();
            }
        }

        // ------------- Engine Func ------------- //

        internal bool FetchInput<T>(out T input, int inputSource) where T : INetworkInput
        {
            input = default(T);
            if (inputSource == -1) return false;
            if (this.IsServer)
            {
                return this.ServerSimulation.FetchInput<T>(out input, inputSource);
            }

            // 客户端保证只有本机的角色才能得到输入
            if (this.IsClient && this.IsConnected && inputSource == this.Client.Client.Id)
            {
                return this.ClientSimulation.FetchInput<T>(out input);
            }

            return false;
        }

        internal bool NetworkRaycast(Vector3 origin,
            Vector3 direction,
            int inputSource,
            out RaycastHit hitInfo,
            float maxDistance,
            int layerMask)
        {
            return this.LagCompensate.NetworkRaycast(origin, direction, inputSource,out hitInfo, maxDistance, layerMask);
        }

        // ------------- Server Only ------------- //
        /// <summary>
        /// 
        /// </summary>
        /// <param name="gameObject">预制体</param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="inputSource">输入源，和客户端id一致。服务端是0，客户端id从1开始</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        internal NetworkObject NetworkSpawn(GameObject gameObject, Vector3 position, Quaternion rotation,
            int inputSource = -1)
        {
            // 判断是服务端还是客户端(状态帧同步框架应该让所有涉及同步的部分都由服务端来决定，所以这里应该只由服务端来调用)
            // 生成物体，构造Entity，根据IM来决定要发给哪个客户端，同时加入pedding send集合中(每个client一个集合，这样可以根据IM的设置来决定是否要在指定客户端生成)
            // 在下一帧ServerPeer.Send中发出
            // 夹在DS中发给客户端,内存构造是：length,bitmap,data。
            if (gameObject.TryGetComponent(out NetworkObject component))
            {
                int id = component.PrefabId;
                if (!this.PrefabsTable.ContainsKey(id))
                    throw new Exception($"GameObject {gameObject.name} has not been registered");
                NetworkObject networkObject = this.ObjectSpawner.Spawn(component, position, rotation);
                // TODO: 把子节点如果是NetworkObject的也加入进去
                this.Simulation.AddEntity(networkObject, ++this._networkIdCounter,
                    this.EntityMetaManager.RequestWorldIdx(), inputSource);
                return networkObject;
            }
            else throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }

        internal void NetworkDestroy(GameObject gameObject)
        {
            if (gameObject.TryGetComponent(out NetworkObject networkObject))
            {
                NetworkObjectRef networkObjectRef = networkObject.NetworkId;
                this.OnEntityDestroy(networkObject);
                this.Simulation.RemoveEntity(networkObjectRef);
                this.ObjectSpawner.Despawn(gameObject);
            }
            else
                throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }

        // ------------- Client Only ------------- //
        internal void ClientSpawn(int networkId, int worldIdx, int prefabId, int inputSource, Vector3 position,
            Quaternion rotation)
        {
            if (prefabId == -1 || !this.PrefabsTable.TryGetValue(prefabId, out var value))
                throw new Exception($"Prefab Id:{prefabId} is not exist");
            NetworkObject networkObject = this.ObjectSpawner.Spawn(value, position, rotation);
            this.Simulation.AddEntity(networkObject, networkId, worldIdx, inputSource);
            this.Simulation.DrainPaddingAddedEntity();
        }

        internal void ClientDestroy(int networkId)
        {
            if (networkId == -1) return;
            NetworkObjectRef networkObjectRef = new NetworkObjectRef(networkId);
            if (this.Simulation.entitiesTable.TryGetValue(networkObjectRef, out Entity entity))
            {
                this.OnEntityDestroy(entity.entityObject);
                this.ObjectSpawner.Despawn(entity.entityObject.gameObject);
                this.Simulation.RemoveEntity(networkObjectRef);
                this.Simulation.DrainPaddingRemovedEntity();
            }
            else throw new Exception($"Network Id:{networkId} is not exist");
        }

        internal void SetInput<T>(T input) where T : INetworkInput
        {
            int inputSource = -1;
            if (this.IsClient && this.IsConnected)
            {
                inputSource = this.Client.Client.Id;
            }

            if (this.IsServer)
            {
                inputSource = 0;
            }

            if (inputSource != -1)
            {
                this.Simulation.SetInput(inputSource, input);
            }
        }

        internal T GetInput<T>() where T : INetworkInput
        {
            return this.Simulation.GetInput<T>(0);
        }

        private void OnEntityDestroy(NetworkObject networkObject)
        {
            var scripts = networkObject.NetworkScripts;
            foreach (var script in scripts)
            {
                script.NetworkDestroy(this.SgNetworkGalaxy);
            }
        }
    }
}