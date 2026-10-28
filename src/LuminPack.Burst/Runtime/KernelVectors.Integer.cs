#if LUMINPACK_BURST
using System;
using System.Runtime.CompilerServices;
using Unity.Burst.Intrinsics;
namespace LuminPack.Burst
{
    /// <summary>Implements integer lane representation operations shared by native vector adapters.</summary>
    internal static unsafe class LaneBits
    {
        /// <summary>Reads an unsigned integer lane.</summary><param name="p">Lane address.</param><param name="size">Lane width.</param><returns>The lane bits.</returns>
        internal static ulong Read(byte* p, int size) { if (size == 1) return *p; if (size == 2) return *(ushort*)p; if (size == 4) return *(uint*)p; return *(ulong*)p; }
        /// <summary>Writes an integer lane.</summary><param name="p">Lane address.</param><param name="size">Lane width.</param><param name="value">Lane bits.</param>
        internal static void Write(byte* p, int size, ulong value) { if (size == 1) *p = (byte)value; else if (size == 2) *(ushort*)p = (ushort)value; else if (size == 4) *(uint*)p = (uint)value; else *(ulong*)p = value; }
    }
    /// <summary>Provides typed integer operations for the projected Core algorithms.</summary>
    internal unsafe partial struct Vector128<T> where T : unmanaged
    {
        /// <summary>Extracts the low eight bytes.</summary><returns>The low half.</returns>
        internal Vector64<T> GetLower() => new Vector64<T> { Value = Value.ULong0 };
        /// <summary>Extracts the high eight bytes.</summary><returns>The high half.</returns>
        internal Vector64<T> GetUpper() => new Vector64<T> { Value = Value.ULong1 };
        internal static int Count => 16 / sizeof(T);
        internal static Vector128<T> Zero => default;
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<byte> AsByte() => new Vector128<byte> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<ushort> AsUInt16() => new Vector128<ushort> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<short> AsInt16() => new Vector128<short> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<uint> AsUInt32() => new Vector128<uint> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<int> AsInt32() => new Vector128<int> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<ulong> AsUInt64() => new Vector128<ulong> { Value = Value };
        /// <summary>Reads a lane from an already loaded complete vector.</summary><param name="index">Lane index.</param><returns>The lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T GetElement(int index) { v128 copy = Value; return ((T*)&copy)[index]; }
        /// <summary>Stores a complete vector at a native address.</summary><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Store(T* destination)
        {
            if (X86.Sse2.IsSse2Supported) { X86.Sse2.storeu_si128(destination, Value); return; }
            if (Arm.Neon.IsNeonSupported) { Arm.Neon.vst1q_u8((byte*)destination, Value); return; }
            KernelMemory.WriteUnaligned(destination, Value);
        }
        /// <summary>Stores a complete vector at a typed reference.</summary><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref T destination) => StoreUnsafe(ref destination, 0);
        /// <summary>Stores a complete vector at an element offset.</summary><param name="destination">Output.</param><param name="offset">Element offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref T destination, nuint offset) { fixed (T* p = &destination) Store(p + offset); }
        /// <summary>Combines vector bits.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> operator &(Vector128<T> left, Vector128<T> right)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<T> { Value = X86.Sse2.and_si128(left.Value, right.Value) };
            v128 result = default; for (int i = 0; i < 2; i++) ((ulong*)&result)[i] = ((ulong*)&left.Value)[i] & ((ulong*)&right.Value)[i]; return new Vector128<T> { Value = result };
        }
        /// <summary>Combines vector bits.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> operator |(Vector128<T> left, Vector128<T> right)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<T> { Value = X86.Sse2.or_si128(left.Value, right.Value) };
            v128 result = default; for (int i = 0; i < 2; i++) ((ulong*)&result)[i] = ((ulong*)&left.Value)[i] | ((ulong*)&right.Value)[i]; return new Vector128<T> { Value = result };
        }
        /// <summary>Shifts unsigned integer lanes.</summary><param name="value">Input.</param><param name="count">Shift count.</param><returns>The shifted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> operator <<(Vector128<T> value, int count)
        {
            if (X86.Sse2.IsSse2Supported)
            {
                if (sizeof(T) == 2) return new Vector128<T> { Value = X86.Sse2.sll_epi16(value.Value, new v128((ulong)(count & 15), 0UL)) };
                if (sizeof(T) == 4) return new Vector128<T> { Value = X86.Sse2.sll_epi32(value.Value, new v128((ulong)(count & 31), 0UL)) };
            }
            v128 result = default; for (int i = 0; i < 16; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&value.Value + i, sizeof(T)) << count); return new Vector128<T> { Value = result };
        }
        /// <summary>Shifts unsigned integer lanes.</summary><param name="value">Input.</param><param name="count">Shift count.</param><returns>The shifted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector128<T> operator >>(Vector128<T> value, int count)
        {
            if (X86.Sse2.IsSse2Supported)
            {
                if (sizeof(T) == 2) return new Vector128<T> { Value = X86.Sse2.srl_epi16(value.Value, new v128((ulong)(count & 15), 0UL)) };
                if (sizeof(T) == 4) return new Vector128<T> { Value = X86.Sse2.srl_epi32(value.Value, new v128((ulong)(count & 31), 0UL)) };
            }
            v128 result = default; for (int i = 0; i < 16; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&value.Value + i, sizeof(T)) >> count); return new Vector128<T> { Value = result };
        }
    }
    /// <summary>Maps Core integer vector primitives without maintaining serializer traversal.</summary>
    internal static unsafe partial class Vector128
    {
        /// <summary>Loads a complete typed lane.</summary><typeparam name="T">Integer lane type.</typeparam><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> Load<T>(T* source) where T : unmanaged
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<T> { Value = X86.Sse2.loadu_si128(source) };
            if (Arm.Neon.IsNeonSupported) return new Vector128<T> { Value = Arm.Neon.vld1q_u8((byte*)source) };
            return new Vector128<T> { Value = KernelMemory.ReadUnaligned<v128>(source) };
        }
        /// <summary>Loads a complete typed lane.</summary><typeparam name="T">Integer lane type.</typeparam><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> LoadUnsafe<T>(ref T source) where T : unmanaged => LoadUnsafe(ref source, 0);
        /// <summary>Loads a complete typed lane at an element offset.</summary><typeparam name="T">Integer lane type.</typeparam><param name="source">Input.</param><param name="offset">Element offset.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> LoadUnsafe<T>(ref T source, nuint offset) where T : unmanaged { fixed (T* p = &source) return Load(p + offset); }
        /// <summary>Broadcasts a lane.</summary><typeparam name="T">Lane type.</typeparam><param name="value">Lane.</param><returns>The broadcast vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> Create<T>(T value) where T : unmanaged { v128 result = default; for (int i = 0; i < 16 / sizeof(T); i++) ((T*)&result)[i] = value; return new Vector128<T> { Value = result }; }
        /// <summary>Compares integer lanes for equality.</summary><typeparam name="T">Lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>The lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> Equals<T>(Vector128<T> left, Vector128<T> right) where T : unmanaged
        {
            if (X86.Sse2.IsSse2Supported)
            {
                if (sizeof(T) == 1) return new Vector128<T> { Value = X86.Sse2.cmpeq_epi8(left.Value, right.Value) };
                if (sizeof(T) == 2) return new Vector128<T> { Value = X86.Sse2.cmpeq_epi16(left.Value, right.Value) };
                if (sizeof(T) == 4) return new Vector128<T> { Value = X86.Sse2.cmpeq_epi32(left.Value, right.Value) };
            }
            v128 result = default; for (int i = 0; i < 16; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&left.Value + i, sizeof(T)) == LaneBits.Read((byte*)&right.Value + i, sizeof(T)) ? ulong.MaxValue : 0); return new Vector128<T> { Value = result };
        }
        /// <summary>Tests every lane for equality.</summary><typeparam name="T">Lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>Whether all lanes match.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool EqualsAll<T>(Vector128<T> left, Vector128<T> right) where T : unmanaged { if (X86.Sse2.IsSse2Supported) return X86.Sse2.movemask_epi8(X86.Sse2.cmpeq_epi8(left.Value, right.Value)) == 0xFFFF; v128 a = left.Value, b = right.Value; for (int i = 0; i < 2; i++) if (((ulong*)&a)[i] != ((ulong*)&b)[i]) return false; return true; }
        /// <summary>Compares unsigned integer lanes.</summary><typeparam name="T">Unsigned lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>The lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> LessThan<T>(Vector128<T> left, Vector128<T> right) where T : unmanaged
        {

            if (X86.Sse2.IsSse2Supported)
            {
                if (sizeof(T) == 1) { var sign = Create((byte)128); return new Vector128<T> { Value = X86.Sse2.cmpgt_epi8(X86.Sse2.xor_si128(right.Value, sign.Value), X86.Sse2.xor_si128(left.Value, sign.Value)) }; }
                if (sizeof(T) == 2) { var sign = Create((ushort)0x8000); return new Vector128<T> { Value = X86.Sse2.cmpgt_epi16(X86.Sse2.xor_si128(right.Value, sign.Value), X86.Sse2.xor_si128(left.Value, sign.Value)) }; }
                if (sizeof(T) == 4) { var sign = Create(0x80000000u); return new Vector128<T> { Value = X86.Sse2.cmpgt_epi32(X86.Sse2.xor_si128(right.Value, sign.Value), X86.Sse2.xor_si128(left.Value, sign.Value)) }; }
            }
            v128 result = default; for (int i = 0; i < 16; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&left.Value + i, sizeof(T)) < LaneBits.Read((byte*)&right.Value + i, sizeof(T)) ? ulong.MaxValue : 0); return new Vector128<T> { Value = result };
        }
        /// <summary>Tests whether any unsigned lane is greater.</summary><typeparam name="T">Unsigned lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>Whether a lane is greater.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GreaterThanAny<T>(Vector128<T> left, Vector128<T> right) where T : unmanaged { var mask = LessThan(right, left); if (X86.Sse2.IsSse2Supported) return X86.Sse2.movemask_epi8(mask.Value) != 0; return !EqualsAll(mask, Vector128<T>.Zero); }
        /// <summary>Selects bits using a lane mask.</summary><typeparam name="T">Lane type.</typeparam><param name="mask">Mask.</param><param name="left">Set bits.</param><param name="right">Clear bits.</param><returns>The selected vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> ConditionalSelect<T>(Vector128<T> mask, Vector128<T> left, Vector128<T> right) where T : unmanaged => right ^ (mask & (left ^ right));
        /// <summary>Extracts integer lane sign bits.</summary><typeparam name="T">Lane type.</typeparam><param name="value">Input.</param><returns>The sign bit mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ExtractMostSignificantBits<T>(Vector128<T> value) where T : unmanaged
        {
            if (X86.Sse2.IsSse2Supported)
            {
                if (sizeof(T) == 1) return (uint)X86.Sse2.movemask_epi8(value.Value);
                if (sizeof(T) == 2) { uint bits = (uint)X86.Sse2.movemask_epi8(X86.Sse2.packs_epi16(value.Value, value.Value)); return bits & 255u; }
            }
            v128 copy = value.Value; uint mask = 0; for (int i = 0; i < 16 / sizeof(T); i++) mask |= (uint)(((byte*)&copy)[(i + 1) * sizeof(T) - 1] >> 7) << i; return mask;
        }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><param name="a2">Lane 2.</param><param name="a3">Lane 3.</param><param name="a4">Lane 4.</param><param name="a5">Lane 5.</param><param name="a6">Lane 6.</param><param name="a7">Lane 7.</param><param name="a8">Lane 8.</param><param name="a9">Lane 9.</param><param name="a10">Lane 10.</param><param name="a11">Lane 11.</param><param name="a12">Lane 12.</param><param name="a13">Lane 13.</param><param name="a14">Lane 14.</param><param name="a15">Lane 15.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> Create(byte a0, byte a1, byte a2, byte a3, byte a4, byte a5, byte a6, byte a7, byte a8, byte a9, byte a10, byte a11, byte a12, byte a13, byte a14, byte a15) { v128 result = default; ((byte*)&result)[0] = a0; ((byte*)&result)[1] = a1; ((byte*)&result)[2] = a2; ((byte*)&result)[3] = a3; ((byte*)&result)[4] = a4; ((byte*)&result)[5] = a5; ((byte*)&result)[6] = a6; ((byte*)&result)[7] = a7; ((byte*)&result)[8] = a8; ((byte*)&result)[9] = a9; ((byte*)&result)[10] = a10; ((byte*)&result)[11] = a11; ((byte*)&result)[12] = a12; ((byte*)&result)[13] = a13; ((byte*)&result)[14] = a14; ((byte*)&result)[15] = a15; return new Vector128<byte> { Value = result }; }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><param name="a2">Lane 2.</param><param name="a3">Lane 3.</param><param name="a4">Lane 4.</param><param name="a5">Lane 5.</param><param name="a6">Lane 6.</param><param name="a7">Lane 7.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<ushort> Create(ushort a0, ushort a1, ushort a2, ushort a3, ushort a4, ushort a5, ushort a6, ushort a7) { v128 result = default; ((ushort*)&result)[0] = a0; ((ushort*)&result)[1] = a1; ((ushort*)&result)[2] = a2; ((ushort*)&result)[3] = a3; ((ushort*)&result)[4] = a4; ((ushort*)&result)[5] = a5; ((ushort*)&result)[6] = a6; ((ushort*)&result)[7] = a7; return new Vector128<ushort> { Value = result }; }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><param name="a2">Lane 2.</param><param name="a3">Lane 3.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<uint> Create(uint a0, uint a1, uint a2, uint a3) { v128 result = default; ((uint*)&result)[0] = a0; ((uint*)&result)[1] = a1; ((uint*)&result)[2] = a2; ((uint*)&result)[3] = a3; return new Vector128<uint> { Value = result }; }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<ulong> Create(ulong a0, ulong a1) { v128 result = default; ((ulong*)&result)[0] = a0; ((ulong*)&result)[1] = a1; return new Vector128<ulong> { Value = result }; }
        /// <summary>Narrows unsigned words with the Core primitive's truncating semantics.</summary><param name="low">Low words.</param><param name="high">High words.</param><returns>The bytes in logical lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> Narrow(Vector128<ushort> low, Vector128<ushort> high)
        {
            if (X86.Sse2.IsSse2Supported) { var mask = Create((ushort)255); var packed = Sse2.PackUnsignedSaturate((low & mask).AsInt16(), (high & mask).AsInt16()); return packed; }
            if (Arm.Neon.IsNeonSupported) return new Vector128<byte> { Value = Arm.Neon.vcombine_u8(Arm.Neon.vmovn_u16(low.Value), Arm.Neon.vmovn_u16(high.Value)) };
            v128 result = default; for (int i = 0; i < 8; i++) { ((byte*)&result)[i] = (byte)low.GetElement(i); ((byte*)&result)[i + 8] = (byte)high.GetElement(i); }
            return new Vector128<byte> { Value = result };
        }
        /// <summary>Widens one half of a byte vector.</summary><param name="value">Input.</param><returns>The widened words.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<ushort> WidenLower(Vector128<byte> value)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<ushort> { Value = X86.Sse2.unpacklo_epi8(value.Value, default) };
            if (Arm.Neon.IsNeonSupported) return new Vector128<ushort> { Value = Arm.Neon.vmovl_u8(Arm.Neon.vget_low_u8(value.Value)) };
            v128 result = default; for (int i = 0; i < 8; i++) ((ushort*)&result)[i] = value.GetElement(i); return new Vector128<ushort> { Value = result };
        }
        /// <summary>Widens one half of a byte vector.</summary><param name="value">Input.</param><returns>The widened words.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<ushort> WidenUpper(Vector128<byte> value)
        {
            if (X86.Sse2.IsSse2Supported) return new Vector128<ushort> { Value = X86.Sse2.unpackhi_epi8(value.Value, default) };
            if (Arm.Neon.IsNeonSupported) return new Vector128<ushort> { Value = Arm.Neon.vmovl_u8(Arm.Neon.vget_high_u8(value.Value)) };
            v128 result = default; for (int i = 0; i < 8; i++) ((ushort*)&result)[i] = value.GetElement(i + 8); return new Vector128<ushort> { Value = result };
        }
    }
    /// <summary>Provides typed integer operations for the projected Core algorithms.</summary>
    internal unsafe partial struct Vector256<T> where T : unmanaged
    {
        internal static int Count => 32 / sizeof(T);
        internal static Vector256<T> Zero => default;
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector256<byte> AsByte() => new Vector256<byte> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector256<ushort> AsUInt16() => new Vector256<ushort> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector256<short> AsInt16() => new Vector256<short> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector256<uint> AsUInt32() => new Vector256<uint> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector256<int> AsInt32() => new Vector256<int> { Value = Value };
        /// <summary>Reinterprets the same lane bits.</summary><returns>The reinterpreted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector256<ulong> AsUInt64() => new Vector256<ulong> { Value = Value };
        /// <summary>Reads a lane from an already loaded complete vector.</summary><param name="index">Lane index.</param><returns>The lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal T GetElement(int index) { v256 copy = Value; return ((T*)&copy)[index]; }
        /// <summary>Stores a complete vector at a native address.</summary><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Store(T* destination)
        {
            if (X86.Avx2.IsAvx2Supported) { X86.Avx.mm256_storeu_si256(destination, Value); return; }
            KernelMemory.WriteUnaligned(destination, Value);
        }
        /// <summary>Stores a complete vector at a typed reference.</summary><param name="destination">Output.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref T destination) => StoreUnsafe(ref destination, 0);
        /// <summary>Stores a complete vector at an element offset.</summary><param name="destination">Output.</param><param name="offset">Element offset.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void StoreUnsafe(ref T destination, nuint offset) { fixed (T* p = &destination) Store(p + offset); }
        /// <summary>Extracts the low half.</summary><returns>The low half.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<T> GetLower()
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector128<T> { Value = X86.Avx.mm256_castsi256_si128(Value) };
            return new Vector128<T> { Value = Value.Lo128 };
        }
        /// <summary>Extracts the high half.</summary><returns>The high half.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Vector128<T> GetUpper()
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector128<T> { Value = X86.Avx2.mm256_extracti128_si256(Value, 1) };
            return new Vector128<T> { Value = Value.Hi128 };
        }
        /// <summary>Combines vector bits.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> operator &(Vector256<T> left, Vector256<T> right)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<T> { Value = X86.Avx2.mm256_and_si256(left.Value, right.Value) };
            v256 result = default; for (int i = 0; i < 4; i++) ((ulong*)&result)[i] = ((ulong*)&left.Value)[i] & ((ulong*)&right.Value)[i]; return new Vector256<T> { Value = result };
        }
        /// <summary>Combines vector bits.</summary><param name="left">First vector.</param><param name="right">Second vector.</param><returns>The combined bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> operator |(Vector256<T> left, Vector256<T> right)
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<T> { Value = X86.Avx2.mm256_or_si256(left.Value, right.Value) };
            v256 result = default; for (int i = 0; i < 4; i++) ((ulong*)&result)[i] = ((ulong*)&left.Value)[i] | ((ulong*)&right.Value)[i]; return new Vector256<T> { Value = result };
        }
        /// <summary>Shifts unsigned integer lanes.</summary><param name="value">Input.</param><param name="count">Shift count.</param><returns>The shifted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> operator <<(Vector256<T> value, int count)
        {
            if (X86.Avx2.IsAvx2Supported)
            {
                if (sizeof(T) == 2) return new Vector256<T> { Value = X86.Avx2.mm256_sll_epi16(value.Value, new v128((ulong)(count & 15), 0UL)) };
                if (sizeof(T) == 4) return new Vector256<T> { Value = X86.Avx2.mm256_sll_epi32(value.Value, new v128((ulong)(count & 31), 0UL)) };
            }
            v256 result = default; for (int i = 0; i < 32; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&value.Value + i, sizeof(T)) << count); return new Vector256<T> { Value = result };
        }
        /// <summary>Shifts unsigned integer lanes.</summary><param name="value">Input.</param><param name="count">Shift count.</param><returns>The shifted vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector256<T> operator >>(Vector256<T> value, int count)
        {
            if (X86.Avx2.IsAvx2Supported)
            {
                if (sizeof(T) == 2) return new Vector256<T> { Value = X86.Avx2.mm256_srl_epi16(value.Value, new v128((ulong)(count & 15), 0UL)) };
                if (sizeof(T) == 4) return new Vector256<T> { Value = X86.Avx2.mm256_srl_epi32(value.Value, new v128((ulong)(count & 31), 0UL)) };
            }
            v256 result = default; for (int i = 0; i < 32; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&value.Value + i, sizeof(T)) >> count); return new Vector256<T> { Value = result };
        }
    }
    /// <summary>Maps Core integer vector primitives without maintaining serializer traversal.</summary>
    internal static unsafe partial class Vector256
    {
        /// <summary>Loads a complete typed lane.</summary><typeparam name="T">Integer lane type.</typeparam><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> Load<T>(T* source) where T : unmanaged
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<T> { Value = X86.Avx.mm256_loadu_si256(source) };
            return new Vector256<T> { Value = KernelMemory.ReadUnaligned<v256>(source) };
        }
        /// <summary>Loads a complete typed lane.</summary><typeparam name="T">Integer lane type.</typeparam><param name="source">Input.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> LoadUnsafe<T>(ref T source) where T : unmanaged => LoadUnsafe(ref source, 0);
        /// <summary>Loads a complete typed lane at an element offset.</summary><typeparam name="T">Integer lane type.</typeparam><param name="source">Input.</param><param name="offset">Element offset.</param><returns>The loaded vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> LoadUnsafe<T>(ref T source, nuint offset) where T : unmanaged { fixed (T* p = &source) return Load(p + offset); }
        /// <summary>Broadcasts a lane.</summary><typeparam name="T">Lane type.</typeparam><param name="value">Lane.</param><returns>The broadcast vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> Create<T>(T value) where T : unmanaged { v256 result = default; for (int i = 0; i < 32 / sizeof(T); i++) ((T*)&result)[i] = value; return new Vector256<T> { Value = result }; }
        /// <summary>Compares integer lanes for equality.</summary><typeparam name="T">Lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>The lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> Equals<T>(Vector256<T> left, Vector256<T> right) where T : unmanaged
        {
            if (X86.Avx2.IsAvx2Supported)
            {
                if (sizeof(T) == 1) return new Vector256<T> { Value = X86.Avx2.mm256_cmpeq_epi8(left.Value, right.Value) };
                if (sizeof(T) == 2) return new Vector256<T> { Value = X86.Avx2.mm256_cmpeq_epi16(left.Value, right.Value) };
                if (sizeof(T) == 4) return new Vector256<T> { Value = X86.Avx2.mm256_cmpeq_epi32(left.Value, right.Value) };
            }
            v256 result = default; for (int i = 0; i < 32; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&left.Value + i, sizeof(T)) == LaneBits.Read((byte*)&right.Value + i, sizeof(T)) ? ulong.MaxValue : 0); return new Vector256<T> { Value = result };
        }
        /// <summary>Tests every lane for equality.</summary><typeparam name="T">Lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>Whether all lanes match.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool EqualsAll<T>(Vector256<T> left, Vector256<T> right) where T : unmanaged { if (X86.Avx2.IsAvx2Supported) return X86.Avx2.mm256_movemask_epi8(X86.Avx2.mm256_cmpeq_epi8(left.Value, right.Value)) == -1; v256 a = left.Value, b = right.Value; for (int i = 0; i < 4; i++) if (((ulong*)&a)[i] != ((ulong*)&b)[i]) return false; return true; }
        /// <summary>Compares unsigned integer lanes.</summary><typeparam name="T">Unsigned lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>The lane mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> LessThan<T>(Vector256<T> left, Vector256<T> right) where T : unmanaged
        {

            if (X86.Avx2.IsAvx2Supported)
            {
                if (sizeof(T) == 1) { var sign = Create((byte)128); return new Vector256<T> { Value = X86.Avx2.mm256_cmpgt_epi8(X86.Avx2.mm256_xor_si256(right.Value, sign.Value), X86.Avx2.mm256_xor_si256(left.Value, sign.Value)) }; }
                if (sizeof(T) == 2) { var sign = Create((ushort)0x8000); return new Vector256<T> { Value = X86.Avx2.mm256_cmpgt_epi16(X86.Avx2.mm256_xor_si256(right.Value, sign.Value), X86.Avx2.mm256_xor_si256(left.Value, sign.Value)) }; }
                if (sizeof(T) == 4) { var sign = Create(0x80000000u); return new Vector256<T> { Value = X86.Avx2.mm256_cmpgt_epi32(X86.Avx2.mm256_xor_si256(right.Value, sign.Value), X86.Avx2.mm256_xor_si256(left.Value, sign.Value)) }; }
            }
            v256 result = default; for (int i = 0; i < 32; i += sizeof(T)) LaneBits.Write((byte*)&result + i, sizeof(T), LaneBits.Read((byte*)&left.Value + i, sizeof(T)) < LaneBits.Read((byte*)&right.Value + i, sizeof(T)) ? ulong.MaxValue : 0); return new Vector256<T> { Value = result };
        }
        /// <summary>Tests whether any unsigned lane is greater.</summary><typeparam name="T">Unsigned lane type.</typeparam><param name="left">First.</param><param name="right">Second.</param><returns>Whether a lane is greater.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool GreaterThanAny<T>(Vector256<T> left, Vector256<T> right) where T : unmanaged { var mask = LessThan(right, left); if (X86.Avx2.IsAvx2Supported) return X86.Avx2.mm256_movemask_epi8(mask.Value) != 0; return !EqualsAll(mask, Vector256<T>.Zero); }
        /// <summary>Selects bits using a lane mask.</summary><typeparam name="T">Lane type.</typeparam><param name="mask">Mask.</param><param name="left">Set bits.</param><param name="right">Clear bits.</param><returns>The selected vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> ConditionalSelect<T>(Vector256<T> mask, Vector256<T> left, Vector256<T> right) where T : unmanaged => right ^ (mask & (left ^ right));
        /// <summary>Extracts integer lane sign bits.</summary><typeparam name="T">Lane type.</typeparam><param name="value">Input.</param><returns>The sign bit mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static uint ExtractMostSignificantBits<T>(Vector256<T> value) where T : unmanaged
        {
            if (X86.Avx2.IsAvx2Supported)
            {
                if (sizeof(T) == 1) return (uint)X86.Avx2.mm256_movemask_epi8(value.Value);
                if (sizeof(T) == 2) { uint bits = (uint)X86.Avx2.mm256_movemask_epi8(X86.Avx2.mm256_packs_epi16(value.Value, value.Value)); return (bits & 255u) | ((bits >> 8) & 0xFF00u); }
            }
            v256 copy = value.Value; uint mask = 0; for (int i = 0; i < 32 / sizeof(T); i++) mask |= (uint)(((byte*)&copy)[(i + 1) * sizeof(T) - 1] >> 7) << i; return mask;
        }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><param name="a2">Lane 2.</param><param name="a3">Lane 3.</param><param name="a4">Lane 4.</param><param name="a5">Lane 5.</param><param name="a6">Lane 6.</param><param name="a7">Lane 7.</param><param name="a8">Lane 8.</param><param name="a9">Lane 9.</param><param name="a10">Lane 10.</param><param name="a11">Lane 11.</param><param name="a12">Lane 12.</param><param name="a13">Lane 13.</param><param name="a14">Lane 14.</param><param name="a15">Lane 15.</param><param name="a16">Lane 16.</param><param name="a17">Lane 17.</param><param name="a18">Lane 18.</param><param name="a19">Lane 19.</param><param name="a20">Lane 20.</param><param name="a21">Lane 21.</param><param name="a22">Lane 22.</param><param name="a23">Lane 23.</param><param name="a24">Lane 24.</param><param name="a25">Lane 25.</param><param name="a26">Lane 26.</param><param name="a27">Lane 27.</param><param name="a28">Lane 28.</param><param name="a29">Lane 29.</param><param name="a30">Lane 30.</param><param name="a31">Lane 31.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<byte> Create(byte a0, byte a1, byte a2, byte a3, byte a4, byte a5, byte a6, byte a7, byte a8, byte a9, byte a10, byte a11, byte a12, byte a13, byte a14, byte a15, byte a16, byte a17, byte a18, byte a19, byte a20, byte a21, byte a22, byte a23, byte a24, byte a25, byte a26, byte a27, byte a28, byte a29, byte a30, byte a31) { v256 result = default; ((byte*)&result)[0] = a0; ((byte*)&result)[1] = a1; ((byte*)&result)[2] = a2; ((byte*)&result)[3] = a3; ((byte*)&result)[4] = a4; ((byte*)&result)[5] = a5; ((byte*)&result)[6] = a6; ((byte*)&result)[7] = a7; ((byte*)&result)[8] = a8; ((byte*)&result)[9] = a9; ((byte*)&result)[10] = a10; ((byte*)&result)[11] = a11; ((byte*)&result)[12] = a12; ((byte*)&result)[13] = a13; ((byte*)&result)[14] = a14; ((byte*)&result)[15] = a15; ((byte*)&result)[16] = a16; ((byte*)&result)[17] = a17; ((byte*)&result)[18] = a18; ((byte*)&result)[19] = a19; ((byte*)&result)[20] = a20; ((byte*)&result)[21] = a21; ((byte*)&result)[22] = a22; ((byte*)&result)[23] = a23; ((byte*)&result)[24] = a24; ((byte*)&result)[25] = a25; ((byte*)&result)[26] = a26; ((byte*)&result)[27] = a27; ((byte*)&result)[28] = a28; ((byte*)&result)[29] = a29; ((byte*)&result)[30] = a30; ((byte*)&result)[31] = a31; return new Vector256<byte> { Value = result }; }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><param name="a2">Lane 2.</param><param name="a3">Lane 3.</param><param name="a4">Lane 4.</param><param name="a5">Lane 5.</param><param name="a6">Lane 6.</param><param name="a7">Lane 7.</param><param name="a8">Lane 8.</param><param name="a9">Lane 9.</param><param name="a10">Lane 10.</param><param name="a11">Lane 11.</param><param name="a12">Lane 12.</param><param name="a13">Lane 13.</param><param name="a14">Lane 14.</param><param name="a15">Lane 15.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<ushort> Create(ushort a0, ushort a1, ushort a2, ushort a3, ushort a4, ushort a5, ushort a6, ushort a7, ushort a8, ushort a9, ushort a10, ushort a11, ushort a12, ushort a13, ushort a14, ushort a15) { v256 result = default; ((ushort*)&result)[0] = a0; ((ushort*)&result)[1] = a1; ((ushort*)&result)[2] = a2; ((ushort*)&result)[3] = a3; ((ushort*)&result)[4] = a4; ((ushort*)&result)[5] = a5; ((ushort*)&result)[6] = a6; ((ushort*)&result)[7] = a7; ((ushort*)&result)[8] = a8; ((ushort*)&result)[9] = a9; ((ushort*)&result)[10] = a10; ((ushort*)&result)[11] = a11; ((ushort*)&result)[12] = a12; ((ushort*)&result)[13] = a13; ((ushort*)&result)[14] = a14; ((ushort*)&result)[15] = a15; return new Vector256<ushort> { Value = result }; }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><param name="a2">Lane 2.</param><param name="a3">Lane 3.</param><param name="a4">Lane 4.</param><param name="a5">Lane 5.</param><param name="a6">Lane 6.</param><param name="a7">Lane 7.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<uint> Create(uint a0, uint a1, uint a2, uint a3, uint a4, uint a5, uint a6, uint a7) { v256 result = default; ((uint*)&result)[0] = a0; ((uint*)&result)[1] = a1; ((uint*)&result)[2] = a2; ((uint*)&result)[3] = a3; ((uint*)&result)[4] = a4; ((uint*)&result)[5] = a5; ((uint*)&result)[6] = a6; ((uint*)&result)[7] = a7; return new Vector256<uint> { Value = result }; }
        /// <summary>Constructs a vector in lane order.</summary><param name="a0">Lane 0.</param><param name="a1">Lane 1.</param><param name="a2">Lane 2.</param><param name="a3">Lane 3.</param><returns>The vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<ulong> Create(ulong a0, ulong a1, ulong a2, ulong a3) { v256 result = default; ((ulong*)&result)[0] = a0; ((ulong*)&result)[1] = a1; ((ulong*)&result)[2] = a2; ((ulong*)&result)[3] = a3; return new Vector256<ulong> { Value = result }; }
        /// <summary>Joins two complete halves.</summary><typeparam name="T">Lane type.</typeparam><param name="low">Low half.</param><param name="high">High half.</param><returns>The joined vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<T> Create<T>(Vector128<T> low, Vector128<T> high) where T : unmanaged
        {
            if (X86.Avx2.IsAvx2Supported) return new Vector256<T> { Value = X86.Avx2.mm256_inserti128_si256(X86.Avx.mm256_castsi128_si256(low.Value), high.Value, 1) };
            return new Vector256<T> { Value = new v256(low.Value, high.Value) };
        }
        /// <summary>Narrows unsigned words with the Core primitive's truncating semantics.</summary><param name="low">Low words.</param><param name="high">High words.</param><returns>The bytes in logical lane order.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<byte> Narrow(Vector256<ushort> low, Vector256<ushort> high)
        {
            if (X86.Avx2.IsAvx2Supported) { var mask = Create((ushort)255); var packed = Avx2.PackUnsignedSaturate((low & mask).AsInt16(), (high & mask).AsInt16()); return Avx2.Permute4x64(packed.AsUInt64(), 0xD8).AsByte(); }
            v256 result = default; for (int i = 0; i < 16; i++) { ((byte*)&result)[i] = (byte)low.GetElement(i); ((byte*)&result)[i + 16] = (byte)high.GetElement(i); }
            return new Vector256<byte> { Value = result };
        }
        /// <summary>Widens one half of a byte vector.</summary><param name="value">Input.</param><returns>The widened words.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<ushort> WidenLower(Vector256<byte> value) { if (X86.Avx2.IsAvx2Supported) return new Vector256<ushort> { Value = X86.Avx2.mm256_cvtepu8_epi16(value.GetLower().Value) }; v256 result = default; for (int i = 0; i < 16; i++) ((ushort*)&result)[i] = value.GetElement(i + 0); return new Vector256<ushort> { Value = result }; }
        /// <summary>Widens one half of a byte vector.</summary><param name="value">Input.</param><returns>The widened words.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector256<ushort> WidenUpper(Vector256<byte> value) { if (X86.Avx2.IsAvx2Supported) return new Vector256<ushort> { Value = X86.Avx2.mm256_cvtepu8_epi16(value.GetUpper().Value) }; v256 result = default; for (int i = 0; i < 16; i++) ((ushort*)&result)[i] = value.GetElement(i + 16); return new Vector256<ushort> { Value = result }; }
    }
    /// <summary>Represents the bounded four-word load used by Core's 32-bit short ASCII branch.</summary>
    internal unsafe struct Vector64<T> where T : unmanaged { internal ulong Value; }
    /// <summary>Loads the bounded four-word 32-bit primitive.</summary>
    internal static unsafe class Vector64
    {
        /// <summary>Loads eight bytes.</summary><typeparam name="T">Lane type.</typeparam><param name="source">Input.</param><returns>The loaded bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector64<T> LoadUnsafe<T>(ref T source) where T : unmanaged => LoadUnsafe(ref source, 0);
        /// <summary>Loads eight bytes at an element offset.</summary><typeparam name="T">Lane type.</typeparam><param name="source">Input.</param><param name="offset">Element offset.</param><returns>The loaded bits.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector64<T> LoadUnsafe<T>(ref T source, nuint offset) where T : unmanaged { fixed (T* p = &source) return new Vector64<T> { Value = KernelMemory.ReadUnaligned<ulong>(p + offset) }; }
    }
    internal static unsafe partial class Vector128
    {
        /// <summary>Loads the complete sixteen-byte Core mask.</summary><param name="source">Complete input.</param><returns>The mask.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<byte> Create(ReadOnlySpan<byte> source) { fixed (byte* p = source) return Load(p); }
        /// <summary>Joins two eight-byte primitives.</summary><typeparam name="T">Lane type.</typeparam><param name="low">Low half.</param><param name="high">High half.</param><returns>The joined vector.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static Vector128<T> Create<T>(Vector64<T> low, Vector64<T> high) where T : unmanaged => new Vector128<T> { Value = new v128(low.Value, high.Value) };
    }
}
#endif

