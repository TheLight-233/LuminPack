#if NET8_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using LuminPack.Core;

namespace LuminPack.Code;

public static partial class LuminPackMarshal
{
    /// <summary>Uses the checked dense entry layout to transfer groups of 32-bit values.</summary>
    /// <typeparam name="T">The raw hash-set element storage.</typeparam>
    /// <param name="destination">The first byte of the reserved payload.</param>
    /// <param name="entry">The first entry of the probed framework array.</param>
    /// <param name="count">The dense entry count.</param>
    /// <param name="capacity">The captured entry array length.</param>
    /// <returns>Whether this kernel wrote the complete payload.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryWriteDenseHashSetInt32<T>(scoped ref byte destination, scoped ref HashSetView<T>.Entry entry,
        int count, int capacity) where T : unmanaged
    {
        if (!Sse.IsSupported || IntPtr.Size != 8 || Unsafe.SizeOf<T>() != sizeof(uint) ||
            Unsafe.SizeOf<HashSetView<T>.Entry>() != 3 * sizeof(uint) || count < 16 || count > capacity ||
            !DenseHashSetInt32Layout<T>.Available)
            return false;

        ref byte source = ref Unsafe.As<HashSetView<T>.Entry, byte>(ref entry);
        ref byte value = ref Unsafe.As<T, byte>(ref entry.Value);
        if (Unsafe.ByteOffset(ref source, ref value) != 2 * sizeof(uint))
            return false;

        nint displacement = Unsafe.ByteOffset(ref source, ref destination);
        if (displacement > -(nint)count * sizeof(uint) && displacement < (nint)count * (3 * sizeof(uint)))
            return false;

        WriteDenseHashSetInt32(ref destination, ref source, count);
        return true;
    }

    /// <summary>Requires direct proof of the framework entry stride and value offset for vector loads.</summary>
    /// <typeparam name="T">The closed element storage type.</typeparam>
    private static class DenseHashSetInt32Layout<T> where T : unmanaged
    {
        /// <summary>Whether the current runtime exposes and matches the required entry layout.</summary>
        internal static readonly bool Available = Check();

        /// <summary>Probes the actual framework entry rather than relying on an unavailable-metadata fallback.</summary>
        /// <returns>Whether the runtime entry has the expected shape.</returns>
        private static bool Check()
        {
            if (LuminFieldLayout.FieldType(typeof(HashSet<T>), "_entries")?.GetElementType() is not { } entry ||
                LuminFieldLayout.ArrayStride(entry) != 3 * sizeof(uint))
                return false;
            return LuminFieldLayout.FieldType(entry, "Value") == typeof(T) &&
                LuminFieldLayout.ValueField(entry, "Value") == 2 * sizeof(uint);
        }
    }

    /// <summary>Extracts ordered values in groups of sixteen, eight or four 12-byte entries and writes the scalar tail.</summary>
    /// <param name="destination">The first byte in the reserved payload.</param>
    /// <param name="source">The first entry in the validated dense range.</param>
    /// <param name="count">The number of values to transfer.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteDenseHashSetInt32(scoped ref byte destination, scoped ref byte source, int count)
    {
        if (Avx512F.IsSupported)
        {
            // The first two blocks contain five values each, and the third block contains the remaining six.
            var firstTen = Vector512.Create(2U, 5U, 8U, 11U, 14U, 17U, 20U, 23U, 26U, 29U, 0U, 0U, 0U, 0U, 0U, 0U);
            var all = Vector512.Create(0U, 1U, 2U, 3U, 4U, 5U, 6U, 7U, 8U, 9U, 16U, 19U, 22U, 25U, 28U, 31U);
            while (count >= 16)
            {
                ref uint lanes = ref Unsafe.As<byte, uint>(ref source);
                var low = Vector512.LoadUnsafe(ref lanes);
                var middle = Vector512.LoadUnsafe(ref lanes, 16);
                var high = Vector512.LoadUnsafe(ref lanes, 32);
                var prefix = Avx512F.PermuteVar16x32x2(low, firstTen, middle);
                Avx512F.PermuteVar16x32x2(prefix, all, high).StoreUnsafe(ref Unsafe.As<byte, uint>(ref destination));
                source = ref Unsafe.Add(ref source, 192);
                destination = ref Unsafe.Add(ref destination, 16 * sizeof(uint));
                count -= 16;
            }
        }
        if (Avx2.IsSupported)
        {
            while (count >= 8)
            {
                var low = ReadFourDenseHashSetInt32(ref source);
                var high = ReadFourDenseHashSetInt32(ref Unsafe.Add(ref source, 48));
                Vector256.StoreUnsafe(Vector256.Create(low, high), ref Unsafe.As<byte, uint>(ref destination));
                source = ref Unsafe.Add(ref source, 96);
                destination = ref Unsafe.Add(ref destination, 8 * sizeof(uint));
                count -= 8;
            }
        }

        while (count >= 4)
        {
            Vector128.StoreUnsafe(ReadFourDenseHashSetInt32(ref source), ref Unsafe.As<byte, uint>(ref destination));
            source = ref Unsafe.Add(ref source, 48);
            destination = ref Unsafe.Add(ref destination, 4 * sizeof(uint));
            count -= 4;
        }

        while (count != 0)
        {
            LuminPackWriter.Write(ref destination, in Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, 8)));
            source = ref Unsafe.Add(ref source, 12);
            destination = ref Unsafe.Add(ref destination, sizeof(uint));
            count--;
        }
    }

    /// <summary>Extracts four ordered values from four complete 12-byte entries.</summary>
    /// <param name="source">The first byte of the four entries.</param>
    /// <returns>The packed 32-bit values.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ReadFourDenseHashSetInt32(scoped ref byte source)
    {
        // Each load starts at a value and ends at the next value, in a complete pair of entries.
        var first = Vector128.LoadUnsafe(ref Unsafe.Add(ref source, 8)).AsSingle();
        var second = Vector128.LoadUnsafe(ref Unsafe.Add(ref source, 32)).AsSingle();
        return Sse.Shuffle(first, second, 0xCC).AsUInt32();
    }
}
#endif
