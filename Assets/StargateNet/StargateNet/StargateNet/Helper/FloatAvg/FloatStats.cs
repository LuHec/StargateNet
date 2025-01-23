using System;

namespace StargateNet
{
    public class FloatStats
    {
        private float m_count;
        private float m_meanPrev;
        private float m_meanCurr;
        private float m_sPrev;
        private float m_sCurr;
        private float m_varianceCurr;
        private float m_Sum;
        private float m_SqrSum;
        private CircularList<float> m_Entries;

        public float Latest { get; private set; }

        public float Max { get; private set; }

        public float Min { get; private set; }

        public float Average { get; private set; }

        public float StdDeviation
        {
            get
            {
                int count = this.m_Entries.Count;
                this.m_Sum = 0.0f;
                this.m_SqrSum = 0.0f;
                for (int i = 0; i < this.m_Entries.Count; ++i)
                {
                    this.m_Sum += this.m_Entries[i];
                    this.m_SqrSum += this.m_Entries[i] * this.m_Entries[i];
                }

                this.Average = this.m_Sum / (float)count;
                float num = (this.m_SqrSum - this.m_Sum * this.m_Sum / (float)count) / (float)count;
                return (double)num < 0.0 ? 0.0f : MathF.Sqrt(num);
            }
        }

        internal CircularList<float> Data => this.m_Entries;

        internal CircularList<float> GetData() => this.m_Entries;

        public FloatStats(int windowSize = 128) => this.m_Entries = new CircularList<float>(windowSize);

        public void Update(float value)
        {
            this.Latest = value;
            if (this.m_Entries.Count == this.m_Entries.Capacity)
            {
                float entry1 = this.m_Entries[0];
                if ((double)entry1 == (double)this.Min || (double)entry1 == (double)this.Max)
                {
                    this.Min = float.MaxValue;
                    this.Max = float.MinValue;
                    for (int i = 0; i < this.m_Entries.Capacity; ++i)
                    {
                        double entry2 = (double)this.m_Entries[i];
                        if (entry2 < (double)this.Min)
                            this.Min = this.m_Entries[i];
                        if (entry2 > (double)this.Max)
                            this.Max = this.m_Entries[i];
                    }
                }

                this.m_Sum -= entry1;
                this.m_SqrSum -= entry1 * entry1;
            }

            if ((double)value > (double)this.Max)
                this.Max = value;
            if ((double)value < (double)this.Min)
                this.Min = value;
            this.m_Entries.Add(value);
            int count = this.m_Entries.Count;
            this.m_Sum += value;
            this.m_SqrSum += value * value;
            this.Average = this.m_Sum / (float)count;
            this.m_count = (float)count;
            if (count == 1)
            {
                this.m_meanCurr = value;
                this.m_sCurr = 0.0f;
                this.m_varianceCurr = this.m_sCurr;
            }
            else
            {
                this.m_meanPrev = this.m_meanCurr;
                this.m_sPrev = this.m_sCurr;
                this.m_meanCurr = this.Average;
                this.m_sCurr = this.m_sPrev + (float)(((double)value - (double)this.m_meanPrev) *
                                                      ((double)value - (double)this.m_meanCurr));
                this.m_varianceCurr = this.m_sCurr / (this.m_count - 1f);
            }
        }

        internal void ResetTo(float v)
        {
            for (int index = 0; index < this.m_Entries.Capacity; ++index)
                this.Update(v);
        }

        public void Reset()
        {
            this.m_Entries.Clear();
            this.Latest = 0.0f;
            this.Average = 0.0f;
            this.Max = 0.0f;
            this.Min = 0.0f;
            this.m_Sum = 0.0f;
            this.m_SqrSum = 0.0f;
        }
    }
}