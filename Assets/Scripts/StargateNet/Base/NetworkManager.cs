using System;
using Riptide.Utils;
using UnityEngine;

namespace StargateNet
{
    public class NetworkManager : MonoBehaviour
    {
        [Header("Server")] [SerializeField] private ushort serverPort;
        [SerializeField] private ushort maxClientCount;

        [Header("Client")] [SerializeField] private string toServerIp;

        private bool _showConnectBtn = true;
        
        private void OnGUI()
        {
            if (!_showConnectBtn && GUI.Button(new Rect(10, 10, 100, 90), "Server"))
            {
                _showConnectBtn = false;
                var galaxy = SgNetwork.StartAsServer(serverPort, maxClientCount);
            }
            
            if (!_showConnectBtn && GUI.Button(new Rect(10, 120, 100, 90), "Server"))
            {
                _showConnectBtn = false;
                var galaxy = SgNetwork.StartAsClient(toServerIp, serverPort);
                galaxy.Connect(toServerIp, serverPort);
            }
        }
    }
}