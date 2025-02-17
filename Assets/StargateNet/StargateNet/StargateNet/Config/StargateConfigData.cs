using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public struct StargateConfigData
    {
        public int tickRate;
        public bool isPhysic2D;
        public ushort maxClientCount;
        public int maxNetworkObjects;
        public bool runAsHeadless;
        public int savedSnapshotsCount;
        public int maxPredictedTicks;
        public int maxSnapshotSendSize;  // 单帧能发送的最大Snapshot大小
        
        // Engine Data
        public List<GameObject> networkPrefabs;
        public long maxObjectStateBytes; // 单个NetworkObject的内存大小
        public List<string> networkInputsTypes;
        public List<int> networkInputsBytes;
    }
}