namespace StargateNet
{
    public class TestScript : INetworkEntityScript
    {
        public unsafe int* stateBlock;

        public unsafe int* State => stateBlock;
    }
}