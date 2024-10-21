using System;
using System.Runtime.CompilerServices;

namespace StargateNet
{
    public struct NetworkObjectRef : IEquatable<NetworkObjectRef>
    {
        public readonly int objectId;

        // public NetworkObjectRef(NetworkObject obj)
        // {
        //     this.objectId = (UnityEngine.Object) obj != (UnityEngine.Object) null ? obj.Id : -1;
        // }

        // public bool TryGetObject(NetworkSandbox sandbox, out NetworkObject obj)
        // {
        //     NetworkObject networkObject;
        //     if (sandbox.TryGetObject(this.objectId, out networkObject))
        //     {
        //         obj = networkObject;
        //         return true;
        //     }
        //     obj = (NetworkObject) null;
        //     return false;
        // }
        //
        // public NetworkObject GetObject(NetworkSandbox sandbox)
        // {
        //     NetworkObject networkObject;
        //     return sandbox.TryGetObject(this.objectId, out networkObject) ? networkObject : (NetworkObject) null;
        // }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode() => this.objectId.GetHashCode();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object other)
        {
            return other is NetworkObjectRef other1 && this.Equals(other1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(NetworkObjectRef other) => this.objectId.Equals(other.objectId);
    }
}