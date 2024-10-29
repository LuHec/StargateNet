using System;
using Riptide.Utils;
using UnityEngine;

namespace StargateNet
{
    public class GameStarter : MonoBehaviour
    {
        [Header("Server")] [SerializeField] private ushort serverPort;
        [SerializeField] private ushort maxClientCount;

        [Header("Client")] [SerializeField] private string toServerIp;

        private bool _showConnectBtn = true;

        private void OnGUI()
        {
            if (_showConnectBtn && GUI.Button(new Rect(10, 10, 100, 90), "Server"))
            {
                _showConnectBtn = false;
                var galaxy = SgNetwork.StartAsServer(serverPort, maxClientCount);
            }

            if (_showConnectBtn && GUI.Button(new Rect(10, 120, 100, 90), "Client"))
            {
                _showConnectBtn = false;
                var galaxy = SgNetwork.StartAsClient(serverPort);
                galaxy.Connect(toServerIp, serverPort);
            }

            if (!_showConnectBtn && SgNetwork.Instance.monitor != null)
            {
                var monitor = SgNetwork.Instance.monitor;
                GUILayout.BeginVertical(); // 开始竖排布局

                string textToDisplay = $"Sim DeltaTime: {monitor.deltaTime:F6}\n" +
                                       $"RTT: {monitor.rtt:F6}\n" +
                                       $"Smooth RTT: {monitor.smothRTT:F6}\n" +
                                       $"Client Resim: {monitor.resims}\n" +
                                       $"Client Input Count: {monitor.inputCount}\n" +
                                       $"Connected Clients: {monitor.connectedClients}";
                string[] textLines = textToDisplay.Split('\n'); // 以换行符分割文本
                foreach (string line in textLines)
                {
                    GUILayout.Label(line); // 逐行显示
                }

                GUILayout.EndVertical(); // 结束竖排布局
            }
        }
    }
}