using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace StargateNet
{
    [CreateAssetMenu(fileName = "StargateConfig", menuName = "StargateNet/StargateConfig")]
    public class StargateConfig : ScriptableObject
    {
        [Range(2, 120)]public int FPS = 30; // fps
        [Range(2, ushort.MaxValue)]public ushort MaxClientCount = 16;
        public bool IsPhysic2D = false;
        public bool RunAsHeadless = true;
        [Range(4, ushort.MaxValue)]public int maxNetworkObject = 10;
        [Range(1, 64)] public int SavedSnapshotsCount = 32;
        [Range(8, 300)]public int MaxPredictedTicks = 8;
        [Tooltip("单帧服务器能发送给客户端最大数据量")]public int maxSnapshotSendSize;
        [Tooltip("AOI区块大小")] public int AoIBound = 10;
        [Tooltip("AOI加载区块范围")] public int AoIRange = 2;
        [Tooltip("AOI宽松卸载区块范围")]public int AoIUnloadRange = 2;
        [Tooltip("AOI区块大小")]public int WorldSize = 500;
    
        // Engine Data
        public List<GameObject> NetworkObjects;
        public long maxObjectStateBytes;
        public List<string> networkInputsTypes;
        public List<int> networkInputsBytes;
    }
}