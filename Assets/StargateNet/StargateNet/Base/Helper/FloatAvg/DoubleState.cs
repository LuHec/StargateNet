using System;
using UnityEngine;

namespace StargateNet
{
    public class DoubleStats
    {
        private double m_count;
        private double m_meanPrev;
        private double m_meanCurr;
        private double m_sPrev;
        private double m_sCurr;
        private double m_varianceCurr;
        private double m_Sum;
        private double m_SqrSum;
        private CircularList<double> m_Entries;

        public double Latest { get; private set; }

        public double Max { get; private set; }

        public double Min { get; private set; }

        public double Average { get; private set; }

        public double StdDeviation
        {
            get
            {
                int count = this.m_Entries.Count;
                this.m_Sum = 0.0;
                this.m_SqrSum = 0.0;
                for (int i = 0; i < this.m_Entries.Count; ++i)
                {
                    this.m_Sum += this.m_Entries[i];
                    this.m_SqrSum += this.m_Entries[i] * this.m_Entries[i];
                }

                this.Average = this.m_Sum / (double)count;
                double d = (this.m_SqrSum - this.m_Sum * this.m_Sum / (double)count) / (double)count;
                return d < 0.0 ? 0.0 : Math.Sqrt(d);
            }
        }

        internal CircularList<double> Data => this.m_Entries;

        internal CircularList<double> GetData() => this.m_Entries;

        public DoubleStats(int windowSize = 128)
        {
            this.m_Entries = new CircularList<double>(windowSize);
        }

        public void Update(double value)
        {
            this.Latest = value;
            if (this.m_Entries.Count == this.m_Entries.Capacity)
            {
                double entry1 = this.m_Entries[0];
                if (entry1 == this.Min || entry1 == this.Max)
                {
                    this.Min = double.MaxValue;
                    this.Max = double.MinValue;
                    for (int i = 0; i < this.m_Entries.Capacity; ++i)
                    {
                        double entry2 = this.m_Entries[i];
                        if (entry2 < this.Min)
                            this.Min = this.m_Entries[i];
                        if (entry2 > this.Max)
                            this.Max = this.m_Entries[i];
                    }
                }

                this.m_Sum -= entry1;
                this.m_SqrSum -= entry1 * entry1;
            }

            if (value > this.Max)
                this.Max = value;
            if (value < this.Min)
                this.Min = value;
            this.m_Entries.Add(value);
            int count = this.m_Entries.Count;
            this.m_Sum += value;
            this.m_SqrSum += value * value;
            this.Average = this.m_Sum / (double)count;
            this.m_count = (double)count;
            if (count == 1)
            {
                this.m_meanCurr = value;
                this.m_sCurr = 0.0;
                this.m_varianceCurr = this.m_sCurr;
            }
            else
            {
                this.m_meanPrev = this.m_meanCurr;
                this.m_sPrev = this.m_sCurr;
                this.m_meanCurr = this.Average;
                this.m_sCurr = this.m_sPrev + (value - this.m_meanPrev) * (value - this.m_meanCurr);
                this.m_varianceCurr = this.m_sCurr / (this.m_count - 1.0);
            }
        }

        internal void ResetTo(float v)
        {
            for (int index = 0; index < this.m_Entries.Capacity; ++index)
                this.Update((double)v);
        }

        public void Reset()
        {
            this.m_Entries.Clear();
            this.Latest = 0.0;
            this.Average = 0.0;
            this.Max = 0.0;
            this.Min = 0.0;
            this.m_Sum = 0.0;
            this.m_SqrSum = 0.0;
        }
    }
}