#if NET8_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using LuminPack.Core;

namespace LuminPack.Utility;

public static partial class LuminFrozenConstruction
{
    /// <summary>Tests the architecture, lane widths and count required by the shared interleave.</summary>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    /// <param name="count">The number of pairs in the input.</param>
    /// <returns>Whether a vector prefix is worthwhile.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool CanInterleave<TKey, TValue>(int count) where TKey : unmanaged where TValue : unmanaged
        => IntPtr.Size == 8 && (Sse2.IsSupported || AdvSimd.Arm64.IsSupported) &&
            Unsafe.SizeOf<TKey>() == Unsafe.SizeOf<TValue>() && (Unsafe.SizeOf<TKey>() switch
        {
            1 => count >= 64,
            2 or 8 => count >= 32,
            4 => count >= 16,
            _ => false
        });

    /// <summary>Interleaves once into a proven disjoint output window, then writes the scalar tail.</summary>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    /// <param name="writer">The writer positioned after the header.</param>
    /// <param name="keys">The captured key array.</param>
    /// <param name="values">The captured value array.</param>
    /// <param name="count">The number of pairs to write.</param>
    /// <returns>Whether the entire payload was written.</returns>
    private static bool TryWriteInterleavedPayload<TKey, TValue>(ref LuminPackWriter writer, TKey[] keys, TValue[] values, int count)
        where TKey : unmanaged where TValue : unmanaged
    {
        int width = Unsafe.SizeOf<TKey>();
        long bytes = (long)count * (2 * width);
        if (bytes > writer.RecordRoom() || count > keys.Length || count > values.Length)
            return false;
        var payload = writer.Buffer.Slice(writer.Position, (int)bytes);
        int index = WriteInterleavedPrefix<TKey, TValue>(payload, keys.AsSpan(0, count), values.AsSpan(0, count));
        if (index == 0)
            return false;
        ref byte destination = ref Unsafe.Add(ref MemoryMarshal.GetReference(payload), (nint)index * (2 * width));
        ref TKey firstKey = ref MemoryMarshal.GetArrayDataReference(keys);
        ref TValue firstValue = ref MemoryMarshal.GetArrayDataReference(values);
        for (; index < count; index++)
        {
            WriteScalar(ref destination, ref Unsafe.Add(ref firstKey, (nint)(uint)index));
            WriteScalar(ref Unsafe.Add(ref destination, width), ref Unsafe.Add(ref firstValue, (nint)(uint)index));
            destination = ref Unsafe.Add(ref destination, 2 * width);
        }
        writer.CompleteRun((int)bytes);
        return true;
    }

    /// <summary>Validates all ranges before splitting raw payload bytes into exclusive key and value staging.</summary>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    /// <param name="reader">The reader positioned after the count.</param>
    /// <param name="keys">The key staging array.</param>
    /// <param name="values">The value staging array.</param>
    /// <param name="count">The number of pairs in the payload.</param>
    /// <returns>Whether the complete payload was consumed.</returns>
    private static bool TryReadInterleavedPayload<TKey, TValue>(ref LuminPackReader reader, TKey[] keys, TValue[] values, int count)
        where TKey : unmanaged where TValue : unmanaged
    {
        int width = Unsafe.SizeOf<TKey>();
        long bytes = (long)count * (2 * width);
        var input = reader.Buffer;
        int position = reader.Position;
        if ((uint)position > (uint)input.Length || bytes > input.Length - position || count > keys.Length || count > values.Length)
            return false;
        var keyBytes = MemoryMarshal.AsBytes(keys.AsSpan(0, count));
        var valueBytes = MemoryMarshal.AsBytes(values.AsSpan(0, count));
        if (keyBytes.Overlaps(valueBytes) || keyBytes.Overlaps(input) || valueBytes.Overlaps(input))
            return false;
        ref byte source = ref reader.CurrentReference;
        reader.Advance((int)bytes);
        ref byte firstKey = ref MemoryMarshal.GetReference(keyBytes);
        ref byte firstValue = ref MemoryMarshal.GetReference(valueBytes);
        int index = ReadInterleavedPrefix(ref source, ref firstKey, ref firstValue, count, width);
        for (; index < count; index++)
        {
            nint offset = (nint)index * width;
            LuminPackReader.Read(ref Unsafe.Add(ref source, offset * 2), out TKey key);
            LuminPackReader.Read(ref Unsafe.Add(ref source, offset * 2 + width), out TValue value);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref firstKey, offset), key);
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref firstValue, offset), value);
        }
        return true;
    }

    /// <summary>Separates complete vector groups using byte shuffles and joins the selected lanes in wire order.</summary>
    /// <param name="source">The validated interleaved payload.</param>
    /// <param name="keys">The first byte of the validated key destination.</param>
    /// <param name="values">The first byte of the validated value destination.</param>
    /// <param name="count">The number of pairs in the payload.</param>
    /// <param name="width">The common width of one key and one value.</param>
    /// <returns>The number of pairs read before the scalar tail.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | LuminPackJit.Optimized)]
    private static int ReadInterleavedPrefix(ref byte source, ref byte keys, ref byte values, int count, int width)
    {
        // Each 128-bit shuffle collects its key bytes into the low eight bytes and clears the high eight.
        ulong keyIndices = width switch
        {
            1 => 0x0E0C0A0806040200UL,
            2 => 0x0D0C090805040100UL,
            4 => 0x0B0A090803020100UL,
            _ => 0x0706050403020100UL
        };
        var keyMask = Vector128.Create(keyIndices, 0x8080808080808080UL).AsByte();
        var valueOffset = Vector128.Create((byte)width);
        var valueMask = Sse2.IsSupported ? Sse2.Add(keyMask, valueOffset) : AdvSimd.Add(keyMask, valueOffset);
        int index = 0;
        if (Avx512BW.IsSupported)
        {
            var keyHalves = Vector256.Create(keyMask, keyMask);
            var valueHalves = Vector256.Create(valueMask, valueMask);
            var wideKeys = Vector512.Create(keyHalves, keyHalves);
            var wideValues = Vector512.Create(valueHalves, valueHalves);
            var order = Vector512.Create(0UL, 2UL, 4UL, 6UL, 1UL, 3UL, 5UL, 7UL);
            int group = Vector512<byte>.Count / width;
            for (; index <= count - group; index += group)
            {
                nuint offset = (nuint)index * (nuint)width;
                var low = Vector512.LoadUnsafe(ref source, offset * 2);
                var high = Vector512.LoadUnsafe(ref source, offset * 2 + 64);
                var keyLanes = Avx512F.UnpackLow(Avx512BW.Shuffle(low, wideKeys).AsUInt64(), Avx512BW.Shuffle(high, wideKeys).AsUInt64());
                var valueLanes = Avx512F.UnpackLow(Avx512BW.Shuffle(low, wideValues).AsUInt64(), Avx512BW.Shuffle(high, wideValues).AsUInt64());
                Avx512F.PermuteVar8x64(keyLanes, order).AsByte().StoreUnsafe(ref keys, offset);
                Avx512F.PermuteVar8x64(valueLanes, order).AsByte().StoreUnsafe(ref values, offset);
            }
        }
        if (Avx2.IsSupported)
        {
            var wideKeys = Vector256.Create(keyMask, keyMask);
            var wideValues = Vector256.Create(valueMask, valueMask);
            int group = Vector256<byte>.Count / width;
            for (; index <= count - group; index += group)
            {
                nuint offset = (nuint)index * (nuint)width;
                var low = Vector256.LoadUnsafe(ref source, offset * 2);
                var high = Vector256.LoadUnsafe(ref source, offset * 2 + 32);
                JoinLanes(low, high, wideKeys).StoreUnsafe(ref keys, offset);
                JoinLanes(low, high, wideValues).StoreUnsafe(ref values, offset);
            }
        }
        int tailGroup = Vector128<byte>.Count / width;
        for (; index <= count - tailGroup; index += tailGroup)
        {
            nuint offset = (nuint)index * (nuint)width;
            var low = Vector128.LoadUnsafe(ref source, offset * 2);
            var high = Vector128.LoadUnsafe(ref source, offset * 2 + 16);
            SelectLanes(low, high, keyMask).StoreUnsafe(ref keys, offset);
            SelectLanes(low, high, valueMask).StoreUnsafe(ref values, offset);
        }
        return index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector256<byte> JoinLanes(Vector256<byte> low, Vector256<byte> high, Vector256<byte> mask)
        {
            var lanes = Avx2.UnpackLow(Avx2.Shuffle(low, mask).AsUInt64(), Avx2.Shuffle(high, mask).AsUInt64());
            return Avx2.Permute4x64(lanes, 0xD8).AsByte();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<byte> SelectLanes(Vector128<byte> low, Vector128<byte> high, Vector128<byte> mask)
            => Ssse3.IsSupported
                ? Sse2.UnpackLow(Ssse3.Shuffle(low, mask).AsUInt64(), Ssse3.Shuffle(high, mask).AsUInt64()).AsByte()
                : AdvSimd.Arm64.ZipLow(AdvSimd.Arm64.VectorTableLookup(low, mask).AsUInt64(),
                    AdvSimd.Arm64.VectorTableLookup(high, mask).AsUInt64()).AsByte();
    }

    /// <summary>Writes full vector groups from disjoint spans into an already reserved output window.</summary>
    /// <typeparam name="TKey">The raw key type.</typeparam>
    /// <typeparam name="TValue">The raw value type.</typeparam>
    /// <param name="payload">The output window.</param>
    /// <param name="keys">The key span.</param>
    /// <param name="values">The value span.</param>
    /// <returns>The number of pairs written, or zero if the input aliases the output.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteInterleavedPrefix<TKey, TValue>(Span<byte> payload, ReadOnlySpan<TKey> keys, ReadOnlySpan<TValue> values)
        where TKey : unmanaged where TValue : unmanaged
    {
        if (MemoryMarshal.AsBytes(keys).Overlaps(payload) || MemoryMarshal.AsBytes(values).Overlaps(payload))
            return 0;
        ref byte destination = ref MemoryMarshal.GetReference(payload);
        ref byte firstKey = ref Unsafe.As<TKey, byte>(ref MemoryMarshal.GetReference(keys));
        ref byte firstValue = ref Unsafe.As<TValue, byte>(ref MemoryMarshal.GetReference(values));
        return Unsafe.SizeOf<TKey>() switch
        {
            1 => WriteInterleavedAs<byte>(ref destination, ref firstKey, ref firstValue, keys.Length),
            2 => WriteInterleavedAs<ushort>(ref destination, ref firstKey, ref firstValue, keys.Length),
            4 => WriteInterleavedAs<uint>(ref destination, ref firstKey, ref firstValue, keys.Length),
            _ => WriteInterleavedAs<ulong>(ref destination, ref firstKey, ref firstValue, keys.Length)
        };
    }

    /// <summary>Reinterprets disjoint byte ranges as vectors of the chosen scalar lane width.</summary>
    /// <typeparam name="T">The unsigned storage lane.</typeparam>
    /// <param name="destination">The packed output bytes.</param>
    /// <param name="keys">The key bytes.</param>
    /// <param name="values">The value bytes.</param>
    /// <param name="count">The number of complete pairs.</param>
    /// <returns>The number of pairs written before the scalar tail.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int WriteInterleavedAs<T>(ref byte destination, ref byte keys, ref byte values, int count) where T : unmanaged
        => WriteInterleaved(ref Unsafe.As<byte, T>(ref destination), ref Unsafe.As<byte, T>(ref keys), ref Unsafe.As<byte, T>(ref values), count);

    /// <summary>Interleaves complete vectors of equal-width raw keys and values into a disjoint output range.</summary>
    /// <typeparam name="T">The unsigned storage lane.</typeparam>
    /// <param name="destination">The already reserved packed output.</param>
    /// <param name="keys">The first lane of the validated key span.</param>
    /// <param name="values">The first lane of the validated value span.</param>
    /// <param name="count">The number of complete pairs in the spans.</param>
    /// <returns>The number of pairs written before the scalar tail.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining | LuminPackJit.Optimized)]
    private static int WriteInterleaved<T>(ref T destination, ref T keys, ref T values, int count) where T : unmanaged
    {
        int index = 0;
        if (Avx512F.IsSupported && (Unsafe.SizeOf<T>() >= 4 || Avx512BW.IsSupported))
        {
            int width = Vector512<T>.Count;
            var first = Vector512.Create(0UL, 1UL, 8UL, 9UL, 2UL, 3UL, 10UL, 11UL);
            var second = Vector512.Create(4UL, 5UL, 12UL, 13UL, 6UL, 7UL, 14UL, 15UL);
            for (; index <= count - width; index += width)
            {
                nuint offset = (nuint)index;
                var key = Vector512.LoadUnsafe(ref keys, offset);
                var value = Vector512.LoadUnsafe(ref values, offset);
                var low = Unsafe.SizeOf<T>() switch
                {
                    1 => Avx512BW.UnpackLow(key.AsByte(), value.AsByte()).AsUInt64(),
                    2 => Avx512BW.UnpackLow(key.AsUInt16(), value.AsUInt16()).AsUInt64(),
                    4 => Avx512F.UnpackLow(key.AsUInt32(), value.AsUInt32()).AsUInt64(),
                    _ => Avx512F.UnpackLow(key.AsUInt64(), value.AsUInt64())
                };
                var high = Unsafe.SizeOf<T>() switch
                {
                    1 => Avx512BW.UnpackHigh(key.AsByte(), value.AsByte()).AsUInt64(),
                    2 => Avx512BW.UnpackHigh(key.AsUInt16(), value.AsUInt16()).AsUInt64(),
                    4 => Avx512F.UnpackHigh(key.AsUInt32(), value.AsUInt32()).AsUInt64(),
                    _ => Avx512F.UnpackHigh(key.AsUInt64(), value.AsUInt64())
                };
                Avx512F.PermuteVar8x64x2(low, first, high).As<ulong, T>().StoreUnsafe(ref destination, offset * 2);
                Avx512F.PermuteVar8x64x2(low, second, high).As<ulong, T>().StoreUnsafe(ref destination, offset * 2 + (nuint)width);
            }
        }
        if (Avx2.IsSupported)
        {
            int width = Vector256<T>.Count;
            for (; index <= count - width; index += width)
            {
                nuint offset = (nuint)index;
                var key = Vector256.LoadUnsafe(ref keys, offset);
                var value = Vector256.LoadUnsafe(ref values, offset);
                var low = Unsafe.SizeOf<T>() switch
                {
                    1 => Avx2.UnpackLow(key.AsByte(), value.AsByte()).AsUInt64(),
                    2 => Avx2.UnpackLow(key.AsUInt16(), value.AsUInt16()).AsUInt64(),
                    4 => Avx2.UnpackLow(key.AsUInt32(), value.AsUInt32()).AsUInt64(),
                    _ => Avx2.UnpackLow(key.AsUInt64(), value.AsUInt64())
                };
                var high = Unsafe.SizeOf<T>() switch
                {
                    1 => Avx2.UnpackHigh(key.AsByte(), value.AsByte()).AsUInt64(),
                    2 => Avx2.UnpackHigh(key.AsUInt16(), value.AsUInt16()).AsUInt64(),
                    4 => Avx2.UnpackHigh(key.AsUInt32(), value.AsUInt32()).AsUInt64(),
                    _ => Avx2.UnpackHigh(key.AsUInt64(), value.AsUInt64())
                };
                // Unpack works within each 128-bit lane. Join the corresponding halves in wire order.
                Vector256.StoreUnsafe(Avx2.Permute2x128(low, high, 0x20).As<ulong, T>(), ref destination, offset * 2);
                Vector256.StoreUnsafe(Avx2.Permute2x128(low, high, 0x31).As<ulong, T>(), ref destination, offset * 2 + (nuint)width);
            }
        }

        int tailWidth = Vector128<T>.Count;
        for (; index <= count - tailWidth; index += tailWidth)
        {
            nuint offset = (nuint)index;
            var key = Vector128.LoadUnsafe(ref keys, offset);
            var value = Vector128.LoadUnsafe(ref values, offset);
            var low = Unsafe.SizeOf<T>() switch
            {
                1 => (Sse2.IsSupported ? Sse2.UnpackLow(key.AsByte(), value.AsByte()) : AdvSimd.Arm64.ZipLow(key.AsByte(), value.AsByte())).As<byte, T>(),
                2 => (Sse2.IsSupported ? Sse2.UnpackLow(key.AsUInt16(), value.AsUInt16()) : AdvSimd.Arm64.ZipLow(key.AsUInt16(), value.AsUInt16())).As<ushort, T>(),
                4 => (Sse2.IsSupported ? Sse2.UnpackLow(key.AsUInt32(), value.AsUInt32()) : AdvSimd.Arm64.ZipLow(key.AsUInt32(), value.AsUInt32())).As<uint, T>(),
                _ => (Sse2.IsSupported ? Sse2.UnpackLow(key.AsUInt64(), value.AsUInt64()) : AdvSimd.Arm64.ZipLow(key.AsUInt64(), value.AsUInt64())).As<ulong, T>()
            };
            var high = Unsafe.SizeOf<T>() switch
            {
                1 => (Sse2.IsSupported ? Sse2.UnpackHigh(key.AsByte(), value.AsByte()) : AdvSimd.Arm64.ZipHigh(key.AsByte(), value.AsByte())).As<byte, T>(),
                2 => (Sse2.IsSupported ? Sse2.UnpackHigh(key.AsUInt16(), value.AsUInt16()) : AdvSimd.Arm64.ZipHigh(key.AsUInt16(), value.AsUInt16())).As<ushort, T>(),
                4 => (Sse2.IsSupported ? Sse2.UnpackHigh(key.AsUInt32(), value.AsUInt32()) : AdvSimd.Arm64.ZipHigh(key.AsUInt32(), value.AsUInt32())).As<uint, T>(),
                _ => (Sse2.IsSupported ? Sse2.UnpackHigh(key.AsUInt64(), value.AsUInt64()) : AdvSimd.Arm64.ZipHigh(key.AsUInt64(), value.AsUInt64())).As<ulong, T>()
            };
            Vector128.StoreUnsafe(low, ref destination, offset * 2);
            Vector128.StoreUnsafe(high, ref destination, offset * 2 + (nuint)tailWidth);
        }
        return index;
    }
}
#endif
