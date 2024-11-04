using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public struct StargateConfigData
    {
        public int tickRate;
        public ushort maxClientCount;
        public int maxNetworkObjects;
        public bool runAsHeadless;
        public int savedSnapshotsCount;
        public List<GameObject> networkPrefabs;
        public int maxPredictedTicks;
    }
}