using System;
using Riptide.Utils;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    /// 网络部分运行载体
    /// </summary>
    public sealed class SgNetwork : MonoBehaviour
    {
        public static SgNetwork Instance => _instance;
        private NetworkGalaxy _networkGalaxy;
        private static SgNetwork _instance;
        private bool _started;

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

        public static NetworkGalaxy StartAsServer(ushort port, ushort maxClientCount)
        {
            SgNetwork.Instance._networkGalaxy = new NetworkGalaxy();
            return SgNetwork.Launch(StartMode.Server);
        }

        public static NetworkGalaxy StartAsClient(string ip, ushort port)
        {
            SgNetwork.Instance._networkGalaxy = new NetworkGalaxy();
            return SgNetwork.Launch(StartMode.Client);
        }

        private void OnApplicationQuit()
        {
            
        }

        public static NetworkGalaxy Launch(StartMode startMode)
        {
            SgNetwork.Instance._networkGalaxy = new NetworkGalaxy();
            SgNetwork.Instance._networkGalaxy.Init(startMode, new SgNetConfigData(){tickRate = 33.3f});
            return SgNetwork.Instance._networkGalaxy;
        }
        

        private void Update()
        {
            if (this._networkGalaxy != null && this._started)
            {
                this._networkGalaxy.Engine.Update(Time.deltaTime, Time.timeScale);
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