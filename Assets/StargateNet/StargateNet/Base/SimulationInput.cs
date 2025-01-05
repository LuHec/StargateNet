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

        public Tick clientAuthorTick = Tick.InvalidTick;
        public Tick clientTargetTick = Tick.InvalidTick;
        public float clientInterpolationAlpha = 0;
        public Tick clientRemoteFromTick = Tick.InvalidTick;
        public List<InputBlock> inputBlocks = new(4);

        public void Init(Tick authorTick, Tick targetTick, float alpha, Tick remoteFromTick)
        {
            this.clientAuthorTick = authorTick;
            this.clientTargetTick = targetTick;
            this.clientInterpolationAlpha = alpha;
            this.clientRemoteFromTick = remoteFromTick;
        }

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
            this.clientAuthorTick = Tick.InvalidTick;
            this.clientTargetTick = Tick.InvalidTick;
            this.clientInterpolationAlpha = 0;
            this.clientRemoteFromTick = Tick.InvalidTick;
            inputBlocks.Clear();
        }

        ~SimulationInput()
        {
            Debug.Log("dieeee");
        }
    }
}