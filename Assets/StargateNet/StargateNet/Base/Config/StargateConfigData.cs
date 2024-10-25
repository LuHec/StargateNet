using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    public struct StargateConfigData
    {
        public float tickRate; // fps
        public ushort maxClientCount;
        public bool runAsHeadless;
        public List<GameObject> networkPrefabs;
    }
}