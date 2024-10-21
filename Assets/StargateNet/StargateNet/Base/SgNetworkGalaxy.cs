using System;
using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    ///  Network Engine, client and server run in same way
    /// </summary>
    public class SgNetworkGalaxy
    {
        public SgNetworkEngine Engine { private set; get; }
        public StargateConfigData ConfigData { private set; get; }
        public Dictionary<int, NetworkObject> NetworkObjectsTable { private set; get; }

        public SgNetworkGalaxy()
        {
        }

        public void Init(StartMode startMode, StargateConfigData configData, ushort port)
        {
            this.Engine = new SgNetworkEngine();
            this.Engine.Start(startMode, configData, port);
            NetworkObjectsTable = new Dictionary<int, NetworkObject>();
            for (int i = 0; i < configData.networkPrefabs.Count; i++)
            {
                NetworkObjectsTable.Add(i, configData.networkPrefabs[i]);
            }
        }

        public void Connect(string ip, ushort port)
        {
            if (this.Engine.IsServer)
                throw new Exception("Can't call Connect by server!");

            this.Engine.Connect(ip, port);
        }

        public void NetworkUpdate()
        {
            this.Engine.Update(Time.deltaTime, Time.timeScale);
            if (this.ConfigData.runAsHeadless)
                return;
            this.Engine.Render();
        }

        public void NetworkSpawn(GameObject gameObject)
        {
            if (Engine.IsClient) throw new Exception("Only Server can spawn network objects");
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
                // 拿到id，判断是服务端还是客户端(但是我认为状态帧同步框架应该让所有涉及同步的部分都由服务端来决定，所以这里应该只由服务端来调用，但是为了扩展，保留一下意见)
                // 生成物体，构造Entity，加入IM
                // 单纯发包/夹在DS中发给客户端
            }
            else throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }
    }
}