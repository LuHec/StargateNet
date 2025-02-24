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
        public float InterpolateDelay => this.Engine.InterpolateDelay;
        public float FixedDeltaTime => this.Engine.SimulationClock.FixedDeltaTime;
        public double ClockTime => this.Engine.SimulationClock.Time;
        public float InKBps => this.Engine.Peer.InKBps;
        public float OutKBps => this.Engine.Peer.OutKBps;
        public bool IsServer => this.Engine.IsServer;
        public bool IsClient => this.Engine.IsClient;
        public int PlayerId => this.Engine.IsServer ? -1 : this.Engine.Client.Client.Id;
        public Tick tick => this.Engine.Tick;
        public bool IsResimulation => this.Engine.IsResimulation;

        public SgNetworkGalaxy()
        {
        }

        public void Init(StartMode startMode, StargateConfigData configData, ushort port, Monitor monitor,ILagCompensateComponent lagCompensateComponent,
            IMemoryAllocator allocator, IObjectSpawner spawner, NetworkEventManager networkEventManager)
        {
            this.Engine = new StargateEngine();
            this.Engine.Start(this, startMode, configData, port, monitor, lagCompensateComponent, allocator, spawner, networkEventManager);
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
        }

        /// <summary>
        /// 网络物体生成，只有服务端可以调用。
        /// 对于使用诸如Yooasset的包体管理，也可以使用，只需要提供其加载出的GameObject即可(待求证，不清楚打进包里的id序列化是否还存在)
        /// 对于dll热更新，只需要把Config也打包进去即可，会通过Config动态加载NetworkObject列表
        /// </summary>
        /// <param name="gameObject"></param>
        /// <param name="position"></param>
        /// <param name="rotation"></param>
        /// <param name="inputSource"></param>
        public NetworkObject NetworkSpawn(GameObject gameObject, Vector3 position, Quaternion rotation, int inputSource = -1)
        {
            if (this.Engine.IsClient) throw new Exception("Only Server can spawn network objects");
            return this.Engine.NetworkSpawn(gameObject, position, rotation, inputSource);
        }

        public void NetworkDestroy(GameObject gameObject)
        {
            if (this.Engine.IsClient) throw new Exception("Only Server can spawn network objects");
            this.Engine.NetworkDestroy(gameObject);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="needRefresh">是否需要刷新延迟补偿的，默认不刷新，保留上次结果。用法一般是开火时刷新</param>
        /// <typeparam name="T"></typeparam>
        public void SetInput<T>(T input, bool needRefresh = false) where T : unmanaged, INetworkInput
        {
            this.Engine.SetInput(input, needRefresh);
        }

        /// <summary>
        /// 客户端行为，服务端不能使用这个
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public T GetInput<T>() where T : unmanaged, INetworkInput
        {
            return this.Engine.GetInput<T>();
        }

        /// <summary>
        /// 网络版本的射线检测，如果打到了带有延迟补偿的碰撞体，就会将物体回滚并重新判定
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="direction"></param>
        /// <param name="inputSource"></param>
        /// <param name="hitInfo"></param>
        /// <param name="maxDistance"></param>
        /// <param name="layerMask"></param>
        /// <returns></returns>
        public bool NetworkRaycast(Vector3 origin,
            Vector3 direction,
            int inputSource,
            out RaycastHit hitInfo,
            float maxDistance,
            int layerMask)
        {
            return this.Engine.NetworkRaycast(origin, direction, inputSource, out hitInfo, maxDistance, layerMask);
        }
    }
}