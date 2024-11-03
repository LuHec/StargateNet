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
        internal Dictionary<int, NetworkObject> NetworkObjectsTable { private set; get; }
        internal Dictionary<int, NetworkBehavior> networkBehaviors = new();
        internal Queue<int> paddingRemoveBehaviors = new();
        internal Queue<KeyValuePair<int, GameObject>> paddingAddBehaviors = new(); // 待加入的
        internal Queue<NetworkObjectRef> networkRef2Reuse = new(); // 回收的id
        internal NetworkObjectRef currentMaxRef = NetworkObjectRef.InvalidNetworkObjectRef; // 当前最大Ref

        internal SgNetworkEngine()
        {
        }

        internal void Start(StartMode startMode, StargateConfigData configData, ushort port, Monitor monitor)
        {
            RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);
            this.ConfigData = configData;
            this.Timer = new SimulationClock(this, this.FixedUpdate);
            this.Monitor = monitor;
            // 初始化预制体的id
            this.NetworkObjectsTable = new Dictionary<int, NetworkObject>(configData.networkPrefabs.Count); 
            for (int i = 0; i < configData.networkPrefabs.Count; i++)
            {
                NetworkObjectsTable.Add(i, configData.networkPrefabs[i].GetComponent<NetworkObject>());
            }
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

            this.IM = new InterestManager();
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
                // Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToClient);
                // msg.AddInt(this.simTick.tickValue);
                // this.Server.SendMessageUnreliable(1, msg);
                this.Server.SendServerPak();
            }
            else if (this.IsConnected)
            {
                this.Client.SendClientPak();
            }
        }
        
        // ------------- Engine Func ------------- //

        // ------------- Server Only ------------- //
        internal void NetworkSpawn(GameObject gameObject)
        {
            // 判断是服务端还是客户端(状态帧同步框架应该让所有涉及同步的部分都由服务端来决定，所以这里应该只由服务端来调用)
            // 生成物体，构造Entity，根据IM来决定要发给哪个客户端，同时加入pedding send集合中(每个client一个集合，这样可以根据IM的设置来决定是否要在指定客户端生成)
            // 在下一帧ServerPeer.Send中发出
            // 夹在DS中发给客户端,内存构造是：length,bitmap,data。由于prefab id是int的，所以这个直接用4字节来处理id即可，没有ds的长度不定问题
            if (this.IsClient) throw new Exception("Only Server can spawn network objects");
            if (gameObject.TryGetComponent(out NetworkObject networkObject))
            {
                int id = networkObject.PrefabId;
                if(!this.NetworkObjectsTable.ContainsKey(id))
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
                
                
            }
            else throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }
    }
}