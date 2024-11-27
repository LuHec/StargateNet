using System.Runtime.CompilerServices;
using UnityEngine;

namespace StargateNet
{
    public static class StargateNetUtil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignTo(int value, int alignment)
        {
            // (x + (align - 1)) & ~(align - 1)
            return (value + (alignment - 1)) & ~(alignment - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe byte[] CopyToBytes(void* data, int size)
        {
            byte[] res = new byte[size];
            byte* byteData = (byte*)data;
            for (int i = 0; i < size; i++)
            {
                res[i] = byteData[i];
            }

            return res;
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector4 GetVector4(int* data)
        {
            Vector4 vector4;
            float* floatData = (float*)data; // 先转换为float*类型
            vector4.x = floatData[0];
            vector4.y = floatData[1];
            vector4.z = floatData[2];
            vector4.w = floatData[3];
            return vector4;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector3 GetVector3(int* data)
        {
            Vector3 vector3;
            float* floatData = (float*)data; // 先转换为float*类型
            vector3.x = floatData[0];
            vector3.y = floatData[1];
            vector3.z = floatData[2];
            return vector3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe Vector2 GetVector2(int* data)
        {
            Vector2 vector2;
            float* floatData = (float*)data; // 先转换为float*类型
            vector2.x = floatData[0];
            vector2.y = floatData[1];
            return vector2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetVector4(int* data, Vector4 value)
        {
            float* floatData = (float*)data; // 先转换为float*类型
            floatData[0] = value.x;
            floatData[1] = value.y;
            floatData[2] = value.z;
            floatData[3] = value.w;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetVector3(int* data, Vector3 value)
        {
            float* floatData = (float*)data; // 先转换为float*类型
            floatData[0] = value.x;
            floatData[1] = value.y;
            floatData[2] = value.z;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetVector2(int* data, Vector2 value)
        {
            float* floatData = (float*)data; // 先转换为float*类型
            floatData[0] = value.x;
            floatData[1] = value.y;
        }
    }
}