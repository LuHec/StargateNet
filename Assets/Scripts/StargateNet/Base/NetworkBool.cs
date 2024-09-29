using System;

namespace StargateNet
{
    [Serializable]
    public struct NetworkBool : IEquatable<NetworkBool>
    {
        public int RawValue;

        public NetworkBool(bool value) => this.RawValue = value ? 1 : 0;

        public bool ToBool() => this.RawValue == 1;

        public static bool operator ==(bool a, NetworkBool b) => a == (b.RawValue == 1);

        public static bool operator !=(bool a, NetworkBool b) => a != (b.RawValue == 1);

        public static bool operator ==(NetworkBool a, bool b) => b == (a.RawValue == 1);

        public static bool operator !=(NetworkBool a, bool b) => b != (a.RawValue == 1);

        public static bool operator ==(NetworkBool a, NetworkBool b) => a.RawValue == b.RawValue;

        public static bool operator !=(NetworkBool a, NetworkBool b) => a.RawValue != b.RawValue;

        public static implicit operator NetworkBool(bool val) => new NetworkBool(val);

        public static implicit operator bool(NetworkBool val) => val.RawValue == 1;

        public bool Equals(NetworkBool other) => this.RawValue == other.RawValue;

        public override bool Equals(object obj)
        {
            return obj is NetworkBool networkBool && networkBool.RawValue == this.RawValue;
        }

        public override int GetHashCode() => this.RawValue;

        public override string ToString() => this.RawValue != 1 ? "false" : "true";
    }
}