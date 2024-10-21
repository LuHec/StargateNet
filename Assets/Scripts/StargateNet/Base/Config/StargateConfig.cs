using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace StargateNet
{
    [CreateAssetMenu(fileName = "StargateConfig", menuName = "StargateNet/StargateConfig")]
    public class StargateConfig : ScriptableObject
    {
        public List<GameObject> NetworkObjects;
    }
}