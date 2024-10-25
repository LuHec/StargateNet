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
            NetworkObjectsTable = new Dictionary<int, NetworkObject>(configData.networkPrefabs.Count);
            for (int i = 0; i < configData.networkPrefabs.Count; i++)
            {
                NetworkObjectsTable.Add(i, configData.networkPrefabs[i].GetComponent<NetworkObject>());
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

        /// <summary>
        /// 网络物体生成，只有服务端可以调用。
        /// 对于使用诸如Yooasset的包体管理，也可以使用，只需要提供其加载出的GameObject即可(待求证，不清楚打进包里的id序列化是否还存在)
        /// 对于dll热更新，只需要把Config也打包进去即可，会通过Config动态加载NetworkObject列表
        /// </summary>
        /// <param name="gameObject"></param>
        /// <exception cref="Exception"></exception>
        public void NetworkSpawn(GameObject gameObject)
        {
            // 判断是服务端还是客户端(状态帧同步框架应该让所有涉及同步的部分都由服务端来决定，所以这里应该只由服务端来调用)
            // 生成物体，构造Entity，根据IM来决定要发给哪个客户端，同时加入pedding send集合中(每个client一个集合，这样可以根据IM的设置来决定是否要在指定客户端生成)
            // 夹在DS中发给客户端,内存构造是：length,bitmap,data。由于prefab id是int的，所以这个直接用4字节来处理id即可，没有ds的长度不定问题
            if (Engine.IsClient) throw new Exception("Only Server can spawn network objects");
            if (gameObject.TryGetComponent<NetworkObject>(out NetworkObject networkObject))
            {
            }
            else throw new Exception($"GameObject {gameObject.name} is not a NetworkObject");
        }
    }
}