namespace StargateNet
{
    public abstract class Interpolation
    {
        internal StargateEngine Engine { private set; get; }
        public Snapshot FromSnapshot { get; set; }
        public Snapshot ToSnapshot { get; set; }
        internal abstract Tick FromTick { get; }

        internal abstract Tick ToTick { get; }

        public abstract bool HasSnapshot { get; }

        public abstract float Alpha { get; }
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