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
        public List<GameObject> networkPrefabs;
        public int maxPredictedTicks;
        public long maxObjectStateBytes; // 单个NetworkObject的内存大小
        public int maxSnapshotSendSize;  // 单帧能发送的最大Snapshot大小
    }
}