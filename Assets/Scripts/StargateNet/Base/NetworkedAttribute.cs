using System;

namespace StargateNet
{
    public class NetworkedAttribute : Attribute
    {
        public NetworkedAttribute()
        {
            
        }

        public Action onValueChanged;
    }
}