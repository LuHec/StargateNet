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
        internal WorldState WorldState { get; private set; }
        internal SimulationClock SimulationClock { get; private set; }
        internal Monitor Monitor { get; private set; }

        internal StargateAllocator WorldAllocator { get; private set; }

        // internal StargateAllocator ObjectAllocator { get; private set; }
        internal Tick simTick = new Tick(10); // 是客户端/服务端已经模拟的本地帧数。客户端的simTick与同步无关，服务端的simtick会作为AuthorTick传给客户端
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
        internal EntityMetaManager EntityMetaManager { get; private set; }
        internal InterestManager IM { get; private set; }
        internal Simulation Simulation { get; private set; }
        internal ServerSimulation ServerSimulation { get; private set; }
        internal ClientSimulation ClientSimulation { get; private set; }
        internal bool Simulated { get; private set; }
        internal bool IsConnected { get; set; }
        internal IObjectSpawner ObjectSpawner { get; private set; }
        internal Dictionary<int, NetworkObject> PrefabsTable { private set; get; }
        internal int maxEntities;
        private int _networkIdCounter = -1;


        internal StargateEngine()
        {
        }

        internal unsafe void Start(StartMode startMode, StargateConfigData configData, ushort port, Monitor monitor,
            IMemoryAllocator allocator, IObjectSpawner objectSpawner)
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            MemoryAllocation.Allocator = allocator;
            this.Monitor = monitor;
            this.ConfigData = configData;
            this.SimulationClock = new SimulationClock(this, this.FixedUpdate);
            // 给每一个Snapshot的分配器，上限是max snapshots
            // 初始化预制体的id
            this.PrefabsTable = new Dictionary<int, NetworkObject>(configData.networkPrefabs.Count);
            for (int i = 0; i < configData.networkPrefabs.Count; i++)
            {
                PrefabsTable.Add(i, configData.networkPrefabs[i].GetComponent<NetworkObject>());
            }

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
            long worldAllocatedBytes = (totalObjectMetaByteSize + totalObjectMapByteSize * 2) *
                                       (this.ConfigData.savedSnapshotsCount + 1) * 2; // 多乘个2是为了给control和header留空间，下同
            this.WorldAllocator = new StargateAllocator(worldAllocatedBytes, monitor);
            //用于物体Sync var的内存大小
            long totalObjectStateByteSize =
                configData.maxNetworkObjects * configData.maxObjectStateBytes * 2 * 2; // 2是因为还有dirtymap的占用
            this.WorldState = new WorldState(configData.savedSnapshotsCount, new Snapshot(
                (int*)this.WorldAllocator.Malloc(totalObjectMetaByteSize),
                (int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                new StargateAllocator(totalObjectStateByteSize, monitor), this.maxEntities)); //专门用于物体Sync var的分配器
            for (int i = 0; i < this.WorldState.MaxSnapshotsCount; i++)
            {
                this.WorldState.snapshots.Add(new Snapshot(
                    (int*)this.WorldAllocator.Malloc(totalObjectMetaByteSize),
                    (int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                    new StargateAllocator(totalObjectStateByteSize, monitor), this.maxEntities)
                );
            }

            this.ObjectSpawner = objectSpawner;

            if (startMode == StartMode.Server)
            {
                this.Server = new SgServerPeer(this, configData);
                this.Peer = this.Server;
                this.ServerSimulation = new ServerSimulation(this);
                this.Simulation = this.ServerSimulation;
                this.Server.StartServer(port, configData.maxClientCount);
                // 客户端的worldState需要在RecvBuffer时更新
                this.WorldState.Init(this.simTick);
            }
            else
            {
                this.Client = new SgClientPeer(this, configData);
                this.Peer = this.Client;
                this.ClientSimulation = new ClientSimulation(this);
                this.Simulation = this.ClientSimulation;
                this.WorldState.Init(Tick.InvalidTick);
            }

            this.Simulated = true;
            this.IsRunning = true;
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
            this.WorldState.CurrentSnapshot.networkStates.HandledRelease();
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
            this.Simulation.ExecuteNetworkUpdate();
            this.SimulationClock.Update();
        }

        /// <summary>
        /// Called every frame, after Step
        /// </summary>
        internal void Render()
        {
            if (!IsRunning) return;
            // Server can also have rendering
            if (this.IsServer && !this.ConfigData.runAsHeadless || this.IsConnected)
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
                this.Simulation.PreFixedUpdate(); // 对于客户端，先在这里处理回滚，然后再模拟下一帧
                this.Simulation.FixedUpdate();
                if (this.SimulationClock.IsLastCall) // 此时toSnapshot已经是完整的信息了，清理前先发送
                    this.Send();
                this.Simulation.DrainPaddingAddedEntity(); // 发送后再添加到模拟中
                this.Simulation.DrainPaddingRemovedEntity(); // 发送后再清除Entity占用的内存和id
                if (this.IsServer) // 更新FromTick
                {
                    this.WorldState.Update(this.simTick);
                }

                this.simTick++; // 下一次Tick是11
                this.Simulation.PostFixedUpdate();
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

        // ------------- Server Only ------------- //
        internal NetworkObject NetworkSpawn(GameObject gameObject, Vector3 position, Quaternion rotation)
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
                    this.EntityMetaManager.RequestWorldIdx(), new NetworkObjectMeta());
                return networkObject;
            }
            else throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }

        internal void NetworkDestroy(GameObject gameObject)
        {
            if (gameObject.TryGetComponent(out NetworkObject networkObject))
            {
                NetworkObjectRef networkObjectRef = networkObject.NetworkId;
                this.Simulation.RemoveEntity(networkObjectRef);
                this.ObjectSpawner.Despawn(gameObject);
            }
            else
                throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }

        // ------------- Client Only ------------- //
        internal void ClientSpawn(int networkId, int worldIdx, int prefabId, Vector3 position, Quaternion rotation)
        {
            if (prefabId == -1 || !this.PrefabsTable.TryGetValue(prefabId, out var value))
                throw new Exception($"Prefab Id:{prefabId} is not exist");
            NetworkObject networkObject = this.ObjectSpawner.Spawn(value, position, rotation);
            this.Simulation.AddEntity(networkObject, networkId, worldIdx, new NetworkObjectMeta());
            this.Simulation.DrainPaddingAddedEntity();
        }

        internal void ClientDestroy(int networkId)
        {
            if (networkId == -1) return;
            NetworkObjectRef networkObjectRef = new NetworkObjectRef(networkId);
            if (this.Simulation.entitiesTable.TryGetValue(networkObjectRef, out Entity entity))
            {
                this.ObjectSpawner.Despawn(entity.entityObject.gameObject);
                this.Simulation.RemoveEntity(networkObjectRef);
                this.Simulation.DrainPaddingRemovedEntity();
            }
            else throw new Exception($"Network Id:{networkId} is not exist");
        }
    }
}