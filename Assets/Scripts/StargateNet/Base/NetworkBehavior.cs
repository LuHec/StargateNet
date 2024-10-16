using UnityEngine;

namespace StargateNet
{
    public abstract class NetworkBehavior : StargateBehavior
    {
        public virtual int StateBlockSize => 0;
    }
}