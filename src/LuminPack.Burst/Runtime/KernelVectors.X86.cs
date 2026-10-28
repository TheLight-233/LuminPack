#if LUMINPACK_BURST
using Unity.Burst.Intrinsics;
using System.Runtime.CompilerServices;
namespace LuminPack.Burst
{
    /// <summary>Maps exact Sse2 integer primitives used by Core text and cryptography algorithms.</summary>
    internal static unsafe partial class Sse2
    {
        /// <summary>Loads a complete unaligned vector.</summary><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<uint> LoadVector128(uint* source) => Vector128.Load(source);
        /// <summary>Stores a complete unaligned vector.</summary><param name="destination">Output.</param><param name="value">Vector.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Store(uint* destination, Vector128<uint> value) => value.Store(destination);
        /// <summary>Combines vector bits.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> And<T>(Vector128<T> a, Vector128<T> b) where T : unmanaged => a & b;
        /// <summary>Combines vector bits.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> Or<T>(Vector128<T> a, Vector128<T> b) where T : unmanaged => a | b;
        /// <summary>Combines vector bits.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> Xor<T>(Vector128<T> a, Vector128<T> b) where T : unmanaged => a ^ b;
        /// <summary>Compares lanes for equality.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> CompareEqual<T>(Vector128<T> a, Vector128<T> b) where T : unmanaged => Vector128.Equals(a, b);
        /// <summary>Compares signed lanes.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The signed comparison mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<short> CompareGreaterThan(Vector128<short> a, Vector128<short> b)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<short> { Value = X86.Sse2.cmpgt_epi16(a.Value, b.Value) };
            v128 result = default; for (int i = 0; i < 8; i++) ((short*)&result)[i] = a.GetElement(i) > b.GetElement(i) ? (short)(-1) : (short)0; return new Vector128<short> { Value = result };
        }
        /// <summary>Compares signed lanes.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The signed comparison mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<int> CompareGreaterThan(Vector128<int> a, Vector128<int> b)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<int> { Value = X86.Sse2.cmpgt_epi32(a.Value, b.Value) };
            v128 result = default; for (int i = 0; i < 4; i++) ((int*)&result)[i] = a.GetElement(i) > b.GetElement(i) ? (int)(-1) : (int)0; return new Vector128<int> { Value = result };
        }
        /// <summary>Shifts integer lanes logically.</summary><typeparam name="T">Lane type.</typeparam><param name="a">Input.</param><param name="count">Bit count.</param><returns>The shifted lanes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> ShiftLeftLogical<T>(Vector128<T> a, byte count) where T : unmanaged => a << count;
        /// <summary>Shifts integer lanes logically.</summary><typeparam name="T">Lane type.</typeparam><param name="a">Input.</param><param name="count">Bit count.</param><returns>The shifted lanes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> ShiftRightLogical<T>(Vector128<T> a, byte count) where T : unmanaged => a >> count;
        /// <summary>Extracts byte sign bits.</summary><param name="a">Input.</param><returns>The byte mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int MoveMask(Vector128<byte> a) => (int)a.ExtractMostSignificantBits();
        /// <summary>Packs signed words with unsigned saturation in native lane order.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The packed bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> PackUnsignedSaturate(Vector128<short> a, Vector128<short> b)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<byte> { Value = X86.Sse2.packus_epi16(a.Value, b.Value) };
            v128 result = default; for (int h = 0; h < 1; h++) for (int i = 0; i < 8; i++) { int x = a.GetElement(h * 8 + i), y = b.GetElement(h * 8 + i); ((byte*)&result)[h * 16 + i] = (byte)(x < 0 ? 0 : x > 255 ? 255 : x); ((byte*)&result)[h * 16 + i + 8] = (byte)(y < 0 ? 0 : y > 255 ? 255 : y); }
            return new Vector128<byte> { Value = result };
        }
        /// <summary>Adds wrapping unsigned words.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<uint> Add(Vector128<uint> a, Vector128<uint> b)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<uint> { Value = X86.Sse2.add_epi32(a.Value, b.Value) };
            v128 result = default; for (int i = 0; i < 4; i++) ((uint*)&result)[i] = a.GetElement(i) + b.GetElement(i); return new Vector128<uint> { Value = result };
        }
        /// <summary>Subtracts unsigned words with saturation.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The differences.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<ushort> SubtractSaturate(Vector128<ushort> a, Vector128<ushort> b)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<ushort> { Value = X86.Sse2.subs_epu16(a.Value, b.Value) };
            v128 result = default; for (int i = 0; i < 8; i++) { int x = a.GetElement(i) - b.GetElement(i); ((ushort*)&result)[i] = (ushort)(x > 0 ? x : 0); }
            return new Vector128<ushort> { Value = result };
        }
        /// <summary>Permutes four 32-bit lanes.</summary><param name="a">Input.</param><param name="control">Lane selectors.</param><returns>The permuted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<uint> Shuffle(Vector128<uint> a, byte control)
        {
            if (X86.Sse2.IsSse2Supported)
            {
                if (control == 0x39) return new Vector128<uint> { Value = X86.Sse2.shuffle_epi32(a.Value, 0x39) };
                if (control == 0x4E) return new Vector128<uint> { Value = X86.Sse2.shuffle_epi32(a.Value, 0x4E) };
                if (control == 0x93) return new Vector128<uint> { Value = X86.Sse2.shuffle_epi32(a.Value, 0x93) };
            }
            v128 result = default; for (int i = 0; i < 4; i++) ((uint*)&result)[i] = a.GetElement((control >> (i * 2)) & 3); return new Vector128<uint> { Value = result };
        }
    }
    /// <summary>Maps exact Avx2 integer primitives used by Core text and cryptography algorithms.</summary>
    internal static unsafe partial class Avx2
    {
        /// <summary>Permutes four 32-bit lanes independently in each half.</summary><param name="a">Input.</param><param name="control">Lane selectors.</param><returns>The permuted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<uint> Shuffle(Vector256<uint> a, byte control)
        {
            if (X86.Avx2.IsAvx2Supported)
            {
                if (control == 0x39) return new Vector256<uint> { Value = X86.Avx2.mm256_shuffle_epi32(a.Value, 0x39) };
                if (control == 0x4E) return new Vector256<uint> { Value = X86.Avx2.mm256_shuffle_epi32(a.Value, 0x4E) };
                if (control == 0x93) return new Vector256<uint> { Value = X86.Avx2.mm256_shuffle_epi32(a.Value, 0x93) };
            }
            return Vector256.Create(Sse2.Shuffle(a.GetLower(), control), Sse2.Shuffle(a.GetUpper(), control));
        }
        /// <summary>Combines vector bits.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> And<T>(Vector256<T> a, Vector256<T> b) where T : unmanaged => a & b;
        /// <summary>Combines vector bits.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> Or<T>(Vector256<T> a, Vector256<T> b) where T : unmanaged => a | b;
        /// <summary>Combines vector bits.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> Xor<T>(Vector256<T> a, Vector256<T> b) where T : unmanaged => a ^ b;
        /// <summary>Compares lanes for equality.</summary><typeparam name="T">Lane type.</typeparam><param name="a">First.</param><param name="b">Second.</param><returns>The lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> CompareEqual<T>(Vector256<T> a, Vector256<T> b) where T : unmanaged => Vector256.Equals(a, b);
        /// <summary>Compares signed lanes.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The signed comparison mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<short> CompareGreaterThan(Vector256<short> a, Vector256<short> b)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<short> { Value = X86.Avx2.mm256_cmpgt_epi16(a.Value, b.Value) };
            v256 result = default; for (int i = 0; i < 16; i++) ((short*)&result)[i] = a.GetElement(i) > b.GetElement(i) ? (short)(-1) : (short)0; return new Vector256<short> { Value = result };
        }
        /// <summary>Compares signed lanes.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The signed comparison mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<int> CompareGreaterThan(Vector256<int> a, Vector256<int> b)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<int> { Value = X86.Avx2.mm256_cmpgt_epi32(a.Value, b.Value) };
            v256 result = default; for (int i = 0; i < 8; i++) ((int*)&result)[i] = a.GetElement(i) > b.GetElement(i) ? (int)(-1) : (int)0; return new Vector256<int> { Value = result };
        }
        /// <summary>Shifts integer lanes logically.</summary><typeparam name="T">Lane type.</typeparam><param name="a">Input.</param><param name="count">Bit count.</param><returns>The shifted lanes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> ShiftLeftLogical<T>(Vector256<T> a, byte count) where T : unmanaged => a << count;
        /// <summary>Shifts integer lanes logically.</summary><typeparam name="T">Lane type.</typeparam><param name="a">Input.</param><param name="count">Bit count.</param><returns>The shifted lanes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> ShiftRightLogical<T>(Vector256<T> a, byte count) where T : unmanaged => a >> count;
        /// <summary>Extracts byte sign bits.</summary><param name="a">Input.</param><returns>The byte mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int MoveMask(Vector256<byte> a) => (int)a.ExtractMostSignificantBits();
        /// <summary>Packs signed words with unsigned saturation in native lane order.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The packed bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<byte> PackUnsignedSaturate(Vector256<short> a, Vector256<short> b)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<byte> { Value = X86.Avx2.mm256_packus_epi16(a.Value, b.Value) };
            v256 result = default; for (int h = 0; h < 2; h++) for (int i = 0; i < 8; i++) { int x = a.GetElement(h * 8 + i), y = b.GetElement(h * 8 + i); ((byte*)&result)[h * 16 + i] = (byte)(x < 0 ? 0 : x > 255 ? 255 : x); ((byte*)&result)[h * 16 + i + 8] = (byte)(y < 0 ? 0 : y > 255 ? 255 : y); }
            return new Vector256<byte> { Value = result };
        }
        /// <summary>Adds wrapping unsigned words.</summary><param name="a">First.</param><param name="b">Second.</param><returns>The sums.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<uint> Add(Vector256<uint> a, Vector256<uint> b)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<uint> { Value = X86.Avx2.mm256_add_epi32(a.Value, b.Value) };
            v256 result = default; for (int i = 0; i < 8; i++) ((uint*)&result)[i] = a.GetElement(i) + b.GetElement(i); return new Vector256<uint> { Value = result };
        }
        /// <summary>Permutes four 64-bit lanes.</summary><param name="a">Input.</param><param name="control">Lane selectors.</param><returns>The permuted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<ulong> Permute4x64(Vector256<ulong> a, byte control)
        {
            if (X86.Avx2.IsAvx2Supported) { if (control == 0xD8) return new Vector256<ulong> { Value = X86.Avx2.mm256_permute4x64_epi64(a.Value, 0xD8) }; }
            v256 result = default; for (int i = 0; i < 4; i++) ((ulong*)&result)[i] = a.GetElement((control >> (i * 2)) & 3); return new Vector256<ulong> { Value = result };
        }
        /// <summary>Shuffles bytes independently in each 128-bit lane.</summary><param name="a">Input.</param><param name="mask">Byte selectors.</param><returns>The selected bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<byte> Shuffle(Vector256<byte> a, Vector256<byte> mask)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<byte> { Value = X86.Avx2.mm256_shuffle_epi8(a.Value, mask.Value) };
            return Vector256.Create(Ssse3.Shuffle(a.GetLower(), mask.GetLower()), Ssse3.Shuffle(a.GetUpper(), mask.GetUpper()));
        }
    }
    /// <summary>Maps SSSE3 byte shuffle with a bounded primitive fallback.</summary>
    internal static unsafe class Ssse3
    {
        internal static bool IsSupported => X86.Ssse3.IsSsse3Supported;
        /// <summary>Selects bytes according to the pshufb primitive semantics.</summary><param name="a">Input.</param><param name="mask">Selectors.</param><returns>The selected bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> Shuffle(Vector128<byte> a, Vector128<byte> mask)
        {
            if (X86.Ssse3.IsSsse3Supported) return new Vector128<byte> { Value = X86.Ssse3.shuffle_epi8(a.Value, mask.Value) };
            v128 result = default; for (int i = 0; i < 16; i++) { byte k = mask.GetElement(i); ((byte*)&result)[i] = (k & 128) != 0 ? (byte)0 : a.GetElement(k & 15); }
            return new Vector128<byte> { Value = result };
        }
    }
    /// <summary>Maps the ARM64 table lookup used by Core layout conversion.</summary>
    internal static class AdvSimd
    {
        /// <summary>Exposes the exact 64-bit Neon execution requirement.</summary>
        internal static unsafe class Arm64
        {
            internal static bool IsSupported => sizeof(void*) == 8 && Arm.Neon.IsNeonSupported;
            /// <summary>Selects in-range table bytes and zeroes all other indices.</summary><param name="source">Table.</param><param name="indices">Selectors.</param><returns>The selected bytes.</returns>
            internal static Vector128<byte> VectorTableLookup(Vector128<byte> source, Vector128<byte> indices)
            {
                if (Arm.Neon.IsNeonSupported)
                {
                    if (sizeof(void*) == 8) return new Vector128<byte> { Value = Arm.Neon.vqtbl1q_u8(source.Value, indices.Value) };
                }
                v128 result = default; for (int i = 0; i < 16; i++) { byte index = indices.GetElement(i); ((byte*)&result)[i] = index < 16 ? source.GetElement(index) : (byte)0; }
                return new Vector128<byte> { Value = result };
            }
        }
    }
}
#endif

