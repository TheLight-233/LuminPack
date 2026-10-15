using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Unicode;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

namespace LuminPackBenchmark;

/// <summary>Provides utf8 benchmark data functionality.</summary>
public enum Utf8BenchmarkData
{
    /// <summary>Specifies ascii.</summary>
    Ascii,
    /// <summary>Specifies chinese.</summary>
    Chinese,
    /// <summary>Specifies mixed.</summary>
    Mixed,
    /// <summary>Specifies emoji mixed.</summary>
    EmojiMixed,
}

/// <summary>Provides utf8 simd benchmark functionality.</summary>
[HideColumns("StdDev", "RatioSD", "Error")]
[MinColumn, MaxColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByParams)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 6, iterationCount: 12)]
[MemoryDiagnoser]
[DisassemblyDiagnoser(maxDepth: 5, printSource: true, printInstructionAddresses: true)]
public unsafe class Utf8SimdBenchmark
{
    private const int TargetChars = 16 * 1024;
    private const int RepeatCount = 256;

    private string _value = null!;
    private byte[] _destination = null!;

    /// <summary>Gets or sets the data.</summary>
    [Params(
        Utf8BenchmarkData.Ascii,
        Utf8BenchmarkData.Chinese,
        Utf8BenchmarkData.Mixed,
        Utf8BenchmarkData.EmojiMixed)]
    public Utf8BenchmarkData Data { get; set; }

    /// <summary>Updates the up used by the current operation.</summary>
    [GlobalSetup]
    public void Setup()
    {
        if (!Sse2.IsSupported)
            throw new PlatformNotSupportedException("This benchmark requires SSE2.");

        _value = Data switch
        {
            Utf8BenchmarkData.Ascii => BuildToLength(
                "LuminPack-HighPerformance-Serialization-0123456789-ABCDEFGHIJKLMNOPQRSTUVWXYZ-", TargetChars),

            Utf8BenchmarkData.Chinese => BuildToLength(
                "\u6781\u81F4\u6027\u80FD\u5E8F\u5217\u5316\u6846\u67B6\u5B57\u7B26\u4E32\u7F16\u7801\u89E3\u7801\u4E2D\u6587\u6570\u636E\u6D4B\u8BD5\u6E38\u620F\u5BA2\u6237\u7AEF\u8D44\u6E90\u52A0\u8F7D\u7F51\u7EDC\u540C\u6B65\u6027\u80FD\u4F18\u5316", TargetChars),

            Utf8BenchmarkData.Mixed => BuildToLength(
                "LuminPack\u6781\u81F4\u6027\u80FDSerialization\u5E8F\u5217\u5316UTF8\u7F16\u78011234567890\u6E38\u620FGame\u7F51\u7EDCNetwork\u6570\u636EData", TargetChars),

            Utf8BenchmarkData.EmojiMixed => BuildToLength(
                "LuminPack\uD83D\uDE80\uD83D\uDD25\uD83C\uDFAE\u6027\u80FD\u6D4B\u8BD5\uD83C\uDF1FUTF8\u4E2D\u6587\uD83D\uDE42Serialization\uD83D\uDCBB\u6570\u636E\uD83C\uDF0DGame\uD83C\uDFAF", TargetChars),

            _ => throw new ArgumentOutOfRangeException()
        };

        _destination = GC.AllocateUninitializedArray<byte>(
            Encoding.UTF8.GetMaxByteCount(_value.Length) + 32);

        // Correctness: custom output must be byte-identical to Encoding.UTF8.
        byte[] expected = Encoding.UTF8.GetBytes(_value);
        byte[] actual = new byte[expected.Length + 32];

        int written = LuminUtf8SimdV3.Encode(_value.AsSpan(), actual);

        if (written != expected.Length)
            throw new InvalidOperationException($"UTF8 byte count mismatch. Expected={expected.Length}, Actual={written}");

        if (!expected.AsSpan().SequenceEqual(actual.AsSpan(0, written)))
        {
            int badIndex = -1;
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] != actual[i])
                {
                    badIndex = i;
                    break;
                }
            }

            throw new InvalidOperationException($"UTF8 content mismatch at byte {badIndex}.");
        }
    }

    /// <summary>Measures encoding utf8 get bytes for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(Baseline = true, OperationsPerInvoke = RepeatCount)]
    public int EncodingUtf8_GetBytes()
    {
        int total = 0;
        string value = _value;
        byte[] destination = _destination;

        for (int i = 0; i < RepeatCount; i++)
            total += Encoding.UTF8.GetBytes(value.AsSpan(), destination);

        return total;
    }

    /// <summary>Measures utf8 from utf16 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int Utf8_FromUtf16()
    {
        int total = 0;
        string value = _value;
        byte[] destination = _destination;

        for (int i = 0; i < RepeatCount; i++)
        {
            OperationStatus status = Utf8.FromUtf16(
                value.AsSpan(),
                destination,
                out int charsRead,
                out int bytesWritten,
                replaceInvalidSequences: true,
                isFinalBlock: true);

            if (status != OperationStatus.Done || charsRead != value.Length)
                ThrowUtf8Failure(status, charsRead, value.Length);

            total += bytesWritten;
        }

        return total;
    }

    /// <summary>Measures lumin utf8 simd v3 for the configured benchmark input.</summary>
    /// <returns>The result of the operation.</returns>
    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int LuminUtf8_SIMD_V3()
    {
        int total = 0;
        string value = _value;
        byte[] destination = _destination;

        for (int i = 0; i < RepeatCount; i++)
            total += LuminUtf8SimdV3.Encode(value.AsSpan(), destination);

        return total;
    }

    /// <summary>Raises the exception for utf8 failure.</summary>
    /// <param name="status">The status used by this operation.</param>
    /// <param name="charsRead">The chars read used by this operation.</param>
    /// <param name="expectedChars">The expected chars used by this operation.</param>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowUtf8Failure(OperationStatus status, int charsRead, int expectedChars)
        => throw new InvalidOperationException(
            $"Utf8.FromUtf16 failed. Status={status}, chars={charsRead}/{expectedChars}");

    /// <summary>Measures build to length for the configured benchmark input.</summary>
    /// <param name="pattern">The pattern used by this operation.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
    private static string BuildToLength(string pattern, int length)
    {
        var builder = new StringBuilder(length + pattern.Length);

        while (builder.Length < length)
            builder.Append(pattern);

        if (builder.Length > length)
            builder.Length = length;

        if (builder.Length != 0 && char.IsHighSurrogate(builder[^1]))
            builder[^1] = 'A';

        return builder.ToString();
    }
}

/// <summary>Provides lumin utf8 simd v3 functionality.</summary>
internal static unsafe class LuminUtf8SimdV3
{
    // Mixed 4-lane kernel:
    // Expand each UTF-16 character into a four-byte lane:
    // ASCII: [a, -, -, -]
    // 3-byte BMP: [b0, b1, b2, -]
    //
    // Four lanes have only 16 ASCII/CJK combinations.
    // The pshufb table packs a 16-byte expanded vector into 4 to 12 bytes.
    // Fixed shuffle for the pure eight-character, three-byte BMP kernel.
    // Keep the verified full-SIMD path so the mixed lookup table does not slow uniform CJK input.
    private const byte Z = 0x80;

    private static readonly Vector128<byte> s_cjkFirstB0 = Vector128.Create(
        (byte)0, Z, Z, (byte)1, Z, Z, (byte)2, Z,
        Z, (byte)3, Z, Z, (byte)4, Z, Z, (byte)5);

    private static readonly Vector128<byte> s_cjkFirstB1 = Vector128.Create(
        Z, (byte)0, Z, Z, (byte)1, Z, Z, (byte)2,
        Z, Z, (byte)3, Z, Z, (byte)4, Z, Z);

    private static readonly Vector128<byte> s_cjkFirstB2 = Vector128.Create(
        Z, Z, (byte)0, Z, Z, (byte)1, Z, Z,
        (byte)2, Z, Z, (byte)3, Z, Z, (byte)4, Z);

    private static readonly Vector128<byte> s_cjkSecondB0 = Vector128.Create(
        Z, Z, (byte)6, Z, Z, (byte)7, Z, Z,
        Z, Z, Z, Z, Z, Z, Z, Z);

    private static readonly Vector128<byte> s_cjkSecondB1 = Vector128.Create(
        (byte)5, Z, Z, (byte)6, Z, Z, (byte)7, Z,
        Z, Z, Z, Z, Z, Z, Z, Z);

    private static readonly Vector128<byte> s_cjkSecondB2 = Vector128.Create(
        Z, (byte)5, Z, Z, (byte)6, Z, Z, (byte)7,
        Z, Z, Z, Z, Z, Z, Z, Z);

    private static readonly byte[] s_mixedShuffleTable =
    {
        0x00, 0x04, 0x08, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x08, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x04, 0x05, 0x06, 0x08, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x05, 0x06, 0x08, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x04, 0x08, 0x09, 0x0A, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x08, 0x09, 0x0A, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0C, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x04, 0x08, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x08, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x04, 0x05, 0x06, 0x08, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x05, 0x06, 0x08, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x04, 0x08, 0x09, 0x0A, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x08, 0x09, 0x0A, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
        0x00, 0x01, 0x02, 0x04, 0x05, 0x06, 0x08, 0x09, 0x0A, 0x0C, 0x0D, 0x0E, 0x80, 0x80, 0x80, 0x80,
    };

    private static readonly Vector128<ushort> s_top5Mask = Vector128.Create((ushort)0xF800);
    private static readonly Vector128<short> s_surrogatePrefix = Vector128.Create(unchecked((short)0xD800));
    private static readonly Vector128<ushort> s_mask3F16 = Vector128.Create((ushort)0x003F);
    private static readonly Vector128<ushort> s_lead3_16 = Vector128.Create((ushort)0x00E0);
    private static readonly Vector128<ushort> s_cont16 = Vector128.Create((ushort)0x0080);

    /// <summary>Measures encode for the configured benchmark input.</summary>
    /// <param name="source">The input data to process.</param>
    /// <param name="destination">The destination receiving the result.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static int Encode(ReadOnlySpan<char> source, Span<byte> destination)
    {
        fixed (char* pSource = source)
        fixed (byte* pDestination = destination)
        {
            char* src = pSource;
            char* srcEnd = pSource + source.Length;
            byte* dst = pDestination;

            while (src < srcEnd)
            {
                uint ch = *src;
                nint remaining = (nint)(srcEnd - src);

                // Use the high-throughput AVX2 path for long uniform ASCII input.
                if (ch <= 0x7F)
                {
                    if (Avx2.IsSupported)
                    {
                        if (remaining >= 32 && TryEncode32Ascii(ref src, ref dst))
                            continue;

                        if (remaining >= 16 && TryEncode16Ascii(ref src, ref dst))
                            continue;
                    }

                    // Try the mixed ASCII/CJK kernel when the pure ASCII run is too short.
                    // The benchmark reserves 32 additional padding bytes in the V3 destination,
                    // A 16-byte SIMD over-store is permitted while the logical output length stays exact.
                    if (Sse41.IsSupported &&
                        Ssse3.IsSupported &&
                        remaining >= 8 &&
                        TryEncode8AsciiOrThreeByteBmpMixed(ref src, ref dst))
                    {
                        continue;
                    }

                    EncodeShortAsciiRun(ref src, srcEnd, ref dst);
                    continue;
                }

                // A long uniform three-byte BMP/CJK sequence.
                if (ch >= 0x0800 && (ch < 0xD800 || ch > 0xDFFF))
                {
                    if (Ssse3.IsSupported)
                    {
                        if (remaining >= 16 &&
                            TryEncode16ThreeByteBmp(ref src, ref dst))
                        {
                            continue;
                        }

                        if (remaining >= 8 &&
                            TryEncode8ThreeByteBmp(ref src, ref dst))
                        {
                            continue;
                        }
                    }

                    // Use the mixed kernel for short CJK runs interleaved with ASCII.
                    if (Sse41.IsSupported &&
                        Ssse3.IsSupported &&
                        remaining >= 8 &&
                        TryEncode8AsciiOrThreeByteBmpMixed(ref src, ref dst))
                    {
                        continue;
                    }

                    EncodeShortThreeByteRun(ref src, srcEnd, ref dst);
                    continue;
                }

                if (ch <= 0x07FF)
                {
                    EncodeTwoByteRun(ref src, srcEnd, ref dst);
                    continue;
                }

                EncodeSurrogateOrInvalid(ref src, srcEnd, ref dst);
            }

            return checked((int)(dst - pDestination));
        }
    }

    // -------------------------
    // ASCII
    // -------------------------

    /// <summary>Attempts to encode32 ascii and reports whether it succeeded.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEncode32Ascii(ref char* src, ref byte* dst)
    {
        Vector256<ushort> chars0 = Unsafe.ReadUnaligned<Vector256<ushort>>(src);
        Vector256<ushort> chars1 = Unsafe.ReadUnaligned<Vector256<ushort>>(src + 16);

        Vector256<ushort> any = Avx2.Or(chars0, chars1);
        Vector256<ushort> nonAsciiBits = Avx2.And(any, Vector256.Create((ushort)0xFF80));

        Vector256<short> zero = Avx2.CompareEqual(
            nonAsciiBits.AsInt16(),
            Vector256<short>.Zero);

        if (Avx2.MoveMask(zero.AsByte()) != -1)
            return false;

        Store16Ascii(chars0, dst);
        Store16Ascii(chars1, dst + 16);

        src += 32;
        dst += 32;
        return true;
    }

    /// <summary>Attempts to encode16 ascii and reports whether it succeeded.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEncode16Ascii(ref char* src, ref byte* dst)
    {
        Vector256<ushort> chars = Unsafe.ReadUnaligned<Vector256<ushort>>(src);

        Vector256<ushort> nonAsciiBits = Avx2.And(
            chars,
            Vector256.Create((ushort)0xFF80));

        Vector256<short> zero = Avx2.CompareEqual(
            nonAsciiBits.AsInt16(),
            Vector256<short>.Zero);

        if (Avx2.MoveMask(zero.AsByte()) != -1)
            return false;

        Store16Ascii(chars, dst);

        src += 16;
        dst += 16;
        return true;
    }

    /// <summary>Measures store16 ascii for the configured benchmark input.</summary>
    /// <param name="chars">The chars used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store16Ascii(Vector256<ushort> chars, byte* dst)
    {
        Vector128<byte> packed = Sse2.PackUnsignedSaturate(
            chars.GetLower().AsInt16(),
            chars.GetUpper().AsInt16());

        Sse2.Store(dst, packed);
    }

    // -------------------------
    // Pure 3-byte BMP / CJK
    // -------------------------

    /// <summary>Attempts to encode16 three byte bmp and reports whether it succeeded.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEncode16ThreeByteBmp(ref char* src, ref byte* dst)
    {
        Vector128<ushort> chars0 = Sse2.LoadVector128((ushort*)src);
        Vector128<ushort> chars1 = Sse2.LoadVector128((ushort*)(src + 8));

        if (!AllEightAreThreeByteBmp(chars0) ||
            !AllEightAreThreeByteBmp(chars1))
        {
            return false;
        }

        Encode8ThreeByteBmpUnchecked(chars0, dst);
        Encode8ThreeByteBmpUnchecked(chars1, dst + 24);

        src += 16;
        dst += 48;
        return true;
    }

    /// <summary>Attempts to encode8 three byte bmp and reports whether it succeeded.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEncode8ThreeByteBmp(ref char* src, ref byte* dst)
    {
        Vector128<ushort> chars = Sse2.LoadVector128((ushort*)src);

        if (!AllEightAreThreeByteBmp(chars))
            return false;

        Encode8ThreeByteBmpUnchecked(chars, dst);

        src += 8;
        dst += 24;
        return true;
    }

    /// <summary>Measures all eight are three byte bmp for the configured benchmark input.</summary>
    /// <param name="chars">The chars used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AllEightAreThreeByteBmp(Vector128<ushort> chars)
    {
        Vector128<ushort> topBits = Sse2.And(chars, s_top5Mask);

        Vector128<short> below0800 = Sse2.CompareEqual(
            topBits.AsInt16(),
            Vector128<short>.Zero);

        if (Sse2.MoveMask(below0800.AsByte()) != 0)
            return false;

        Vector128<short> surrogate = Sse2.CompareEqual(
            topBits.AsInt16(),
            s_surrogatePrefix);

        return Sse2.MoveMask(surrogate.AsByte()) == 0;
    }

    /// <summary>Measures encode8 three byte bmp unchecked for the configured benchmark input.</summary>
    /// <param name="chars">The chars used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Encode8ThreeByteBmpUnchecked(Vector128<ushort> chars, byte* dst)
    {
        Vector128<ushort> b0U16 = Sse2.Or(
            Sse2.ShiftRightLogical(chars, 12),
            s_lead3_16);

        Vector128<ushort> b1U16 = Sse2.Or(
            Sse2.And(Sse2.ShiftRightLogical(chars, 6), s_mask3F16),
            s_cont16);

        Vector128<ushort> b2U16 = Sse2.Or(
            Sse2.And(chars, s_mask3F16),
            s_cont16);

        Vector128<byte> b0 = Sse2.PackUnsignedSaturate(
            b0U16.AsInt16(), b0U16.AsInt16());

        Vector128<byte> b1 = Sse2.PackUnsignedSaturate(
            b1U16.AsInt16(), b1U16.AsInt16());

        Vector128<byte> b2 = Sse2.PackUnsignedSaturate(
            b2U16.AsInt16(), b2U16.AsInt16());

        Vector128<byte> first = Sse2.Or(
            Sse2.Or(
                Ssse3.Shuffle(b0, s_cjkFirstB0),
                Ssse3.Shuffle(b1, s_cjkFirstB1)),
            Ssse3.Shuffle(b2, s_cjkFirstB2));

        Vector128<byte> second = Sse2.Or(
            Sse2.Or(
                Ssse3.Shuffle(b0, s_cjkSecondB0),
                Ssse3.Shuffle(b1, s_cjkSecondB1)),
            Ssse3.Shuffle(b2, s_cjkSecondB2));

        Sse2.Store(dst, first);
        Unsafe.WriteUnaligned(dst + 16, second.AsUInt64().GetElement(0));
    }

    // -------------------------
    // ASCII + 3-byte BMP mixed SIMD
    // -------------------------

    /// <summary>Attempts to encode8 ascii or three byte bmp mixed and reports whether it succeeded.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="dst">The destination receiving the result.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryEncode8AsciiOrThreeByteBmpMixed(
        ref char* src,
        ref byte* dst)
    {
        if (!TryPackFourAsciiOrThreeByteBmp(
                src,
                out Vector128<byte> out0,
                out int len0))
        {
            return false;
        }

        if (!TryPackFourAsciiOrThreeByteBmp(
                src + 4,
                out Vector128<byte> out1,
                out int len1))
        {
            return false;
        }

        // Allow an over-store: the second write starts at the logical end of the first,
        // Overwrite the unused tail of the previous store.
        Sse2.Store(dst, out0);
        Sse2.Store(dst + len0, out1);

        src += 8;
        dst += len0 + len1;
        return true;
    }

    /// <summary>Attempts to pack four ascii or three byte bmp and reports whether it succeeded.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="output">Receives the output produced by the operation.</param>
    /// <param name="bytesWritten">Receives the bytes written produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryPackFourAsciiOrThreeByteBmp(
        char* src,
        out Vector128<byte> output,
        out int bytesWritten)
    {
        ulong raw = Unsafe.ReadUnaligned<ulong>(src);
        Vector128<ushort> chars16 = Vector128.CreateScalarUnsafe(raw).AsUInt16();

        // pmovzxwd: 4 x ushort -> 4 x int
        Vector128<int> chars = Sse41.ConvertToVector128Int32(chars16);

        Vector128<int> asciiMask = Sse2.CompareGreaterThan(
            Vector128.Create(0x80),
            chars);

        Vector128<int> below0800 = Sse2.CompareGreaterThan(
            Vector128.Create(0x800),
            chars);

        // This kernel excludes U+0080 through U+07FF.
        Vector128<int> twoByteMask = Sse2.AndNot(
            asciiMask,
            below0800);

        if (Sse.MoveMask(twoByteMask.AsSingle()) != 0)
        {
            output = default;
            bytesWritten = 0;
            return false;
        }

        Vector128<int> surrogateMask = Sse2.CompareEqual(
            Sse2.And(chars, Vector128.Create(0xF800)),
            Vector128.Create(0xD800));

        if (Sse.MoveMask(surrogateMask.AsSingle()) != 0)
        {
            output = default;
            bytesWritten = 0;
            return false;
        }

        int asciiBits = Sse.MoveMask(asciiMask.AsSingle()) & 0xF;
        int threeByteBits = (~asciiBits) & 0xF;

        Vector128<uint> u = chars.AsUInt32();

        Vector128<uint> b0 = Sse2.Or(
            Sse2.ShiftRightLogical(u, 12),
            Vector128.Create(0xE0u));

        Vector128<uint> b1 = Sse2.Or(
            Sse2.And(
                Sse2.ShiftRightLogical(u, 6),
                Vector128.Create(0x3Fu)),
            Vector128.Create(0x80u));

        Vector128<uint> b2 = Sse2.Or(
            Sse2.And(u, Vector128.Create(0x3Fu)),
            Vector128.Create(0x80u));

        Vector128<uint> packed3 = Sse2.Or(
            Sse2.Or(
                b0,
                Sse2.ShiftLeftLogical(b1, 8)),
            Sse2.ShiftLeftLogical(b2, 16));

        // Keep the low byte of each character in ASCII lanes.
        // A three-byte lane uses [b0,b1,b2,0].
        Vector128<uint> selected = Sse2.Or(
            Sse2.And(asciiMask.AsUInt32(), u),
            Sse2.AndNot(asciiMask.AsUInt32(), packed3));

        Vector128<byte> shuffle = LoadMixedShuffle(threeByteBits);
        output = Ssse3.Shuffle(selected.AsByte(), shuffle);

        bytesWritten = 4 + (BitOperations.PopCount((uint)threeByteBits) << 1);
        return true;
    }

    /// <summary>Measures load mixed shuffle for the configured benchmark input.</summary>
    /// <param name="mask">The mask used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> LoadMixedShuffle(int mask)
    {
        ref byte table = ref MemoryMarshal.GetArrayDataReference(s_mixedShuffleTable);
        return Unsafe.ReadUnaligned<Vector128<byte>>(
            ref Unsafe.Add(ref table, mask << 4));
    }

    // -------------------------
    // Scalar short / uncommon paths
    // -------------------------

    /// <summary>Measures encode short ascii run for the configured benchmark input.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcEnd">The src end used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeShortAsciiRun(ref char* src, char* srcEnd, ref byte* dst)
    {
        do
        {
            *dst++ = (byte)*src++;
        }
        while (src < srcEnd && *src <= 0x7F);
    }

    /// <summary>Measures encode short three byte run for the configured benchmark input.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcEnd">The src end used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeShortThreeByteRun(ref char* src, char* srcEnd, ref byte* dst)
    {
        do
        {
            uint ch = *src;

            if (ch < 0x0800 || (ch >= 0xD800 && ch <= 0xDFFF))
                return;

            dst[0] = (byte)(0xE0 | (ch >> 12));
            dst[1] = (byte)(0x80 | ((ch >> 6) & 0x3F));
            dst[2] = (byte)(0x80 | (ch & 0x3F));

            dst += 3;
            src++;
        }
        while (src < srcEnd);
    }

    /// <summary>Measures encode two byte run for the configured benchmark input.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcEnd">The src end used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeTwoByteRun(ref char* src, char* srcEnd, ref byte* dst)
    {
        do
        {
            uint ch = *src;

            if (ch < 0x80 || ch > 0x07FF)
                return;

            dst[0] = (byte)(0xC0 | (ch >> 6));
            dst[1] = (byte)(0x80 | (ch & 0x3F));

            dst += 2;
            src++;
        }
        while (src < srcEnd);
    }

    /// <summary>Measures encode surrogate or invalid for the configured benchmark input.</summary>
    /// <param name="src">The input data to process.</param>
    /// <param name="srcEnd">The src end used by this operation.</param>
    /// <param name="dst">The destination receiving the result.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EncodeSurrogateOrInvalid(ref char* src, char* srcEnd, ref byte* dst)
    {
        uint ch = *src;

        if (ch <= 0xDBFF && src + 1 < srcEnd)
        {
            uint low = src[1];

            if (low is >= 0xDC00 and <= 0xDFFF)
            {
                uint scalar =
                    0x10000u +
                    ((ch - 0xD800u) << 10) +
                    (low - 0xDC00u);

                dst[0] = (byte)(0xF0 | (scalar >> 18));
                dst[1] = (byte)(0x80 | ((scalar >> 12) & 0x3F));
                dst[2] = (byte)(0x80 | ((scalar >> 6) & 0x3F));
                dst[3] = (byte)(0x80 | (scalar & 0x3F));

                dst += 4;
                src += 2;
                return;
            }
        }

        dst[0] = 0xEF;
        dst[1] = 0xBF;
        dst[2] = 0xBD;

        dst += 3;
        src++;
    }
}
