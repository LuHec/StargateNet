using System;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    ///  Network Engine, client and server run in same way
    /// </summary>
    public class SgNetworkGalaxy
    {
        public SgNetEngine Engine { private set; get; }

        public SgNetworkGalaxy()
        {
        }

        public void Init(StartMode startMode, SgNetConfigData configData, ushort port)
        {
            this.Engine = new SgNetEngine();
            this.Engine.Start(startMode, configData, port);
        }

        public void Connect(string ip, ushort port)
        {
            if (this.Engine.IsServer)
                throw new Exception("Can't call Connect by server!");

            this.Engine.Connect(ip, port);
        }

        public void NetworkUpdate()
        {
            this.Engine.NetworkUpdate(Time.deltaTime, Time.timeScale);
        }
    }
}