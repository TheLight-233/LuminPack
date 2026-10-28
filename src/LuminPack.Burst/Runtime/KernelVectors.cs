#if LUMINPACK_BURST
using System;
using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;

namespace LuminPack.Burst
{
    /// <summary>Disables unsupported native 512-bit instructions while retaining Core's unchanged narrower fallback.</summary>
    internal static class Avx512BW { internal static bool IsSupported => false; }
    /// <summary>Disables unsupported native 512-bit XOR while retaining Core's unchanged narrower fallback.</summary>
    internal static class Avx512F { internal static bool IsSupported => false; }
    /// <summary>Maps the exact AVX2 feature query.</summary>
    internal static partial class Avx2 { internal static bool IsSupported => X86.Avx2.IsAvx2Supported; }
    /// <summary>Maps the exact SSE2 feature query.</summary>
    internal static partial class Sse2 { internal static bool IsSupported => X86.Sse2.IsSse2Supported; }

    /// <summary>Maps Core Vector128 values; algorithm loops and policies live only in projected Core source.</summary>
    internal unsafe partial struct Vector128<T> where T : unmanaged
    {
        internal v128 Value;
        /// <summary>Stores one complete lane at the first output byte.</summary><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref byte destination) => StoreUnsafe(ref destination, 0);
        /// <summary>Stores one complete lane at an explicit byte offset without native-sized optional metadata.</summary><param name="destination">Output.</param><param name="offset">Byte offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref byte destination, nuint offset) => Vector128.StoreUnsafe(new Vector128<byte> { Value = Value }, ref destination, offset);
        /// <summary>Extracts sign bits for the Core comparison-mask operation.</summary><returns>The low lane sign bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint ExtractMostSignificantBits()
        {

            if (X86.Sse2.IsSse2Supported) return (uint)X86.Sse2.movemask_epi8(Value);
            v128 value = Value; byte* p = (byte*)&value; uint mask = 0;
            for (int index = 0; index < 16; index++) mask |= (uint)(p[index] >> 7) << index;
            return mask;
        }
        /// <summary>Maps one vector XOR instruction.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The XOR result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> operator ^(Vector128<T> left, Vector128<T> right)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<T> { Value = X86.Sse2.xor_si128(left.Value, right.Value) };
            if (Arm.Neon.IsNeonSupported) return new Vector128<T> { Value = Arm.Neon.veorq_u8(left.Value, right.Value) };
            return new Vector128<T> { Value = new v128(left.Value.ULong0 ^ right.Value.ULong0, left.Value.ULong1 ^ right.Value.ULong1) };
        }
    }
    /// <summary>Maps the Core Vector128 primitive API to the supported native instruction set.</summary>
    internal static unsafe partial class Vector128
    {
        internal static bool IsHardwareAccelerated => X86.Sse2.IsSse2Supported || Arm.Neon.IsNeonSupported;
        /// <summary>Loads a complete unaligned lane from the first input byte.</summary><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> LoadUnsafe(ref byte source) => LoadUnsafe(ref source, 0);
        /// <summary>Loads one complete unaligned lane.</summary><param name="source">Input.</param><param name="offset">Byte offset.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> LoadUnsafe(ref byte source, nuint offset)
        {
            fixed (byte* p = &source)
            {
                if (X86.Sse2.IsSse2Supported) return new Vector128<byte> { Value = X86.Sse2.loadu_si128(p + offset) };
                if (Arm.Neon.IsNeonSupported) return new Vector128<byte> { Value = Arm.Neon.vld1q_u8(p + offset) };
            }
            return new Vector128<byte> { Value = KernelMemory.ReadUnaligned<v128>(ref KernelMemory.Add(ref source, offset)) };
        }
        /// <summary>Stores a complete unaligned lane at the first output byte.</summary><param name="value">Vector.</param><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StoreUnsafe(Vector128<byte> value, ref byte destination) => StoreUnsafe(value, ref destination, 0);
        /// <summary>Stores one complete unaligned lane.</summary><param name="value">Vector.</param><param name="destination">Output.</param><param name="offset">Byte offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StoreUnsafe(Vector128<byte> value, ref byte destination, nuint offset)
        {
            fixed (byte* p = &destination)
            {
                if (X86.Sse2.IsSse2Supported) { X86.Sse2.storeu_si128(p + offset, value.Value); return; }
                if (Arm.Neon.IsNeonSupported) { Arm.Neon.vst1q_u8(p + offset, value.Value); return; }
            }
            KernelMemory.WriteUnaligned(ref KernelMemory.Add(ref destination, offset), value.Value);
        }
        /// <summary>Maps one lane byte comparison.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The byte equality mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> Equals(Vector128<byte> left, Vector128<byte> right)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<byte> { Value = X86.Sse2.cmpeq_epi8(left.Value, right.Value) };
            if (Arm.Neon.IsNeonSupported) return new Vector128<byte> { Value = Arm.Neon.vceqq_u8(left.Value, right.Value) };
            v128 a = left.Value, b = right.Value, result = default;
            for (int index = 0; index < 16; index++) ((byte*)&result)[index] = ((byte*)&a)[index] == ((byte*)&b)[index] ? (byte)255 : (byte)0;
            return new Vector128<byte> { Value = result };
        }
    }

    /// <summary>Maps Core Vector256 values; algorithm loops and policies live only in projected Core source.</summary>
    internal unsafe partial struct Vector256<T> where T : unmanaged
    {
        internal v256 Value;
        /// <summary>Stores one complete lane at the first output byte.</summary><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref byte destination) => StoreUnsafe(ref destination, 0);
        /// <summary>Stores one complete lane at an explicit byte offset without native-sized optional metadata.</summary><param name="destination">Output.</param><param name="offset">Byte offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref byte destination, nuint offset) => Vector256.StoreUnsafe(new Vector256<byte> { Value = Value }, ref destination, offset);
        /// <summary>Extracts sign bits for the Core comparison-mask operation.</summary><returns>The low lane sign bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal uint ExtractMostSignificantBits()
        {

            if (X86.Avx2.IsAvx2Supported) return (uint)X86.Avx2.mm256_movemask_epi8(Value);
            v256 value = Value; byte* p = (byte*)&value; uint mask = 0;
            for (int index = 0; index < 32; index++) mask |= (uint)(p[index] >> 7) << index;
            return mask;
        }
        /// <summary>Maps one vector XOR instruction.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The XOR result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> operator ^(Vector256<T> left, Vector256<T> right)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<T> { Value = X86.Avx2.mm256_xor_si256(left.Value, right.Value) };
            v256 result = default;
            result.ULong0 = left.Value.ULong0 ^ right.Value.ULong0; result.ULong1 = left.Value.ULong1 ^ right.Value.ULong1;
            result.ULong2 = left.Value.ULong2 ^ right.Value.ULong2; result.ULong3 = left.Value.ULong3 ^ right.Value.ULong3;
            return new Vector256<T> { Value = result };
        }
    }
    /// <summary>Maps the Core Vector256 primitive API to the supported native instruction set.</summary>
    internal static unsafe partial class Vector256
    {
        internal static bool IsHardwareAccelerated => X86.Avx2.IsAvx2Supported;
        /// <summary>Loads a complete unaligned lane from the first input byte.</summary><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<byte> LoadUnsafe(ref byte source) => LoadUnsafe(ref source, 0);
        /// <summary>Loads one complete unaligned lane.</summary><param name="source">Input.</param><param name="offset">Byte offset.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<byte> LoadUnsafe(ref byte source, nuint offset)
        {
            if (X86.Avx2.IsAvx2Supported) { fixed (byte* p = &source) return new Vector256<byte> { Value = X86.Avx.mm256_loadu_si256(p + offset) }; }
            return new Vector256<byte> { Value = KernelMemory.ReadUnaligned<v256>(ref KernelMemory.Add(ref source, offset)) };
        }
        /// <summary>Stores a complete unaligned lane at the first output byte.</summary><param name="value">Vector.</param><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StoreUnsafe(Vector256<byte> value, ref byte destination) => StoreUnsafe(value, ref destination, 0);
        /// <summary>Stores one complete unaligned lane.</summary><param name="value">Vector.</param><param name="destination">Output.</param><param name="offset">Byte offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StoreUnsafe(Vector256<byte> value, ref byte destination, nuint offset)
        {
            if (X86.Avx2.IsAvx2Supported) { fixed (byte* p = &destination) X86.Avx.mm256_storeu_si256(p + offset, value.Value); return; }
            KernelMemory.WriteUnaligned(ref KernelMemory.Add(ref destination, offset), value.Value);
        }
        /// <summary>Maps one lane byte comparison.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The byte equality mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<byte> Equals(Vector256<byte> left, Vector256<byte> right)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<byte> { Value = X86.Avx2.mm256_cmpeq_epi8(left.Value, right.Value) };
            v256 a = left.Value, b = right.Value, result = default;
            for (int index = 0; index < 32; index++) ((byte*)&result)[index] = ((byte*)&a)[index] == ((byte*)&b)[index] ? (byte)255 : (byte)0;
            return new Vector256<byte> { Value = result };
        }
    }

    /// <summary>Maps Core Vector512 values; algorithm loops and policies live only in projected Core source.</summary>
    internal unsafe struct Vector512<T> where T : unmanaged
    {
        internal Wide512 Value;
        /// <summary>Stores one complete lane at the first output byte.</summary><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref byte destination) => StoreUnsafe(ref destination, 0);
        /// <summary>Stores one complete lane at an explicit byte offset without native-sized optional metadata.</summary><param name="destination">Output.</param><param name="offset">Byte offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref byte destination, nuint offset) => Vector512.StoreUnsafe(new Vector512<byte> { Value = Value }, ref destination, offset);
        /// <summary>Extracts sign bits for the Core comparison-mask operation.</summary><returns>The low lane sign bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ulong ExtractMostSignificantBits()
        {

            return 0; // Unreachable: Burst 1.8 has no 512-bit feature path.
        }
        /// <summary>Maps one vector XOR instruction.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The XOR result.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector512<T> operator ^(Vector512<T> left, Vector512<T> right) => new Vector512<T> { Value = default };
    }
    /// <summary>Maps the Core Vector512 primitive API to the supported native instruction set.</summary>
    internal static unsafe class Vector512
    {
        internal static bool IsHardwareAccelerated => false;
        /// <summary>Loads a complete unaligned lane from the first input byte.</summary><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector512<byte> LoadUnsafe(ref byte source) => LoadUnsafe(ref source, 0);
        /// <summary>Loads one complete unaligned lane.</summary><param name="source">Input.</param><param name="offset">Byte offset.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector512<byte> LoadUnsafe(ref byte source, nuint offset)
        {
            return default; // Unreachable without 512-bit support.
        }
        /// <summary>Stores a complete unaligned lane at the first output byte.</summary><param name="value">Vector.</param><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StoreUnsafe(Vector512<byte> value, ref byte destination) => StoreUnsafe(value, ref destination, 0);
        /// <summary>Stores one complete unaligned lane.</summary><param name="value">Vector.</param><param name="destination">Output.</param><param name="offset">Byte offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void StoreUnsafe(Vector512<byte> value, ref byte destination, nuint offset)
        {
        }
        /// <summary>Maps one lane byte comparison.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The byte equality mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector512<byte> Equals(Vector512<byte> left, Vector512<byte> right)
        {
            return default; // Unreachable without 512-bit support.
        }
    }

    /// <summary>Supplies only a compile-time representation for the disabled 512-bit projection.</summary>
    internal struct Wide512 { }
}
#endif
