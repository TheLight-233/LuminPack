#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using LuminPack.Utility;

namespace LuminPack.Internal;

/// <summary>Compares two-byte UTF-8 runs with existing UTF-16 storage without a decoded buffer.</summary>
internal static class Utf8TwoByteComparer
{
    // Keep mask initialization off the ordinary ASCII and transcoding paths.
    private static readonly Vector128<byte> PairMask = Vector128.Create((byte)0, 8, 1, 9, 2, 10, 3, 11, 4, 12, 5, 13, 6, 14, 7, 15);

    /// <summary>Compares canonical two-byte vectors and delegates mixed tails to the validating decoder.</summary>
    /// <param name="bytes">The complete, bounds-validated UTF-8 payload.</param>
    /// <param name="text">The existing text, selected on an x64 SSSE3 runtime with a two-byte first character.</param>
    /// <returns>Whether the valid UTF-8 payload equals the existing text.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Matches(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text)
    {
        int byteOffset = 0, charOffset = 0;
        ref byte source = ref MemoryMarshal.GetReference(bytes);
        ref ushort characters = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(text));
        while (text.Length - charOffset >= 8 && bytes.Length - byteOffset >= 16)
        {
            var value = Vector128.LoadUnsafe(ref characters, (nuint)(uint)charOffset);
            var valid = Sse2.And(
                Sse2.CompareEqual(Sse2.SubtractSaturate(Vector128.Create((ushort)0x80), value), Vector128<ushort>.Zero),
                Sse2.CompareEqual(Sse2.SubtractSaturate(value, Vector128.Create((ushort)0x7FF)), Vector128<ushort>.Zero));
            if (Sse2.MoveMask(valid.AsByte()) != 0xFFFF)
                return StringSerializer.MatchesUtf8Chunks(bytes[byteOffset..], text[charOffset..]);

            var lead = Sse2.Or(Sse2.ShiftRightLogical(value, 6), Vector128.Create((ushort)0xC0));
            var tail = Sse2.Or(Sse2.And(value, Vector128.Create((ushort)0x3F)), Vector128.Create((ushort)0x80));
            var expected = Ssse3.Shuffle(Sse2.PackUnsignedSaturate(lead.AsInt16(), tail.AsInt16()), PairMask);
            if (!Vector128.EqualsAll(expected, Vector128.LoadUnsafe(ref source, (nuint)(uint)byteOffset)))
                return false;
            byteOffset += 16;
            charOffset += 8;
        }
        while (charOffset < text.Length)
        {
            uint value = text[charOffset];
            if (value is < 0x80 or > 0x7FF)
                return StringSerializer.MatchesUtf8Chunks(bytes[byteOffset..], text[charOffset..]);
            if (bytes.Length - byteOffset < 2 || bytes[byteOffset] != (byte)(0xC0 | value >> 6) ||
                bytes[byteOffset + 1] != (byte)(0x80 | value & 0x3F))
                return false;
            byteOffset += 2;
            charOffset++;
        }
        return byteOffset == bytes.Length;
    }
}
#endif
