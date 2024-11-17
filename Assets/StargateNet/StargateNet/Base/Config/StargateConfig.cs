using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace StargateNet
{
    [CreateAssetMenu(fileName = "StargateConfig", menuName = "StargateNet/StargateConfig")]
    public class StargateConfig : ScriptableObject
    {
        [Range(8, 120)]public int FPS = 30; // fps
        [Range(8, ushort.MaxValue)]public ushort MaxClientCount = 16;
        public bool RunAsHeadless = true;
        [Range(4, ushort.MaxValue)]public int maxNetworkObject = 10;
        [Range(1, 64)] public int SavedSnapshotsCount = 32;
        [Range(8, 300)]public int MaxPredictedTicks = 8;
        public List<GameObject> NetworkObjects;
        public long maxObjectStateBytes;
    }
}