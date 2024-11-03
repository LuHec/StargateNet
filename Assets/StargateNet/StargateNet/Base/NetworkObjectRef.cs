using System;

namespace StargateNet
{
    /// <summary>
    /// Network Id
    /// </summary>
    public struct NetworkObjectRef : IEquatable<NetworkObjectRef>
    {
        public int refValue;
        public static NetworkObjectRef InvalidNetworkObjectRef = new NetworkObjectRef(-1);

        public bool IsValid => this.refValue != -1;

        public NetworkObjectRef(int refValue)
            : this()
        {
            this.refValue = refValue;
        }

        private void Invalidate() => this.refValue = -1;

        public static NetworkObjectRef operator +(NetworkObjectRef a, int b) => new NetworkObjectRef(a.refValue + b);

        public static NetworkObjectRef operator -(NetworkObjectRef a, int b) => new NetworkObjectRef(a.refValue - b);

        public static int operator %(NetworkObjectRef a, int b) => a.refValue % b;

        public static int operator -(NetworkObjectRef a, NetworkObjectRef b) => a.refValue - b.refValue;

        public static NetworkObjectRef operator ++(NetworkObjectRef a) => new NetworkObjectRef(a.refValue + 1);

        public static bool operator >(NetworkObjectRef a, NetworkObjectRef b) => a - b > 0;

        public static bool operator <(NetworkObjectRef a, NetworkObjectRef b) => a - b < 0;

        public static bool operator >=(NetworkObjectRef a, NetworkObjectRef b) => a - b >= 0;

        public static bool operator <=(NetworkObjectRef a, NetworkObjectRef b) => a - b <= 0;

        public static bool operator ==(NetworkObjectRef a, NetworkObjectRef b)
        {
            return a.refValue == b.refValue && a.IsValid == b.IsValid;
        }

        public static bool operator !=(NetworkObjectRef a, NetworkObjectRef b)
        {
            return a.refValue != b.refValue || a.IsValid != b.IsValid;
        }

        public override int GetHashCode() => this.refValue;

        public override bool Equals(object obj) => obj is NetworkObjectRef networkRef && networkRef.refValue == this.refValue;

        public bool Equals(NetworkObjectRef other) => this.refValue == other.refValue;

        public override string ToString() => this.refValue.ToString();
    }
}