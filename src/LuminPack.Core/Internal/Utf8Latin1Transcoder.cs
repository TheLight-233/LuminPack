#if NET8_0_OR_GREATER
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LuminPack.Internal;

/// <summary>Encodes mixed ASCII and Latin1 prefixes as standard UTF-8 with exact bounded stores.</summary>
internal static unsafe class Utf8Latin1Transcoder
{
    private static readonly Vector128<byte>[] ShuffleMasks = CreateShuffleMasks();

    /// <summary>Builds first-eight and last-eight byte selectors for each eight-character classification.</summary>
    /// <returns>One bounded selector per possible non-ASCII mask.</returns>
    private static Vector128<byte>[] CreateShuffleMasks()
    {
        var masks = new Vector128<byte>[256];
        Span<byte> indices = stackalloc byte[16];
        Span<byte> selected = stackalloc byte[16];
        for (int mask = 0; mask < masks.Length; mask++)
        {
            int count = 0;
            for (int index = 0; index < 8; index++)
            {
                indices[count++] = (byte)(index * 2);
                if ((mask & (1 << index)) != 0) indices[count++] = (byte)(index * 2 + 1);
            }
            indices[..8].CopyTo(selected);
            indices.Slice(count - 8, 8).CopyTo(selected[8..]);
            masks[mask] = Vector128.LoadUnsafe(ref selected[0]);
        }
        return masks;
    }

    /// <summary>Encodes a Latin1 prefix, leaving other Unicode and partial sequences to the original decoder.</summary>
    /// <param name="source">UTF-16 input beginning with a non-ASCII Latin1 character.</param>
    /// <param name="destination">The complete writable destination boundary.</param>
    /// <param name="charsRead">Receives the fully consumed UTF-16 prefix length.</param>
    /// <param name="bytesWritten">Receives the exact produced UTF-8 length.</param>
    internal static void EncodePrefix(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten)
    {
        charsRead = 0;
        bytesWritten = 0;
        fixed (char* start = source)
        fixed (byte* target = destination)
        {
            if (Avx512BW.IsSupported)
                while (source.Length - charsRead >= 32 && destination.Length - bytesWritten >= 64)
                {
                    var chars = Vector512.Load((ushort*)(start + charsRead));
                    if (!Vector512.EqualsAll(chars & Vector512.Create((ushort)0xFF00), Vector512<ushort>.Zero)) break;
                    uint mask = (uint)Vector512.ExtractMostSignificantBits(chars << 8);
                    var ascii = Vector512.Equals(chars & Vector512.Create((ushort)0x80), Vector512<ushort>.Zero);
                    var pairs = Vector512.ConditionalSelect(ascii, chars,
                        (chars >> 6) | Vector512.Create((ushort)0xC0) | (((chars & Vector512.Create((ushort)63)) | Vector512.Create((ushort)0x80)) << 8));
                    var controls = Vector512.Create(Vector256.Create(ShuffleMasks[mask & 255], ShuffleMasks[(mask >> 8) & 255]),
                        Vector256.Create(ShuffleMasks[(mask >> 16) & 255], ShuffleMasks[mask >> 24]));
                    var packed = Avx512BW.Shuffle(pairs.AsByte(), controls);
                    StoreLane(target, ref bytesWritten, packed.GetLower().GetLower(), (byte)mask);
                    StoreLane(target, ref bytesWritten, packed.GetLower().GetUpper(), (byte)(mask >> 8));
                    StoreLane(target, ref bytesWritten, packed.GetUpper().GetLower(), (byte)(mask >> 16));
                    StoreLane(target, ref bytesWritten, packed.GetUpper().GetUpper(), (byte)(mask >> 24));
                    charsRead += 32;
                }
            if (Avx2.IsSupported)
                while (source.Length - charsRead >= 16 && destination.Length - bytesWritten >= 32)
                {
                    var chars = Vector256.Load((ushort*)(start + charsRead));
                    if (!Vector256.EqualsAll(chars & Vector256.Create((ushort)0xFF00), Vector256<ushort>.Zero)) break;
                    uint mask = Vector256.ExtractMostSignificantBits(chars << 8);
                    var ascii = Vector256.Equals(chars & Vector256.Create((ushort)0x80), Vector256<ushort>.Zero);
                    var pairs = Vector256.ConditionalSelect(ascii, chars,
                        (chars >> 6) | Vector256.Create((ushort)0xC0) | (((chars & Vector256.Create((ushort)63)) | Vector256.Create((ushort)0x80)) << 8));
                    var packed = Avx2.Shuffle(pairs.AsByte(), Vector256.Create(ShuffleMasks[mask & 255], ShuffleMasks[mask >> 8]));
                    StoreLane(target, ref bytesWritten, packed.GetLower(), (byte)mask);
                    StoreLane(target, ref bytesWritten, packed.GetUpper(), (byte)(mask >> 8));
                    charsRead += 16;
                }
            if (Ssse3.IsSupported)
                while (source.Length - charsRead >= 8 && destination.Length - bytesWritten >= 16)
                {
                    var chars = Vector128.Load((ushort*)(start + charsRead));
                    if (!Vector128.EqualsAll(chars & Vector128.Create((ushort)0xFF00), Vector128<ushort>.Zero)) break;
                    uint mask = Vector128.ExtractMostSignificantBits(chars << 8);
                    var ascii = Vector128.Equals(chars & Vector128.Create((ushort)0x80), Vector128<ushort>.Zero);
                    var pairs = Vector128.ConditionalSelect(ascii, chars,
                        (chars >> 6) | Vector128.Create((ushort)0xC0) | (((chars & Vector128.Create((ushort)63)) | Vector128.Create((ushort)0x80)) << 8));
                    StoreLane(target, ref bytesWritten, Ssse3.Shuffle(pairs.AsByte(), ShuffleMasks[mask]), (byte)mask);
                    charsRead += 8;
                }
            while (charsRead < source.Length)
            {
                uint value = start[charsRead];
                if (value > 255) break;
                int length = value < 128 ? 1 : 2;
                if (length > destination.Length - bytesWritten) break;
                target[bytesWritten++] = (byte)(value < 128 ? value : 0xC0 | value >> 6);
                if (length == 2) target[bytesWritten++] = (byte)(0x80 | value & 63);
                charsRead++;
            }
        }
    }

    /// <summary>Publishes eight to sixteen compacted bytes using two exact, possibly overlapping stores.</summary>
    /// <param name="destination">The validated destination start.</param>
    /// <param name="offset">The current produced length.</param>
    /// <param name="packed">The first eight bytes followed by the last eight bytes.</param>
    /// <param name="mask">The non-ASCII character mask.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void StoreLane(byte* destination, ref int offset, Vector128<byte> packed, byte mask)
    {
        int length = 8 + BitOperations.PopCount((uint)mask);
        Unsafe.WriteUnaligned(destination + offset, packed.AsUInt64().GetElement(0));
        Unsafe.WriteUnaligned(destination + offset + length - 8, packed.AsUInt64().GetElement(1));
        offset += length;
    }
}
#endif
