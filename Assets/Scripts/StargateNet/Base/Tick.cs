using System;

namespace StargateNet
{
    [Serializable]
    public struct Tick : IEquatable<Tick>
    {
        public int TickValue;
        public static Tick InvalidTick = new Tick(-1);

        public bool IsValid => this.TickValue != -1;

        public Tick(int rawValue)
            : this()
        {
            this.TickValue = rawValue;
        }

        private void Invalidate() => this.TickValue = -1;

        public static Tick operator +(Tick a, int b) => new Tick(a.TickValue + b);

        public static Tick operator -(Tick a, int b) => new Tick(a.TickValue - b);

        public static int operator %(Tick a, int b) => a.TickValue % b;

        public static int operator -(Tick a, Tick b) => a.TickValue - b.TickValue;

        public static Tick operator ++(Tick a) => new Tick(a.TickValue + 1);

        public static bool operator >(Tick a, Tick b) => a - b > 0;

        public static bool operator <(Tick a, Tick b) => a - b < 0;

        public static bool operator >=(Tick a, Tick b) => a - b >= 0;

        public static bool operator <=(Tick a, Tick b) => a - b <= 0;

        public static bool operator ==(Tick a, Tick b)
        {
            return a.TickValue == b.TickValue && a.IsValid == b.IsValid;
        }

        public static bool operator !=(Tick a, Tick b)
        {
            return a.TickValue != b.TickValue || a.IsValid != b.IsValid;
        }

        public override int GetHashCode() => this.TickValue;

        public override bool Equals(object obj) => obj is Tick tick && tick.TickValue == this.TickValue;

        public bool Equals(Tick other) => this.TickValue == other.TickValue;

        public override string ToString() => this.TickValue.ToString();
    }
}