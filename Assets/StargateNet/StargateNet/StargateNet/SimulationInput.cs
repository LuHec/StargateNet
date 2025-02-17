using System.Collections.Generic;
using UnityEngine;

namespace StargateNet
{
    /// <summary>
    /// 用于包裹NetworkInput的容器
    /// </summary>
    public class SimulationInput
    {
        public Tick clientAuthorTick = Tick.InvalidTick;
        public Tick clientTargetTick = Tick.InvalidTick;
        public float clientInterpolationAlpha = 0;
        public Tick clientRemoteFromTick = Tick.InvalidTick;
        internal List<InputBlock> inputBlocks = new List<InputBlock>();

        public SimulationInput()
        {
        }

        public void Init(Tick authorTick, Tick targetTick, float alpha, Tick remoteFromTick)
        {
            this.clientAuthorTick = authorTick;
            this.clientTargetTick = targetTick;
            this.clientInterpolationAlpha = alpha;
            this.clientRemoteFromTick = remoteFromTick;
        }
        
        internal void AddInputBlock(InputBlock newInputBlock)
        {
            this.inputBlocks.Add(newInputBlock);
        }
        
        internal void Clear()
        {
            this.clientAuthorTick = Tick.InvalidTick;
            this.clientTargetTick = Tick.InvalidTick;
            this.clientInterpolationAlpha = 0;
            this.clientRemoteFromTick = Tick.InvalidTick;
            this.inputBlocks.Clear();
        }
    }
}