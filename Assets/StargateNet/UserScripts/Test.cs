using System;
using System.Collections;
using System.Collections.Generic;
using Riptide;
using StargateNet;
using UnityEngine;

public class Test : MonoBehaviour
{
    public bool simluate;

    private ReadWriteBuffer readBuffer;
    private ReadWriteBuffer writeBuffer;
    private ReadWriteBuffer fragmentBuffer;
    private Server server;
    private Client client;

    // For testing communication
    private const int Port = 7777;

    private void Awake()
    {
        Debug.Log(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(30.1)));
        StartServer();
    }

    void Start()
    {
        MemoryAllocation.Allocator = new UnityAllocator();
        readBuffer = new ReadWriteBuffer(4096);
        writeBuffer = new ReadWriteBuffer(4096);
        fragmentBuffer = new ReadWriteBuffer(4096);
        // Start as a server if running locally (could add condition to run as either client or server)

        StartClient();
    }

    void StartServer()
    {
        // Initialize and start the server
        server = new Server();
        server.ClientConnected += OnConnect;
        server.Start(Port, 10);
        Debug.Log("Server started on port " + Port);
    }

    void StartClient()
    {
        // Initialize and connect the client
        client = new Client();
        client.MessageReceived += OnClientReceiveMessage;
        client.Connect($"127.0.0.1:{Port}"); // Connect to the server running on the same machine

        Debug.Log("Client connected to server.");
    }

    void Update()
    {
        server.Update();
        client.Update();
    }

    void OnApplicationQuit()
    {
        server?.Stop();
        client?.Disconnect();
    }

    void OnConnect(object sender, ServerConnectedEventArgs args)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, 1);
        writeBuffer.AddInt(-1);
        writeBuffer.AddInt(-1);
        writeBuffer.AddBool(true);
        writeBuffer.AddBool(false);
        writeBuffer.AddInt(102);
        writeBuffer.AddBool(true);
        writeBuffer.AddDouble(30.21);
        writeBuffer.AddInt(250);
        message.AddInt((int)writeBuffer.GetUsedBytes());
        message.AddInt(0);
        message.AddInt(10);
        int bytes = (int)writeBuffer.GetUsedBytes();
        int temp = 10;
        while (temp -- > 0)
        {
            message.AddByte(writeBuffer.GetByte());
        }
        args.Client.Send(message);
        Message msg = Message.Create(MessageSendMode.Unreliable, 1);
        msg.AddInt((int)writeBuffer.GetUsedBytes());
        msg.AddInt(10);
        msg.AddInt(bytes - 10);
         temp = bytes - 10;
         while (temp -- > 0)
         {
             msg.AddByte(writeBuffer.GetByte());
         }

         args.Client.Send(msg);
    }

    private int count = 0;
    unsafe void OnClientReceiveMessage(object sender, MessageReceivedEventArgs args)
    {
        Message msg = args.Message;
        fragmentBuffer.Reset();
        count++;
        int size = msg.GetInt();
        int fragStart = msg.GetInt();
        int fragSize = msg.GetInt();
        
        int temp = fragSize;
        Debug.LogWarning($"packet:{fragSize}");
        while (temp-- > 0)
        {
            fragmentBuffer.AddByte(msg.GetByte());
        }

        // readBuffer.SetSize(size, 0);
        fragmentBuffer.CopyTo(readBuffer, fragStart, fragSize);
        if (count != 2) return;
        readBuffer.ResetRead();
        Debug.LogWarning($"readBuffer:{readBuffer.ReadRemainBytes()}");
        Debug.LogWarning(readBuffer.GetInt());
        Debug.LogWarning(readBuffer.GetInt());
        Debug.LogWarning(readBuffer.GetBool());
        Debug.LogWarning(readBuffer.GetBool());
        Debug.LogWarning(readBuffer.GetInt());
        Debug.LogWarning(readBuffer.GetBool());
        Debug.LogWarning(readBuffer.GetDouble());
        Debug.LogWarning(readBuffer.GetInt());
        
    }
}