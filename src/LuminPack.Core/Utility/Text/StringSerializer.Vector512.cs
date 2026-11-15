#if NET8_0_OR_GREATER
using System.Runtime.CompilerServices;
using LuminPack.Core;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LuminPack.Utility;

public static partial class StringSerializer
{
    /// <summary>Consumes bounded homogeneous ASCII, two-byte or BMP three-byte UTF-8 runs using AVX-512BW.</summary>
    /// <param name="input">The first UTF-16 code unit.</param>
    /// <param name="output">The first UTF-8 destination byte.</param>
    /// <param name="inputLength">The complete input character count.</param>
    /// <param name="outputLength">The complete output capacity in bytes.</param>
    /// <param name="inputIndex">The number of consumed characters.</param>
    /// <param name="outputIndex">The number of written bytes.</param>
    /// <param name="lead">The original run's leading code unit.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeVector512Run(ref char input, ref byte output, int inputLength, int outputLength, ref int inputIndex, ref int outputIndex, uint lead)
    {
        if (lead < 0x80)
        {
            while ((inputLength - inputIndex) >= 32 && (outputLength - outputIndex) >= 32)
            {
                var chars = Vector512.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, inputIndex)));
                if (!Vector512.EqualsAll(chars & Vector512.Create((ushort)0xFF80), Vector512<ushort>.Zero))
                    break;
                Vector256.Narrow(chars.GetLower(), chars.GetUpper()).StoreUnsafe(ref Unsafe.Add(ref output, outputIndex));
                inputIndex += 32;
                outputIndex += 32;
            }
        }
        else if (lead < 0x800)
        {
            while ((inputLength - inputIndex) >= 32 && (outputLength - outputIndex) >= 64)
            {
                var chars = Vector512.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, inputIndex)));
                var valid = Vector512.GreaterThan(chars, Vector512.Create((ushort)127)) &
                    Vector512.LessThan(chars, Vector512.Create((ushort)2048));
                if (!Vector512.EqualsAll(valid, Vector512.Create(ushort.MaxValue)))
                    break;
                var pairs = (chars >> 6) | Vector512.Create((ushort)0xC0) |
                    (((chars & Vector512.Create((ushort)63)) | Vector512.Create((ushort)0x80)) << 8);
                pairs.AsByte().StoreUnsafe(ref Unsafe.Add(ref output, outputIndex));
                inputIndex += 32;
                outputIndex += 64;
            }
        }
        else if (IsBmp3(lead))
        {
            var first0 = Vector512.Create(BmpF0x2, BmpF0x2);
            var first1 = Vector512.Create(BmpF1x2, BmpF1x2);
            var first2 = Vector512.Create(BmpF2x2, BmpF2x2);
            var second0 = Vector512.Create(BmpS0x2, BmpS0x2);
            var second1 = Vector512.Create(BmpS1x2, BmpS1x2);
            var second2 = Vector512.Create(BmpS2x2, BmpS2x2);
            while ((inputLength - inputIndex) >= 32 && (outputLength - outputIndex) >= 96)
            {
                var chars = Vector512.LoadUnsafe(ref Unsafe.As<char, ushort>(ref Unsafe.Add(ref input, inputIndex)));
                var top = chars & Vector512.Create((ushort)0xF800);
                if (!Vector512.EqualsAll(Vector512.Equals(top, Vector512<ushort>.Zero) |
                    Vector512.Equals(top, Vector512.Create((ushort)0xD800)), Vector512<ushort>.Zero))
                    break;
                var leadBytes = (chars >> 12) | Vector512.Create((ushort)0xE0);
                var middleBytes = ((chars >> 6) & Vector512.Create((ushort)63)) | Vector512.Create((ushort)0x80);
                var lastBytes = (chars & Vector512.Create((ushort)63)) | Vector512.Create((ushort)0x80);
                var a = Avx512BW.PackUnsignedSaturate(leadBytes.AsInt16(), leadBytes.AsInt16());
                var b = Avx512BW.PackUnsignedSaturate(middleBytes.AsInt16(), middleBytes.AsInt16());
                var c = Avx512BW.PackUnsignedSaturate(lastBytes.AsInt16(), lastBytes.AsInt16());
                var first = Avx512BW.Shuffle(a, first0) | Avx512BW.Shuffle(b, first1) | Avx512BW.Shuffle(c, first2);
                var second = Avx512BW.Shuffle(a, second0) | Avx512BW.Shuffle(b, second1) | Avx512BW.Shuffle(c, second2);
                first.GetLower().GetLower().StoreUnsafe(ref Unsafe.Add(ref output, outputIndex));
                LuminPackWriter.Write(ref Unsafe.Add(ref output, outputIndex + 16), second.AsUInt64().GetElement(0));
                first.GetLower().GetUpper().StoreUnsafe(ref Unsafe.Add(ref output, outputIndex + 24));
                LuminPackWriter.Write(ref Unsafe.Add(ref output, outputIndex + 40), second.AsUInt64().GetElement(2));
                first.GetUpper().GetLower().StoreUnsafe(ref Unsafe.Add(ref output, outputIndex + 48));
                LuminPackWriter.Write(ref Unsafe.Add(ref output, outputIndex + 64), second.AsUInt64().GetElement(4));
                first.GetUpper().GetUpper().StoreUnsafe(ref Unsafe.Add(ref output, outputIndex + 72));
                LuminPackWriter.Write(ref Unsafe.Add(ref output, outputIndex + 88), second.AsUInt64().GetElement(6));
                inputIndex += 32;
                outputIndex += 96;
            }
        }
    }
}
#endif
