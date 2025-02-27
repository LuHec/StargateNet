namespace StargateNet
{
    public class NetworkRPCAttribute
    {
        public NetworkRPCFrom From { get; set; }

        public NetworkRPCAttribute(NetworkRPCFrom from)
        {
            From = from;
        }
    }
}