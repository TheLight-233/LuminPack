#if LUMINPACK_BURST
using System;
using LuminPack.Code;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;

namespace LuminPack.Burst
{
    /// <summary>Maps Core's primitive memory operations to Burst-supported operations without owning traversal policy.</summary>
    internal static unsafe class KernelMemory
    {
        internal static bool Is64Bit => sizeof(void*) == 8;
        /// <summary>Gets a primitive unmanaged storage width.</summary>
        /// <typeparam name="T">The storage type.</typeparam><returns>Its native byte width.</returns>
        internal static int SizeOf<T>() where T : unmanaged => sizeof(T);
        /// <summary>Reinterprets a tracked or native storage reference.</summary>
        /// <typeparam name="TFrom">Input storage.</typeparam><typeparam name="TTo">Output storage.</typeparam>
        /// <param name="value">The reference.</param><returns>The same storage reference.</returns>
        internal static ref TTo As<TFrom, TTo>(ref TFrom value) where TFrom : unmanaged where TTo : unmanaged
        { fixed (TFrom* p = &value) return ref *(TTo*)p; }
        /// <summary>Obtains a reference to a native or already pinned address.</summary>
        /// <typeparam name="T">Storage type.</typeparam><param name="value">Address.</param><returns>The storage reference.</returns>
        internal static ref T AsRef<T>(void* value) where T : unmanaged => ref *(T*)value;
        /// <summary>Adds an element offset to complete caller-owned storage.</summary>
        /// <typeparam name="T">Storage type.</typeparam><param name="value">First element.</param><param name="offset">Element offset.</param><returns>The selected reference.</returns>
        internal static ref T Add<T>(ref T value, nint offset) where T : unmanaged
        { fixed (T* p = &value) return ref p[offset]; }
        /// <summary>Adds an unsigned offset.</summary>
        /// <typeparam name="T">Storage type.</typeparam><param name="value">First element.</param><param name="offset">Element offset.</param><returns>The selected reference.</returns>
        internal static ref T Add<T>(ref T value, nuint offset) where T : unmanaged => ref Add(ref value, (nint)offset);
        /// <summary>Adds an ordinary offset.</summary>
        /// <typeparam name="T">Storage type.</typeparam><param name="value">First element.</param><param name="offset">Element offset.</param><returns>The selected reference.</returns>
        internal static ref T Add<T>(ref T value, int offset) where T : unmanaged => ref Add(ref value, (nint)offset);
        /// <summary>Returns a byte distance within the same complete allocation.</summary>
        /// <param name="start">Start.</param><param name="end">End.</param><returns>The signed byte distance.</returns>
        internal static nint ByteOffset(ref byte start, ref byte end)
        { fixed (byte* a = &start) fixed (byte* b = &end) return (nint)(b - a); }
        /// <summary>Compares two addresses.</summary>
        /// <param name="left">First.</param><param name="right">Second.</param><returns>Whether left is after right.</returns>
        internal static bool IsAddressGreaterThan(ref byte left, ref byte right) => ByteOffset(ref right, ref left) > 0;
        /// <summary>Compares two addresses.</summary>
        /// <param name="left">First.</param><param name="right">Second.</param><returns>Whether left is before right.</returns>
        internal static bool IsAddressLessThan(ref byte left, ref byte right) => ByteOffset(ref left, ref right) > 0;
        /// <summary>Loads an unaligned primitive via a complete native copy.</summary>
        /// <typeparam name="T">Primitive.</typeparam><param name="source">Input.</param><returns>The loaded value.</returns>
        internal static T ReadUnaligned<T>(ref byte source) where T : unmanaged
        { T value = default; CopyBlockUnaligned(ref As<T, byte>(ref value), ref source, (uint)sizeof(T)); return value; }
        /// <summary>Loads complete unaligned native storage.</summary><typeparam name="T">Storage type.</typeparam><param name="source">Input.</param><returns>The loaded value.</returns>
        internal static T ReadUnaligned<T>(void* source) where T : unmanaged => ReadUnaligned<T>(ref *(byte*)source);
        /// <summary>Stores complete unaligned native storage.</summary><typeparam name="T">Storage type.</typeparam><param name="destination">Output.</param><param name="value">Value.</param>
        internal static void WriteUnaligned<T>(void* destination, T value) where T : unmanaged => WriteUnaligned(ref *(byte*)destination, value);
        /// <summary>Maps a complete platform span comparison to the platform memory primitive.</summary><typeparam name="T">Storage type.</typeparam><param name="left">First range.</param><param name="right">Second range.</param><returns>Whether the ranges match.</returns>
        internal static bool SequenceEqual<T>(ReadOnlySpan<T> left, ReadOnlySpan<T> right) where T : unmanaged
        {
            if (left.Length != right.Length) return false;
            if (left.IsEmpty) return true;
            bool handled = false, equal = false;
            ManagedEqual(ref handled, ref equal, left, right);
            if (handled) return equal;
            fixed (T* a = left) fixed (T* b = right) return UnsafeUtility.MemCmp(a, b, (long)left.Length * sizeof(T)) == 0;
        }
        /// <summary>Keeps portable validation on the platform byte comparison outside Unity's native engine.</summary><typeparam name="T">Storage type.</typeparam><param name="handled">Receives true.</param><param name="equal">Comparison result.</param><param name="left">First range.</param><param name="right">Second range.</param>
        [BurstDiscard]
        private static void ManagedEqual<T>(ref bool handled, ref bool equal, ReadOnlySpan<T> left, ReadOnlySpan<T> right) where T : unmanaged
        { handled = true; equal = System.Runtime.InteropServices.MemoryMarshal.AsBytes(left).SequenceEqual(System.Runtime.InteropServices.MemoryMarshal.AsBytes(right)); }
        /// <summary>Stores an unaligned primitive via a complete native copy.</summary>
        /// <typeparam name="T">Primitive.</typeparam><param name="destination">Output.</param><param name="value">Value.</param>
        internal static void WriteUnaligned<T>(ref byte destination, T value) where T : unmanaged
            => CopyBlockUnaligned(ref destination, ref As<T, byte>(ref value), (uint)sizeof(T));
        /// <summary>Maps the raw-copy leaf to native memcpy in Burst, or Core's sole cpblk owner in managed validation.</summary>
        /// <param name="destination">Output.</param><param name="source">Input.</param><param name="count">Bytes.</param>
        internal static void CopyBlockUnaligned(ref byte destination, ref byte source, uint count)
        {
            if (count == 0) return;
            bool handled = false;
            ManagedCopy(ref handled, ref destination, ref source, count);
            if (handled) return;
            fixed (byte* output = &destination) fixed (byte* input = &source) UnsafeUtility.MemCpy(output, input, count);
        }
        /// <summary>Keeps ordinary managed validation on Core's native-copy implementation.</summary>
        /// <param name="handled">Receives true.</param><param name="destination">Output.</param><param name="source">Input.</param><param name="count">Bytes.</param>
        [BurstDiscard]
        private static void ManagedCopy(ref bool handled, ref byte destination, ref byte source, uint count)
        { handled = true; LuminPackMarshal.CopyBlockUnaligned(ref destination, ref source, count); }
        /// <summary>Maps Core's fill leaf to the platform memset operation.</summary>
        /// <param name="destination">Output.</param><param name="value">Fill byte.</param><param name="count">Bytes.</param>
        internal static void InitBlockUnaligned(ref byte destination, byte value, uint count)
        {
            bool handled = false;
            ManagedFill(ref handled, ref destination, value, count);
            if (handled) return;
            fixed (byte* output = &destination) UnsafeUtility.MemSet(output, value, count);
        }
        /// <summary>Provides the managed platform fill for the portable contract host.</summary>
        /// <param name="handled">Receives true.</param><param name="destination">Output.</param><param name="value">Fill byte.</param><param name="count">Bytes.</param>
        [BurstDiscard]
        private static void ManagedFill(ref bool handled, ref byte destination, byte value, uint count)
        { handled = true; fixed (byte* output = &destination) new Span<byte>(output, checked((int)count)).Fill(value); }
    }

    /// <summary>Maps Core bit primitives independently of its matching/traversal algorithms.</summary>
    internal static class KernelBits
    {
        /// <summary>Counts set bits without requiring optional CPU features.</summary><param name="value">Input.</param><returns>The population count.</returns>
        internal static int PopCount(uint value)
        {
            if (Unity.Burst.Intrinsics.X86.Popcnt.IsPopcntSupported) return Unity.Burst.Intrinsics.X86.Popcnt.popcnt_u32(value);
            value -= (value >> 1) & 0x55555555;
            value = (value & 0x33333333) + ((value >> 2) & 0x33333333);
            return (int)((((value + (value >> 4)) & 0x0F0F0F0F) * 0x01010101) >> 24);
        }
        /// <summary>Counts low zero bits.</summary><param name="value">Input.</param><returns>The count including 64 for zero.</returns>
        internal static int TrailingZeroCount(ulong value)
        { if (Unity.Burst.Intrinsics.X86.Bmi1.IsBmi1Supported) return (int)Unity.Burst.Intrinsics.X86.Bmi1.tzcnt_u64(value); if (value == 0) return 64; int count = 0; while ((value & 1) == 0) { value >>= 1; count++; } return count; }
        /// <summary>Counts low zero bits.</summary><param name="value">Input.</param><returns>The count including 32 for zero.</returns>
        internal static int TrailingZeroCount(uint value) => value == 0 ? 32 : TrailingZeroCount((ulong)value);
    }
}
#endif
