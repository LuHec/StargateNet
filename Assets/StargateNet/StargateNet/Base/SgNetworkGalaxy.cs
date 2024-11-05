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
        public StargateEngine Engine { private set; get; }
        public StargateConfigData ConfigData { private set; get; }

        public SgNetworkGalaxy()
        {
        }

        public void Init(StartMode startMode, StargateConfigData configData, ushort port, Monitor monitor,
            IMemoryAllocator allocator, IObjectSpawner spawner)
        {
            this.Engine = new StargateEngine();
            this.Engine.Start(startMode, configData, port, monitor, allocator, spawner);
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
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        public void NetworkSpawn(GameObject gameObject, Vector3 position, Quaternion rotation)
        {
            if (this.Engine.IsClient) throw new Exception("Only Server can spawn network objects");
            this.Engine.NetworkSpawn(gameObject, position, rotation);
        }

        public void NetworkDestroy(GameObject gameObject)
        {
            if (this.Engine.IsClient) throw new Exception("Only Server can spawn network objects");
            this.Engine.NetworkDestroy(gameObject);
        }
    }
}