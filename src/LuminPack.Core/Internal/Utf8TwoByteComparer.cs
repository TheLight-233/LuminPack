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
    /// <summary>Compares canonical two-byte vectors and delegates mixed tails to the validating decoder.</summary>
    /// <param name="bytes">The complete, bounds-validated UTF-8 payload.</param>
    /// <param name="text">The existing text, selected on an x64 SSE2 runtime with a two-byte first character.</param>
    /// <returns>Whether the valid UTF-8 payload equals the existing text.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static bool Matches(ReadOnlySpan<byte> bytes, ReadOnlySpan<char> text)
    {
        int byteOffset = 0, charOffset = 0;
        ref byte source = ref MemoryMarshal.GetReference(bytes);
        ref ushort characters = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(text));
        if (Avx2.IsSupported)
        {
            while (text.Length - charOffset >= 16 && bytes.Length - byteOffset >= 32)
            {
                var value = Vector256.LoadUnsafe(ref characters, (nuint)(uint)charOffset);
                var valid = Avx2.And(Avx2.CompareGreaterThan(value.AsInt16(), Vector256.Create((short)0x7F)),
                    Avx2.CompareGreaterThan(Vector256.Create((short)0x800), value.AsInt16()));
                if (Avx2.MoveMask(valid.AsByte()) != -1)
                    break;
                // Each UTF-16 lane becomes its little-endian UTF-8 byte pair without packing or shuffling.
                var expected = (value >> 6) | ((value & Vector256.Create((ushort)0x3F)) << 8) | Vector256.Create((ushort)0x80C0);
                if (!Vector256.EqualsAll(expected.AsByte(), Vector256.LoadUnsafe(ref source, (nuint)(uint)byteOffset)))
                    return false;
                byteOffset += 32;
                charOffset += 16;
            }
        }
        while (text.Length - charOffset >= 8 && bytes.Length - byteOffset >= 16)
        {
            var value = Vector128.LoadUnsafe(ref characters, (nuint)(uint)charOffset);
            var valid = Sse2.And(Sse2.CompareGreaterThan(value.AsInt16(), Vector128.Create((short)0x7F)),
                Sse2.CompareGreaterThan(Vector128.Create((short)0x800), value.AsInt16()));
            if (Sse2.MoveMask(valid.AsByte()) != 0xFFFF)
                return StringSerializer.MatchesUtf8Chunks(bytes[byteOffset..], text[charOffset..]);

            var expected = (value >> 6) | ((value & Vector128.Create((ushort)0x3F)) << 8) | Vector128.Create((ushort)0x80C0);
            if (!Vector128.EqualsAll(expected.AsByte(), Vector128.LoadUnsafe(ref source, (nuint)(uint)byteOffset)))
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
