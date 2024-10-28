using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace StargateNet
{
    [CreateAssetMenu(fileName = "StargateConfig", menuName = "StargateNet/StargateConfig")]
    public class StargateConfig : ScriptableObject
    {
        public float FPS = 30; // fps
        
        
        [Range(8, ushort.MaxValue)]public ushort MaxClientCount = 16;
        public bool RunAsHeadless = true;
        [Range(8, 300)]public int MaxPredictedTicks = 8;
        public List<GameObject> NetworkObjects;
    }
}