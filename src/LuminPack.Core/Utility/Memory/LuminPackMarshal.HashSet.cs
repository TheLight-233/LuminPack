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
    /// <summary>Uses the checked dense entry layout to transfer four 32-bit values per output store.</summary>
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

    /// <summary>Gathers the ordered values from groups of four 12-byte entries and writes the scalar tail.</summary>
    /// <param name="destination">The first byte in the reserved payload.</param>
    /// <param name="source">The first entry in the validated dense range.</param>
    /// <param name="count">The number of values to transfer.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void WriteDenseHashSetInt32(scoped ref byte destination, scoped ref byte source, int count)
    {
        while (count >= 4)
        {
            // Each load starts at a value and ends at the next value, in a complete pair of entries.
            var first = Vector128.LoadUnsafe(ref Unsafe.Add(ref source, 8)).AsSingle();
            var second = Vector128.LoadUnsafe(ref Unsafe.Add(ref source, 32)).AsSingle();
            var packed = Sse.Shuffle(first, second, 0xCC).AsUInt32();
            Vector128.StoreUnsafe(packed, ref Unsafe.As<byte, uint>(ref destination));
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
}
#endif
