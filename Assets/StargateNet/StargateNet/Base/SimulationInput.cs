using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    /// 用于包裹NetworkInput的容器
    /// </summary>
    public class SimulationInput
    {
        public struct InputBlock
        {
            public int type;
            public INetworkInput input;
        }

        public Tick srvTick = Tick.InvalidTick;
        public Tick targetTick = Tick.InvalidTick;
        public List<InputBlock> inputBlocks = new(4);

        public void AddInputBlock(InputBlock inputBlock)
        {
            inputBlocks.Add(inputBlock);
        }
        
        public void AddInputBlock(int type, INetworkInput input)
        {
            this.inputBlocks.Add(new InputBlock { type = type, input = input });
        }
        
        public void Clear()
        {
            this.srvTick = Tick.InvalidTick;
            this.targetTick = Tick.InvalidTick;
            inputBlocks.Clear();
        }

        ~SimulationInput()
        {
            Debug.Log("dieeee");
        }
    }
}