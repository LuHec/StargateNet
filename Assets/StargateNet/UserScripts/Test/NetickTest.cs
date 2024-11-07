using System.Collections.Generic;
using StargateNet;


public class ClientConnection
{
    
}

public struct NetworkObjectMeta
{
    public bool destroyed;
    public NetworkObjectRef id;
}

public class Netobj
{
    public int prefabId;
}

public class InterestGroup
{
}

public class Snap
{
    public unsafe int* meta;
    public Allocator data;
}

public class Allocator
{
    public struct Pool
    {
        public unsafe int* data;
        public int byteSize;
    }

    // 存放obj数据
    public List<Pool> pools;
}

public class Sim
{
    // 共享
    public List<Snap> snapshots;
}

public class Engine
{
    public Sim sim;
}