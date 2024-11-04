using System;
using System.Collections.Generic;
using System.Text;
using Riptide;
using Riptide.Utils;
using UnityEngine;
using LogType = Riptide.Utils.LogType;

namespace StargateNet
{
    public sealed class SgNetworkEngine
    {
        internal SimulationClock Timer { get; private set; }
        internal Monitor Monitor { get; private set; }
        internal StargateAllocator GlobalAllocator { get; private set; }
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
        internal Dictionary<int, NetworkBehavior> networkBehaviors = new();
        internal Queue<int> paddingRemoveBehaviors = new();
        internal Queue<KeyValuePair<int, GameObject>> paddingAddBehaviors = new(); // 待加入的
        internal Queue<NetworkObjectRef> networkRef2Reuse = new(); // 回收的id
        internal NetworkObjectRef currentMaxRef = NetworkObjectRef.InvalidNetworkObjectRef; // 当前最大Ref
        internal Dictionary<NetworkObjectRef, NetworkObject> NetworkObjectsTable { private set; get; }
        internal int maxNetworkRef;
        internal unsafe int* networkRefMap; 


        internal SgNetworkEngine()
        {
        }

        internal unsafe void Start(StartMode startMode, StargateConfigData configData, ushort port, Monitor monitor,
            IMemoryAllocator allocator, IObjectSpawner objectSpawner)
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            MemoryAllocation.Allocator = allocator;
            this.Monitor = monitor;
            this.ConfigData = configData;
            this.Timer = new SimulationClock(this, this.FixedUpdate);
            this.GlobalAllocator = new StargateAllocator(4096, monitor); //全局的分配器
            // 给每一个Snapshot的分配器，上限是max snapshots
            // 初始化预制体的id
            this.PrefabsTable = new Dictionary<int, NetworkObject>(configData.networkPrefabs.Count);
            for (int i = 0; i < configData.networkPrefabs.Count; i++)
            {
                PrefabsTable.Add(i, configData.networkPrefabs[i].GetComponent<NetworkObject>());
            }

            this.IM = new InterestManager();
            this.maxNetworkRef = (configData.maxNetworkObjects & 1) == 1
                ? configData.maxNetworkObjects + 1
                : configData.maxNetworkObjects;
            // 对齐一个int,申请足够大小的内存给id map
            this.maxNetworkRef = SgNetworkUtil.AlignTo(this.maxNetworkRef, 32);
            this.networkRefMap = (int*)this.GlobalAllocator.Malloc((ulong)this.maxNetworkRef * 4);
            this.NetworkObjectsTable = new(configData.maxNetworkObjects);
            this.ObjectSpawner = objectSpawner;

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
            this.Timer.PreUpdate();
            this.Peer.NetworkUpdate();
            this.Simulation.PreUpdate();
            this.Simulation.ExecuteNetworkUpdate();
            this.Timer.Update();
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
                this.Simulation.FixedUpdate();
                this.simTick++;
            }

            this.Send();
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
            if (this.IsClient) throw new Exception("Only Server can spawn network objects");
            if (gameObject.TryGetComponent(out NetworkObject networkObject))
            {
                int id = networkObject.PrefabId;
                if (!this.PrefabsTable.ContainsKey(id))
                    throw new Exception($"GameObject {gameObject.name} has not been registered");

                NetworkObjectRef networkObjectRef = NetworkObjectRef.InvalidNetworkObjectRef;
                if (this.networkRef2Reuse.Count > 0)
                {
                    networkObjectRef = this.networkRef2Reuse.Dequeue();
                }
                else
                {
                    networkObjectRef = this.currentMaxRef + 1;
                }

                NetworkObject spNetObject = this.ObjectSpawner.Spawn(gameObject, position, rotation)
                    .GetComponent<NetworkObject>();
                // 初始化Entity以及NetworkObject上的所有Behavior脚本
                Entity entity = new Entity(networkObjectRef, this, spNetObject);

                // 标记bitmap
                int raw = networkObjectRef.refValue / 32;
                int column = networkObjectRef.refValue % 32;
                this.networkRefMap[raw] |= (1 << column);
                this.NetworkObjectsTable.TryAdd(networkObjectRef, spNetObject);
            }
            else throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }

        internal unsafe void NetworkDestroy(GameObject gameObject)
        {
            if (this.IsClient) throw new Exception("Only Server can spawn network objects");
            if (gameObject.TryGetComponent(out NetworkObject networkObject))
            {
                // 设置bitmap,回收ref
                NetworkObjectRef networkObjectRef = networkObject.NetworkId;
                this.NetworkObjectsTable.Remove(networkObjectRef);
                int raw = networkObjectRef.refValue / 32;
                int column = networkObjectRef.refValue % 32;
                this.networkRefMap[raw] &= ~(1 << column);
                this.networkRef2Reuse.Enqueue(networkObjectRef);

                this.ObjectSpawner.Despawn(gameObject);
            }
            else
                throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }
        
        // ------------- Client Only ------------- //
        
        
    }
    
}