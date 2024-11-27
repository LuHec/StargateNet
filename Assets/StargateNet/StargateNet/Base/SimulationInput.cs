using System.Collections.Generic;

namespace StargateNet
{
    /// <summary>
    /// 用于包裹NetworkInput的容器
    /// </summary>
    public class SimulationInput
    {
        public struct InputBlock
        {
            public int inputSource;
            public INetworkInput input;
        }

        public Tick srvTick = Tick.InvalidTick;
        public Tick targetTick = Tick.InvalidTick;
        public List<InputBlock> inputBlocks = new(4);

        public void Clear()
        {
            this.srvTick = Tick.InvalidTick;
            this.targetTick = Tick.InvalidTick;
            inputBlocks.Clear();
        }
    }
}