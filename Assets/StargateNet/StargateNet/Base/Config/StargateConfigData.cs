using System.Collections.Generic;

namespace StargateNet
{
    public struct StargateConfigData
    {
        public float tickRate; // fps
        public ushort maxClientCount;
        public bool runAsHeadless;
        public List<NetworkObject> networkPrefabs;
    }
}