#if NET8_0_OR_GREATER
using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LuminPack.Internal;

// An exact-store prefix kernel. Other code points and all tails use the platform transcoder.
internal static unsafe class Utf8BmpTranscoder
{
    private const byte Z = 0x80;
    private static readonly Vector128<byte> First0 = Vector128.Create((byte)0, Z, Z, (byte)1, Z, Z, (byte)2, Z, Z, (byte)3, Z, Z, (byte)4, Z, Z, (byte)5);
    private static readonly Vector128<byte> First1 = Vector128.Create(Z, (byte)0, Z, Z, (byte)1, Z, Z, (byte)2, Z, Z, (byte)3, Z, Z, (byte)4, Z, Z);
    private static readonly Vector128<byte> First2 = Vector128.Create(Z, Z, (byte)0, Z, Z, (byte)1, Z, Z, (byte)2, Z, Z, (byte)3, Z, Z, (byte)4, Z);
    private static readonly Vector128<byte> Second0 = Vector128.Create(Z, Z, (byte)6, Z, Z, (byte)7, Z, Z, Z, Z, Z, Z, Z, Z, Z, Z);
    private static readonly Vector128<byte> Second1 = Vector128.Create((byte)5, Z, Z, (byte)6, Z, Z, (byte)7, Z, Z, Z, Z, Z, Z, Z, Z, Z);
    private static readonly Vector128<byte> Second2 = Vector128.Create(Z, (byte)5, Z, Z, (byte)6, Z, Z, (byte)7, Z, Z, Z, Z, Z, Z, Z, Z);

    internal static void TranscodePrefix(ReadOnlySpan<char> source, Span<byte> destination, out int charsRead, out int bytesWritten)
    {
        charsRead = 0;
        bytesWritten = 0;
        fixed (char* input = source)
        fixed (byte* output = destination)
        {
            while (source.Length - charsRead >= 8 && destination.Length - bytesWritten >= 24)
            {
                var chars = Unsafe.ReadUnaligned<Vector128<ushort>>(input + charsRead);
                var top = Sse2.And(chars, Vector128.Create((ushort)0xF800));
                if (Sse2.MoveMask(Sse2.CompareEqual(top.AsInt16(), Vector128<short>.Zero).AsByte()) != 0 ||
                    Sse2.MoveMask(Sse2.CompareEqual(top.AsInt16(), Vector128.Create(unchecked((short)0xD800))).AsByte()) != 0)
                    break;
                var lead = Sse2.Or(Sse2.ShiftRightLogical(chars, 12), Vector128.Create((ushort)0xE0));
                var middle = Sse2.Or(Sse2.And(Sse2.ShiftRightLogical(chars, 6), Vector128.Create((ushort)0x3F)), Vector128.Create((ushort)0x80));
                var last = Sse2.Or(Sse2.And(chars, Vector128.Create((ushort)0x3F)), Vector128.Create((ushort)0x80));
                var b0 = Sse2.PackUnsignedSaturate(lead.AsInt16(), lead.AsInt16());
                var b1 = Sse2.PackUnsignedSaturate(middle.AsInt16(), middle.AsInt16());
                var b2 = Sse2.PackUnsignedSaturate(last.AsInt16(), last.AsInt16());
                var first = Sse2.Or(Sse2.Or(Ssse3.Shuffle(b0, First0), Ssse3.Shuffle(b1, First1)), Ssse3.Shuffle(b2, First2));
                var second = Sse2.Or(Sse2.Or(Ssse3.Shuffle(b0, Second0), Ssse3.Shuffle(b1, Second1)), Ssse3.Shuffle(b2, Second2));
                Unsafe.WriteUnaligned(output + bytesWritten, first);
                Unsafe.WriteUnaligned(output + bytesWritten + 16, second.AsUInt64().GetElement(0));
                charsRead += 8;
                bytesWritten += 24;
            }
        }
    }
}
#endif
