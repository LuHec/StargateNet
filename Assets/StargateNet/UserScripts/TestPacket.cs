using System;
using System.Collections;
using System.Collections.Generic;
using Riptide;
using StargateNet;
using UnityEngine;

public class TestPacket : MonoBehaviour
{
    // 这是我对分包最终机制的结论：
    // 两种方案：
    // 第一种方案：对大包拆分，这样计算方便，但是丢了一个包客户端就要全部丢弃，导致客户端可能长时间无法更新
    // 第二种方案：本身就是小包，每个包写入部分Snapshot，这样客户端丢了一部分包，也能应用一部分的状态。
    // 但是这样KBps会变大(每个包头都需要写入Meta)，且在Meta数量增加时，也有仅写入Meta就触发MTU上限的风险
    // 处理丢包两个方式都一样，只告诉服务端丢了哪一帧，而不是哪个分包。服务端只会计算客户端丢帧后的差分，客户端直接用最新的包覆盖状态。
    // 在应用上：
    // 第一种方案丢了包(收到了新的帧)就直接清空缓冲区，并丢弃新的包，然后通知服务端丢包了。这样的话，假设60tick16.6ms,延迟有200ms，
    // 丢了一次包就得等100 / 16.6 = 6帧左右服务端才能收到客户端的消息，并做差分，这样需要写入6帧的包
    // 第二种方案，可以直接写入缓冲区，但是因为需要Meta信息，所以这种方案得每个分包都带有Meta，基本上重连后同步3，40个物体就要分一堆
    // 包了，所以还是弃用。
    // 所以最终决定使用第一种方案。
    // 此外，还决定加入一个优先级机制来决定同步哪些物体。因为我的框架Meta是只有改变了的才会发，所以完全适配这个机制。
    // 添加一个属性MaxSnapshotSendSize，决定一次发送的Snapshot大小最大是多少，小了就能减少丢包的概率，大了能传输的就更多。
    // 优先级机制会根据用户设定NetworkObject的优先级(也可以实时改变优先级)以及自定义评估因素(比如自定义距离)来进行排序，优先发送最前面的数据。
    // 并且发送的物体会进入冷却(可以设定是否无视冷却，但这样会出现饥饿的情况，此时可以通过提高MSSS大小来缓解)。
    // 一般来说，推荐让玩家实体根据距离来无视冷却，其他场景物体、实体子弹等进入冷却。
    // class NetworkObj : IComparable<NetworkObj>
    // {
    //     public int lastSendTick;
    //     public bool needCool = true;
    //
    //     public virtual bool CustomPriority(NetworkObject other) => true;
    //     
    //     public int CompareTo(NetworkObj other)
    //     {
    //         if (ReferenceEquals(this, other)) return 0;
    //         if (other is null) return 1;
    //         // 先比较Tick
    //         bool priority = false;
    //         if (!needCool) priority = true;
    //         else priority = lastSendTick <= other.lastSendTick;
    //         
    //     }
    // }

    void OnGetMessage()
    {
        
    }

    void Send()
    {
    }

    void ReadHeader()
    {
    }

    private void Start()
    {
        MemoryAllocation.Allocator = new UnityAllocator();
        ReadWriteBuffer writeBuffer = new ReadWriteBuffer(4096);
        ReadWriteBuffer readBuffer = new ReadWriteBuffer(4096);
        writeBuffer.AddInt(-10);
        writeBuffer.AddBool(false);
        writeBuffer.AddInt(20);
        writeBuffer.AddBool(false);
        writeBuffer.AddInt(20);
        writeBuffer.AddBool(true);
        long size = writeBuffer.GetUsedBytes();
        Debug.Log($"Write Buffer size: {writeBuffer.GetUsedBytes()}");
        
        Message msg = Message.Create(MessageSendMode.Unreliable, Protocol.ToClient);
        msg.AddLong(size);
        while (!writeBuffer.ReadEOF())
        {
            msg.AddByte(writeBuffer.GetByte());
            // readBuffer.AddByte(writeBuffer.GetByte());
        }
        Debug.Log($"Message size: {msg.BytesInUse}");
        //
        // //
        //
        long rcvSize = msg.GetLong();
        Debug.Log($"Read Buffer size: {rcvSize}");
        while (rcvSize-- > 0)
        {
            readBuffer.AddByte(msg.GetByte());
        }
        Debug.Log(readBuffer.GetInt());
        Debug.Log(readBuffer.GetBool());
        Debug.Log(readBuffer.GetInt());
        Debug.Log(readBuffer.GetBool());
        Debug.Log(readBuffer.GetInt());
        Debug.Log(readBuffer.GetBool());
    }
}