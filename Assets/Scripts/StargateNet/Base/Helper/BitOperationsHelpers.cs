﻿#if UNITY_2021_3_OR_NEWER || GODOT
using System;
using System.Runtime.InteropServices;
#endif
using System.Runtime.CompilerServices;
#if NET5_0_OR_GREATER
using System.Numerics;
#endif

// ReSharper disable ALL

namespace tlsf
{
    public static class BitOperationsHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(ulong value)
        {
#if NET5_0_OR_GREATER
            return BitOperations.LeadingZeroCount(value);
#else
            var high = (uint)(value >> 32);
            return high == 0 ? 32 + LeadingZeroCount((uint)value) : 31 ^ Log2(high);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(uint value)
        {
#if NET5_0_OR_GREATER
            return BitOperations.LeadingZeroCount(value);
#else
            return value == 0 ? 32 : 31 ^ Log2(value);
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Log2(uint value)
        {
#if NET5_0_OR_GREATER
            return BitOperations.Log2(value);
#else
            value |= 1;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return Unsafe.AddByteOffset(ref MemoryMarshal.GetReference(Log2DeBruijn),
                (nint)(int)((value * 130329821U) >> 27));
#endif
        }

#if !NET5_0_OR_GREATER
        private static ReadOnlySpan<byte> Log2DeBruijn => new byte[32]
        {
            0, 9, 1, 10, 13, 21, 2, 29,
            11, 14, 16, 18, 22, 25, 3, 30,
            8, 12, 20, 28, 15, 17, 24, 7,
            19, 27, 23, 6, 26, 5, 4, 31
        };
#endif
    }
}