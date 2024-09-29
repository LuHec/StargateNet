using System.Runtime.CompilerServices;
using UnityEngine;

namespace StargateNet
{
    public static class SgNetworkUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector4 GetVector4(int* data)
        {
            Vector4 vector4;
            vector4.x = *(float*)(data + 1);
            vector4.y = *(float*)(data + 2);
            vector4.z = *(float*)(data + 3);
            vector4.w = *(float*)(data + 4);
            return vector4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector3 GetVector3(int* data)
        {
            Vector3 vector3;
            vector3.x = *(float*)(data + 1);
            vector3.y = *(float*)(data + 2);
            vector3.z = *(float*)(data + 3);
            return vector3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector2 GetVector2(int* data)
        {
            Vector2 vector2;
            vector2.x = *(float*)(data + 1);
            vector2.y = *(float*)(data + 2);
            return vector2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetVector4(int* data, Vector4 value)
        {
            *(float*)(data + 1) = value.x;
            *(float*)(data + 2) = value.y;
            *(float*)(data + 3) = value.z;
            *(float*)(data + 4) = value.w;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetVector3(int* data, Vector3 value)
        {
            *(float*)(data + 1) = value.x;
            *(float*)(data + 2) = value.y;
            *(float*)(data + 3) = value.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetVector2(int* data, Vector2 value)
        {
            *(float*)(data + 1) = value.x;
            *(float*)(data + 2) = value.y;
        }
    }
}