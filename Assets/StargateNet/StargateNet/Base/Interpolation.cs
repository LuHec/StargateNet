namespace StargateNet
{
    public abstract class Interpolation
    {
        internal StargateEngine Engine { private set; get; }
        internal Snapshot FromSnapshot { get; set; }
        internal Snapshot ToSnapshot { get; set; }
        internal abstract Tick FromTick { get; }

        internal abstract Tick ToTick { get; }

        internal abstract bool HasSnapshot { get; }

        internal abstract float Alpha { get; }
        /// <summary>
        /// 暂时没什么用
        /// </summary>
        internal abstract float InterpolationTime { get; }

        protected Interpolation(StargateEngine stargateEngine)
        {
            this.Engine = stargateEngine;
        }

        internal abstract void Update();
    }
}