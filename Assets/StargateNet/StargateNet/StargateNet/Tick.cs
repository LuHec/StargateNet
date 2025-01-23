using System;
using UnityEngine.Serialization;

namespace StargateNet
{
    [Serializable]
    public struct Tick : IEquatable<Tick>                              
    {
        public int tickValue;
        public static Tick InvalidTick = new Tick(-1);

        public bool IsValid => this.tickValue != -1;

        public Tick(int rawValue)
            : this()
        {
            this.tickValue = rawValue;
        }

        private void Invalidate() => this.tickValue = -1;

        public static Tick operator +(Tick a, int b) => new Tick(a.tickValue + b);

        public static Tick operator -(Tick a, int b) => new Tick(a.tickValue - b);

        public static int operator %(Tick a, int b) => a.tickValue % b;

        public static int operator -(Tick a, Tick b) => a.tickValue - b.tickValue;

        public static Tick operator ++(Tick a) => new Tick(a.tickValue + 1);

        public static bool operator >(Tick a, Tick b) => a - b > 0;

        public static bool operator <(Tick a, Tick b) => a - b < 0;

        public static bool operator >=(Tick a, Tick b) => a - b >= 0;

        public static bool operator <=(Tick a, Tick b) => a - b <= 0;

        public static bool operator ==(Tick a, Tick b)
        {
            return a.tickValue == b.tickValue && a.IsValid == b.IsValid;
        }

        public static bool operator !=(Tick a, Tick b)
        {
            return a.tickValue != b.tickValue || a.IsValid != b.IsValid;
        }

        public override int GetHashCode() => this.tickValue;

        public override bool Equals(object obj) => obj is Tick tick && tick.tickValue == this.tickValue;

        public bool Equals(Tick other) => this.tickValue == other.tickValue;

        public override string ToString() => this.tickValue.ToString();
    }
}