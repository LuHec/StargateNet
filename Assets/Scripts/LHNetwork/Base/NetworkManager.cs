using System;
using LHNetwork.ClientCode;
using LHNetwork.ServerCode;
using Riptide;
using Riptide.Utils;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _networkManager;

    public static NetworkManager Singleton
    {
        get { return _networkManager; }
        private set
        {
            if (_networkManager == null)
            {
                _networkManager = value;
            }
            else if (_networkManager != value)
            {
                Debug.Log($"{nameof(NetworkManager)} instance has already exists, destroying duplicate");
                Destroy(value);
            }
        }
    }

    [Header("Server")] [SerializeField] private ushort serverPort;
    [SerializeField] private ushort maxClientCount;

    [Header("Client")] [SerializeField] private string toServerIp;

    private NetworkInstance _networkInstance;

    public bool IsServer => _networkInstance.IsServer;
    public bool IsClient => _networkInstance.IsClient;

    private bool isStart = false;

    private void Awake()
    {
        Singleton = this;
    }

    public void NetworkStart()
    {
        // 暂时先这么测试，编辑器服务端，打包后是客户端
#if UNITY_EDITOR
        _networkInstance = new ServerNetworkInstance(serverPort, maxClientCount);
#else
        _networkInstance = new ClientNetworkInstance(toServerIp,serverPort);
#endif
        RiptideLogger.Initialize(Debug.Log, Debug.Log, Debug.LogWarning, Debug.LogError, false);

        _networkInstance.NetworkStart();
        isStart = true;
    }

    public void Connect()
    {
        _networkInstance.Connect();
    }
    
    private void FixedUpdate()
    {
        if (isStart)
        {
            if (_networkInstance == null) throw new Exception("NetworkInstance has not been initialized!");
            _networkInstance.NetworkUpdate();
        }
    }

    private void OnApplicationQuit()
    {
        _networkInstance.OnQuit();
    }
    
    
    private NetworkManager()
    {
    }
}