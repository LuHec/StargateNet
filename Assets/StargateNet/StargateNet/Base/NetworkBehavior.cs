using UnityEngine;

namespace StargateNet
{
    public abstract class NetworkBehavior : StargateBehavior
    {
        public virtual int StateBlockSize => 0; // 字节数，ILProcessor会算出大小
    }
}