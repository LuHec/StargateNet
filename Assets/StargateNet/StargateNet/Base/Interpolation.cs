namespace StargateNet
{
    public abstract class Interpolation
    {
        public Tick FromTick { get; private set; }

        public Tick ToTick { get; private set; }
        public Snapshot FromSnapshot { get; private set; }
        public Snapshot ToSnapshot { get; private set; }
        public bool HasSnapshot => this.FromSnapshot != null && this.ToSnapshot != null; 

        public float Alpha { get; private set; }
        public float InterpolationTime { get; private set; }
        
        public Interpolation()
        {
            this.FromTick = Tick.InvalidTick;
            this.ToTick = Tick.InvalidTick;
        }

        internal void Update(Snapshot toSnapshot, Tick toTick)
        {
            this.FromSnapshot = this.ToSnapshot;
            this.ToSnapshot = toSnapshot;
            this.FromTick = this.ToTick;
            this.ToTick = toTick;
        }
    }
}