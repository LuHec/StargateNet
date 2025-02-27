using System;

namespace StargateNet
{
    public class NetworkRPCAttribute : Attribute
    {
        public NetworkRPCFrom From { get; set; }

        public NetworkRPCAttribute(NetworkRPCFrom from)
        {
            From = from;
        }
    }
}