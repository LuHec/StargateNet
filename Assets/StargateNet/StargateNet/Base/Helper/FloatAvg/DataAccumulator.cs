namespace StargateNet
{
    public sealed class DataAccumulator
    {
        private float _accumulatedTime;
        private float _accumulation;
        private FloatStats _rollingAverage;

        internal float AvgKBps => this._rollingAverage.Average / 1000f;

        public float Avg => this._rollingAverage.Average;

        public float Latest => this._rollingAverage.Latest;

        public DataAccumulator(int windowSize) => this._rollingAverage = new FloatStats(windowSize);

        public void Add(int amount) => this._accumulation += (float) amount;

        public void Add(float amount) => this._accumulation += amount;

        public void Update(float delta)
        {
            this._accumulatedTime += delta;
            if ((double) this._accumulatedTime < 1.0)
                return;
            this._rollingAverage.Update(this._accumulation);
            this._accumulation = 0.0f;
            this._accumulatedTime = 0.0f;
        }

        public void Stop()
        {
            this._rollingAverage.Update(this._accumulation);
            this._accumulation = 0.0f;
            this._accumulatedTime = 0.0f;
        }

        public void Reset()
        {
            this._rollingAverage.Reset();
            this._accumulation = 0.0f;
            this._accumulatedTime = 0.0f;
        }
    }
}