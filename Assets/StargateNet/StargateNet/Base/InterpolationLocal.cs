namespace StargateNet
{
    /// <summary>
    /// 本地插值，适用于服务端所有的物体，以及客户端预测的物体。
    /// 不论客户端还是服务端，真实的逻辑位置都是在FixedUpdate更新时同步的，只有渲染延迟会体现在客户端上。
    /// 本地观感上的输入延迟是硬件物理延迟+FixedUpdate的延迟，因为需要延缓一个Fixed帧做渲染插值。但是输入会立刻同步到服务端。
    /// </summary>
    public class InterpolationLocal : Interpolation
    {
        public InterpolationLocal(StargateEngine stargateEngine) : base(stargateEngine)
        {
        }

        internal override Tick FromTick => this.Engine.Tick - 1;
        internal override Tick ToTick => this.Engine.Tick;

        /// <summary>
        /// InterpolationLocal永远有Snapshot，因为客户端采用本地的预测/服务端是权威
        /// </summary>
        internal override bool HasSnapshot => true;

        internal override float Alpha => this.Engine.SimulationClock.Alpha;
        internal override float InterpolationTime { get; }

        internal void Update()
        {
            this.FromSnapshot = this.Engine.Simulation.fromSnapshot;
            this.ToSnapshot = this.Engine.Simulation.toSnapshot;
        }
    }
}