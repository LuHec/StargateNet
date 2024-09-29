namespace StargateNet
{
    public class TestScript : INetworkEntityScript
    {
        public unsafe int* stateBlock;

        public unsafe int* State => stateBlock;

        uint a = 10;
        
        unsafe int Test()
        {
            // uint a = 10;
            a = (uint)10;
            return *(int*)(this.State + 2);
        }
    }
}