using System;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    ///  Network Engine, client and server run in same way
    /// </summary>
    public class SgNetworkGalaxy
    {
        public SgNetworkEngine Engine { private set; get; }
        public SgNetConfigData ConfigData { private set; get; }

        public SgNetworkGalaxy()
        {
        }

        public void Init(StartMode startMode, SgNetConfigData configData, ushort port)
        {
            this.Engine = new SgNetworkEngine();
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
            this.Engine.Update(Time.deltaTime, Time.timeScale);
            if (this.ConfigData.runAsHeadless)
                return;
            this.Engine.Render();
        }
    }
}