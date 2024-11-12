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
        internal StargateAllocator ObjectAllocator { get; private set; }
        internal Tick simTick = new Tick(10); // 是客户端/服务端已经模拟的本地帧数。客户端的simTick与同步无关，服务端的simtick会作为AuthorTick传给客户端
        internal float LastDeltaTime { get; private set; }
        internal float LastTimeScale { get; private set; }
        internal StargateConfigData ConfigData { get; private set; }
        internal bool IsRunning { get; private set; }
        internal SgPeer Peer { get; private set; }
        internal SgClientPeer Client { get; private set; }
        internal SgServerPeer Server { get; private set; }
        internal bool IsServer => Peer.IsServer;
        internal bool IsClient => Peer.IsClient;
        internal InterestManager IM { get; private set; }
        internal Simulation Simulation { get; private set; }
        internal ServerSimulation ServerSimulation { get; private set; }
        internal ClientSimulation ClientSimulation { get; private set; }
        internal bool Simulated { get; private set; }
        internal bool IsConnected { get; set; }
        internal IObjectSpawner ObjectSpawner { get; private set; }
        internal Dictionary<int, NetworkObject> PrefabsTable { private set; get; }
        internal int maxNetworkRef;


        internal StargateEngine()
        {
        }

        internal unsafe void Start(StartMode startMode, StargateConfigData configData, ushort port, Monitor monitor,
            IMemoryAllocator allocator, IObjectSpawner objectSpawner)
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            this.WorldState = new WorldState(configData.savedSnapshotsCount);
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

            this.IM = new InterestManager(configData.maxNetworkObjects);
            // ------------------------ 申请所有需要用到的内存 ------------------------ //
            int totalObjectStateByteSize = configData.maxNetworkObjects * configData.objectStateSize;
            int totalObjectMetaByteSize = configData.maxNetworkObjects * sizeof(NetworkObjectMeta);
            this.maxNetworkRef = (configData.maxNetworkObjects & 1) == 1
                ? configData.maxNetworkObjects + 1
                : configData.maxNetworkObjects;
            this.maxNetworkRef = StargateNetUtil.AlignTo(this.maxNetworkRef, 32); // 对齐一个int,申请足够大小的内存给id map
            int totalObjectMapByteSize = this.maxNetworkRef * 4;
            this.WorldAllocator = new StargateAllocator(4096, monitor); //全局的分配器
            this.ObjectAllocator = new StargateAllocator(4096, monitor); //专门用于物体Sync var的分配器
            for (int i = 0; i < this.WorldState.MaxSnapshotsCount; i++)
            {
                this.WorldState.snapshots.Add(new Snapshot((int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                    (int*)this.WorldAllocator.Malloc(totalObjectMetaByteSize),
                    (int*)this.WorldAllocator.Malloc(totalObjectMapByteSize),
                    new StargateAllocator(totalObjectStateByteSize, monitor))
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
                this.Simulation.DrainPaddingAddedEntity();
                this.Simulation.PreFixedUpdate();
                this.Simulation.FixedUpdate();
                if(this.SimulationClock.IsLastCall) // 优先发送消息
                    this.Send();
                this.Simulation.PostFixedUpdate();
                this.Simulation.DrainPaddingRemovedEntity();
                this.simTick++;
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
        internal unsafe void NetworkSpawn(GameObject gameObject, Vector3 position, Quaternion rotation)
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
                NetworkObject networkObject = this.ObjectSpawner.Spawn(gameObject, position, rotation).GetComponent<NetworkObject>();
                // TODO: 把子节点如果是NetworkObject的也加入进去
                this.Simulation.AddEntity(networkObject, new NetworkObjectMeta());
            }
            else throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }

        internal unsafe void NetworkDestroy(GameObject gameObject)
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
        internal unsafe void ClinetSpawn()
        {
        }
    }
}