using System.Text;
using System.Text.Unicode;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks every Latin1 compaction mask, SIMD width and bounded fallback against the platform encoder.</summary>
public class Latin1VectorTranscoderTest
{
    /// <summary>Checks every sampled lane at UTF-8 category and surrogate boundaries against the platform contract.</summary>
    [Fact]
    public void DispatchSamplePreservesUnicodeBoundaries()
    {
        foreach (char lead in new[] { '\u07FF', '\u4E2D' })
            foreach (char boundary in new[] { '\0', '\u007F', '\u0080', '\u00FF', '\u0100', '\u07FF', '\u0800', '\uD7FF', '\uD800', '\uDFFF', '\uE000', '\uFFFF' })
                for (int lane = 0; lane <= 16; lane++)
                    foreach (bool replace in new[] { false, true })
                        foreach (int capacity in new[] { 0, 31, 32, 63, 96 })
                        {
                            char[] text = Enumerable.Repeat(lead, 32).ToArray();
                            text[lane] = boundary;
                            byte[] expected = new byte[capacity];
                            byte[] actual = new byte[capacity];
                            var status = Utf8.FromUtf16(text, expected, out int read, out int written, replace, false);
                            var result = StringSerializer.Serialize<LuminPack.Option.LuminPackUtf8LengthOptions>(text, actual, out int actualRead, out int actualWritten, replace, false);
                            Assert.Equal(status, result);
                            Assert.Equal(read, actualRead);
                            Assert.Equal(written, actualWritten);
                            Assert.True(expected.AsSpan(0, written).SequenceEqual(actual.AsSpan(0, written)));
                        }
    }

    /// <summary>Exercises the bounded low-level ASCII, two-byte and three-byte kernels independently of native dispatch.</summary>
    [Fact]
    public unsafe void LowLevelWidthsPreserveExactStoresAndLaneOrder()
    {
        foreach (char value in new[] { 'x', '\u07FF', '\u4E2D' })
        {
            string source = new string(value, 129);
            for (int capacity = 0; capacity <= source.Length * 3 + 1; capacity++)
            {
                byte[] expected = new byte[capacity];
                byte[] guarded = Enumerable.Repeat((byte)0xCC, capacity + 32).ToArray();
                var status = Utf8.FromUtf16(source, expected, out int expectedRead, out int expectedWritten, false, false);
                fixed (char* input = source)
                fixed (byte* output = guarded)
                {
                    var actual = StringSerializer.TranscodeToUtf8(input, source.Length, output + 16, capacity, out char* remainingInput, out byte* remainingOutput);
                    Assert.Equal(status, actual);
                    Assert.Equal(expectedRead, (int)(remainingInput - input));
                    Assert.Equal(expectedWritten, (int)(remainingOutput - output - 16));
                    Assert.True(expected.AsSpan(0, expectedWritten).SequenceEqual(guarded.AsSpan(16, expectedWritten)));
                    Assert.All(guarded.Take(16).Concat(guarded.Skip(capacity + 16)), item => Assert.Equal((byte)0xCC, item));
                }
            }
        }
    }

    /// <summary>Checks all eight-lane ASCII/non-ASCII masks, including exact stores and lane ordering.</summary>
    [Fact]
    public void EveryMaskMatchesUtf8AtEveryVectorWidth()
    {
        for (int mask = 0; mask < 256; mask++)
        {
            char[] lane = Enumerable.Range(0, 8).Select(index => (char)((mask & (1 << index)) == 0 ? 'A' + index : 0x80 + index * 17)).ToArray();
            foreach (int repeats in new[] { 1, 2, 4, 5, 9 })
            {
                string source = "\u00FF" + string.Concat(Enumerable.Repeat(new string(lane), repeats));
                byte[] expected = Encoding.UTF8.GetBytes(source);
                byte[] guarded = Enumerable.Repeat((byte)0xCC, expected.Length + 32).ToArray();
                var status = StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(source, guarded.AsSpan(16, expected.Length), out int read, out int written);
                Assert.Equal(System.Buffers.OperationStatus.Done, status);
                Assert.Equal(source.Length, read);
                Assert.Equal(expected.Length, written);
                Assert.Equal(expected, guarded.AsSpan(16, written).ToArray());
                Assert.All(guarded.Take(16).Concat(guarded.Skip(expected.Length + 16)), value => Assert.Equal((byte)0xCC, value));
            }
        }
    }

    /// <summary>Checks partial buffers, Unicode transitions, malformed surrogates and streaming flags.</summary>
    [Fact]
    public void Latin1AndWideRunsPreservePlatformStatusAndConsumption()
    {
        foreach (string prefix in new[] { string.Concat(Enumerable.Repeat("\u00E9cafe\u0080\u00FF", 10)), new string('\u03A9', 65), new string('\u4E2D', 65) })
            foreach (string suffix in new[] { "", "abc", "\u07FF\u0800", "\uD83D\uDE00", "\uD800", "\uDC00", "\uD800X" })
                foreach (bool replace in new[] { false, true })
                    foreach (bool final in new[] { false, true })
                    {
                        string source = prefix + suffix;
                        int required = Encoding.UTF8.GetByteCount(source);
                        for (int capacity = 0; capacity <= required + 1; capacity++)
                        {
                            byte[] expected = new byte[capacity];
                            byte[] guarded = Enumerable.Repeat((byte)0xCC, capacity + 32).ToArray();
                            var status = Utf8.FromUtf16(source, expected, out int expectedRead, out int expectedWritten, replace, final);
                            var actual = StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(source, guarded.AsSpan(16, capacity), out int read, out int written,
                                replaceInvalidSequences: replace, isFinalBlock: final);
                            Assert.Equal(status, actual);
                            Assert.Equal(expectedRead, read);
                            Assert.Equal(expectedWritten, written);
                            Assert.True(expected.AsSpan(0, written).SequenceEqual(guarded.AsSpan(16, written)));
                            Assert.All(guarded.Take(16).Concat(guarded.Skip(capacity + 16)), value => Assert.Equal((byte)0xCC, value));
                        }
                    }
    }

    /// <summary>Exercises every Latin1 scalar value and verifies the standard decoder's corresponding result.</summary>
    [Fact]
    public void AllLatin1CharactersRoundTripWithoutANewEncoding()
    {
        string text = "\u00E9" + new string(Enumerable.Range(0, 256).Select(value => (char)value).ToArray());
        byte[] payload = new byte[Encoding.UTF8.GetMaxByteCount(text.Length)];
        StringSerializer.Serialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(text, payload, out int read, out int written);
        char[] decoded = new char[text.Length];
        var status = StringSerializer.Deserialize<global::LuminPack.Option.LuminPackUtf8LengthOptions>(payload.AsSpan(0, written), decoded, out int consumed, out int chars);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(text.Length, read);
        Assert.Equal(written, consumed);
        Assert.Equal(text, new string(decoded, 0, chars));
    }
}
