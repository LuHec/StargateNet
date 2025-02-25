using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public class GameStarter : MonoBehaviour
    {
        [Header("Server")][SerializeField] private ushort serverPort;
        [SerializeField] private ushort maxClientCount;

        [Header("Client")][SerializeField] private string toServerIp;
        public bool IsBotScene = false;

        private bool _showConnectBtn = true;
        private int mode = 0;
        public GameObject test;

        private Queue<GameObject> refs = new();

        public void StartBotClient()
        {
            var galaxy = SgNetwork.StartAsClient(serverPort);

            // 使用本地回环地址
            galaxy.Connect("127.0.0.1", serverPort);
        }

        private void OnGUI()
        {
            if (IsBotScene) return;

            if (_showConnectBtn && GUI.Button(new Rect(10, 10, 100, 90), "Server"))
            {
                _showConnectBtn = false;
                var galaxy = SgNetwork.StartAsServer(serverPort, maxClientCount);
                mode = 2;
            }

            if (_showConnectBtn && GUI.Button(new Rect(10, 120, 100, 90), "Client"))
            {
                _showConnectBtn = false;
                var galaxy = SgNetwork.StartAsClient(serverPort);
                galaxy.Connect(toServerIp, serverPort);
                mode = 1;
            }

            if (_showConnectBtn && GUI.Button(new Rect(10, 230, 100, 90), "Server + Bot"))
            {
                _showConnectBtn = false;
                var galaxy = SgNetwork.StartAsClient(serverPort);
                galaxy.Connect(toServerIp, serverPort);
                mode = 1;
            }

            if (!_showConnectBtn && SgNetwork.Instance.monitor != null)
            {
                var monitor = SgNetwork.Instance.monitor;
                GUILayout.BeginVertical(); // 开始竖排布局

                string textToDisplay = $"Tick: {monitor.tick}\n" +
                                       $"Sim DeltaTime: {monitor.deltaTime:F6}\n" +
                                       $"Clock Level{monitor.clockLevel}\n" +
                                       $"RTT: {monitor.rtt:F6}ms\n" +
                                       $"Smooth RTT: {monitor.smothRTT:F6}ms\n" +
                                       $"InKBps: {SgNetwork.Instance.sgNetworkGalaxy.InKBps}KB/s\n" +
                                       $"OutKBps: {SgNetwork.Instance.sgNetworkGalaxy.OutKBps}KB/s\n" +
                                       $"InterpolationDelay: {SgNetwork.Instance.sgNetworkGalaxy.InterpolateDelay * 1000f}ms\n" +
                                       $"Client Resim: {monitor.resims}\n" +
                                       $"Client Input Count: {monitor.inputCount}\n" +
                                       $"Connected Clients: {monitor.connectedClients}\n" +
                                       $"Entities: {monitor.entities}\n" +
                                       $"Unmanged Memory: {monitor.unmanagedMemeory}byte\n" +
                                       $"Using Unmanaged Memory: {monitor.unmanagedMemeoryInuse}byte";
                string[] textLines = textToDisplay.Split('\n'); // 以换行符分割文本
                foreach (string line in textLines)
                {
                    GUILayout.Label(line); // 逐行显示
                }

                GUILayout.EndVertical(); // 结束竖排布局
            }

            if (!_showConnectBtn && mode == 2 && GUI.Button(new Rect(80, 200, 100, 90), "Spawn"))
            {
                refs.Enqueue(SgNetwork.Instance.sgNetworkGalaxy.NetworkSpawn(test, Vector3.zero, Quaternion.identity)
                    .gameObject);
            }

            if (!_showConnectBtn && mode == 2 && GUI.Button(new Rect(80, 300, 100, 90), "Destroy"))
            {
                if (refs.Count > 0)
                    SgNetwork.Instance.sgNetworkGalaxy.NetworkDestroy(refs.Dequeue());
            }
        }
    }
}