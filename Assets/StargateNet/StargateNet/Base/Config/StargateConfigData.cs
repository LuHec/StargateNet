using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public struct StargateConfigData
    {
        public int tickRate;
        public ushort maxClientCount;
        public bool runAsHeadless;
        public List<GameObject> networkPrefabs;
        public int maxPredictedTicks;
    }
}