using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides json fused string scanner test functionality.</summary>
public class JsonFusedStringScannerTest
{
    /// <summary>Checks reading and skipping escaped runs in unaligned, truncated and control-bearing windows.</summary>
    /// <param name="utf8">Whether the input uses UTF-8 rather than UTF-16.</param>
    /// <param name="strict">Whether strict JSON validation is selected.</param>
    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public void EscapedLiteralRunsRespectInputWindows(bool utf8, bool strict)
    {
        if (utf8)
        {
            if (strict) CheckEscapedLiteralWindows<TestStrictUtf8Length>();
            else CheckEscapedLiteralWindows<TestUtf8Length>();
        }
        else
        {
            if (strict) CheckEscapedLiteralWindows<TestStrictUtf16Length>();
            else CheckEscapedLiteralWindows<TestUtf16Length>();
        }
    }

    /// <summary>Exercises literal-run search boundaries through decoded and validation-only string operations.</summary>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    private static void CheckEscapedLiteralWindows<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        Encoding encoding = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
        foreach (int length in new[] { 15, 16, 17, 31, 32, 33, 63, 64, 65, 255, 256, 257 })
        {
            string run = new('x', length);
            string json = "\"\\n" + run + "\\\"" + run + "\\\\" + run + "\"";
            string expected = JsonSerializer.Deserialize<string>(json)!;
            byte[] bytes = encoding.GetBytes(json);
            foreach (bool skip in new[] { false, true })
            {
                // A valid closing quote outside a shortened window must remain invisible to the search.
                byte[] guarded = new byte[bytes.Length + 8];
                bytes.CopyTo(guarded, 3);
                Assert.Equal(expected, ReadEscapedWindow<TOptions>(guarded, bytes.Length, skip, expected));
                foreach (int cut in new[] { 3, bytes.Length / 2, bytes.Length - 2, bytes.Length - 1 })
                    Assert.Throws<FormatException>(() => ReadEscapedWindow<TOptions>(guarded, cut, skip, expected));

                foreach (char control in Enumerable.Range(0, 32).Select(value => (char)value))
                {
                    byte[] malformed = encoding.GetBytes("\"\\n" + run + control + run + "\"");
                    byte[] malformedGuarded = new byte[malformed.Length + 8];
                    malformed.CopyTo(malformedGuarded, 3);
                    Assert.Throws<FormatException>(() => ReadEscapedWindow<TOptions>(malformedGuarded, malformed.Length, skip, expected));
                }
                foreach (string escape in new[] { "\\q", "\\u00G0", "\\uD800", "\\uDC00" })
                {
                    byte[] malformed = encoding.GetBytes("\"\\n" + run + escape + run + "\"");
                    byte[] malformedGuarded = new byte[malformed.Length + 8];
                    malformed.CopyTo(malformedGuarded, 3);
                    Assert.Throws<FormatException>(() => ReadEscapedWindow<TOptions>(malformedGuarded, malformed.Length, skip, expected));
                }
            }
        }
    }

    /// <summary>Reads or skips a string from the exact input window without exposing its surrounding bytes.</summary>
    /// <typeparam name="TOptions">The compiled document profile.</typeparam>
    /// <param name="guarded">The storage containing the input and its guards.</param>
    /// <param name="length">The visible input length.</param>
    /// <param name="skip">Whether to validate without decoding the string.</param>
    /// <param name="expected">The result used by a validation-only operation.</param>
    /// <returns>The decoded string, or the expected string after successful validation.</returns>
    private static string ReadEscapedWindow<TOptions>(byte[] guarded, int length, bool skip, string expected)
        where TOptions : struct, ILuminPackOptions
    {
        ReadOnlySpan<byte> input = guarded.AsSpan(3, length);
        var reader = new LuminPackJsonReader(ref input, new LuminPackReaderOptionalState());
        Assert.True(reader.Read<TOptions>());
        string result;
        if (skip)
        {
            reader.Skip<TOptions>();
            result = expected;
        }
        else result = reader.Read<string, TOptions>();
        Assert.Equal(length, reader.Position);
        reader.EnsureEndOfDocument<TOptions>();
        return result;
    }

    /// <summary>Reads reused, null and explicitly selected strings among other JSON values.</summary>
    [Fact]
    public void StringReferenceReadsAdvanceAndConsumeTokens()
    {
        byte[] data = Encoding.Unicode.GetBytes("[\"same\",null,\"next\",5]");
        ReadOnlySpan<byte> input = data;
        var reader = new LuminPackJsonReader(ref input, new LuminPackReaderOptionalState());
        reader.TryConsumeArrayStart();
        string value = new string(new[] { 's', 'a', 'm', 'e' });
        string original = value;
        reader.Read(ref value);
        Assert.Same(original, value);
        reader.Read(ref value);
        Assert.Null(value);
        Assert.True(reader.Read());
        reader.Read(ref value);
        Assert.Equal("next", value);
        int number = 0;
        reader.Read(ref number);
        Assert.Equal(5, number);
        reader.ConsumeArrayEnd();
        reader.EnsureEndOfDocument();
    }
    /// <summary>Preserves overlapping forward decoding when callers supply input as the public scratch buffer.</summary>
    [Fact]
    public void LiteralRunCopiesPreserveAliasedScratchStorage()
    {
        string prefix = new(Enumerable.Range(0, 128).Select(index => (char)('a' + index % 23)).ToArray());
        string suffix = new(Enumerable.Range(0, 128).Select(index => (char)(0x4E00 + index % 31)).ToArray());
        string tail = new(Enumerable.Range(0, 128).Select(index => (char)('A' + index % 26)).ToArray());
        string value = prefix + "\n" + suffix + "\t" + tail;
        string json = "\"" + prefix + "\\n" + suffix + "\\t" + tail + "\"";
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        Span<byte> byteWindow = bytes;
        var byteReader = new LuminPackJsonReader(ref byteWindow, new LuminPackReaderOptionalState());
        byteReader.Utf8StringScratch = bytes;
        Assert.True(byteReader.Read<LuminPackUtf8LengthOptions>());
        Assert.Equal(value, Encoding.UTF8.GetString(byteReader.ReadStringUtf8<LuminPackUtf8LengthOptions>()));
        byteReader.EnsureEndOfDocument<LuminPackUtf8LengthOptions>();

        char[] chars = json.ToCharArray();
        Span<byte> charWindow = MemoryMarshal.AsBytes(chars.AsSpan());
        var charReader = new LuminPackJsonReader(ref charWindow, new LuminPackReaderOptionalState());
        charReader.Utf16StringScratch = chars;
        Assert.True(charReader.Read<LuminPackDefaultOptions>());
        Assert.Equal(value, new string(charReader.ReadStringUtf16<LuminPackDefaultOptions>()));
        charReader.EnsureEndOfDocument<LuminPackDefaultOptions>();
    }

    /// <summary>Checks complete literal runs around every escape across short and vector-sized boundaries.</summary>
    /// <param name="encoding">The JSON string encoding.</param>
    /// <param name="strict">Whether the original strict grammar is selected.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, false)]
    [InlineData(LuminPackStringEncoding.UTF8, true)]
    [InlineData(LuminPackStringEncoding.UTF16, false)]
    [InlineData(LuminPackStringEncoding.UTF16, true)]
    public void LiteralRunsAroundEscapesPreserveDecodedCodeUnits(LuminPackStringEncoding encoding, bool strict)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, ValidateJsonSyntax = strict };
        foreach (int length in new[] { 0, 1, 7, 15, 16, 17, 31, 32, 33, 63, 64, 65, 256, 4096 })
        {
            string prefix = new('x', length);
            string suffix = new('\u4E2D', length);
            foreach (string escape in new[] { "\\\"", "\\\\", "\\/", "\\b", "\\f", "\\n", "\\r", "\\t", "\\u0000", "\\u4E2D", "\\uD83D\\uDE00" })
            {
                string json = "\"" + prefix + escape + suffix + "\\n" + prefix + "\"";
                using var expected = JsonDocument.Parse(json);
                byte[] bytes = (encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(json);
                byte[] guarded = Enumerable.Repeat((byte)0xA5, bytes.Length + 16).ToArray();
                bytes.CopyTo(guarded, 3);
                Assert.Equal(expected.RootElement.GetString(), LuminPackSerializer.DeserializeJson<string>(guarded.AsSpan(3, bytes.Length), option));
                Assert.Equal(bytes, guarded.AsSpan(3, bytes.Length).ToArray());
                Assert.All(guarded.AsSpan(0, 3).ToArray(), value => Assert.Equal((byte)0xA5, value));
                Assert.All(guarded.AsSpan(3 + bytes.Length).ToArray(), value => Assert.Equal((byte)0xA5, value));
            }
        }
    }

    /// <summary>Checks UTF-16 literal segments and escape stores in exact and shortened unaligned destinations.</summary>
    [Fact]
    public void Utf16EscapedRunsRespectExactAndShortDestinationWindows()
    {
        foreach (int length in new[] { 0, 1, 7, 15, 16, 17, 31, 32, 33, 64, 256, 4096 })
        {
            string prefix = new('x', length);
            string suffix = new('\u4E2D', length);
            string value = prefix + "\n\"\\\0" + suffix + "\t";
            byte[] expected = Encoding.Unicode.GetBytes("\"" + prefix + "\\n\\\"\\\\\\u0000" + suffix + "\\t\"");
            foreach (int capacity in new[] { 0, 1, 2, 3, 15, 16, 31, 32, length * 2 + 3, expected.Length - 1, expected.Length }.Where(size => size <= expected.Length).Distinct())
            {
                byte[] guarded = Enumerable.Repeat((byte)0xA5, capacity + 20).ToArray();
                Span<byte> window = guarded.AsSpan(3, capacity);
                var writer = new LuminPackJsonWriter(ref window, new LuminPackWriterOptionalState());
                bool failed = false;
                try { writer.Write<LuminPackDefaultOptions>(value); }
                catch (Exception) { failed = true; }
                Assert.Equal(capacity < expected.Length, failed);
                Assert.InRange(writer.Position, 0, capacity);
                Assert.Equal(expected.AsSpan(0, writer.Position).ToArray(), window.Slice(0, writer.Position).ToArray());
                Assert.All(guarded.AsSpan(0, 3).ToArray(), item => Assert.Equal((byte)0xA5, item));
                Assert.All(guarded.AsSpan(3 + capacity).ToArray(), item => Assert.Equal((byte)0xA5, item));
            }
        }
    }

    /// <summary>Verifies escapes controls and truncations agree across scan boundaries.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    /// <param name="strict">The strict used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, false)]
    [InlineData(LuminPackStringEncoding.UTF8, true)]
    [InlineData(LuminPackStringEncoding.UTF16, false)]
    [InlineData(LuminPackStringEncoding.UTF16, true)]
    public void EscapesControlsAndTruncationsAgreeAcrossScanBoundaries(LuminPackStringEncoding encoding, bool strict)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, ValidateJsonSyntax = strict };
        foreach (int length in new[] { 0, 1, 7, 15, 16, 17, 24, 31, 32, 63, 128 })
        {
            string prefix = new('x', length);
            foreach (string suffix in new[] { "tail", "\\\"tail", "\\ntail", "\\\\tail", "\\u4E2Dtail", "\\uD83D\\uDE00tail" })
            {
                string json = "\"" + prefix + suffix + "\"";
                using var document = JsonDocument.Parse(json);
                Assert.Equal(document.RootElement.GetString(), LuminPackSerializer.DeserializeJson<string>(json, option));
                byte[] bytes = encoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8.GetBytes(json) : Encoding.Unicode.GetBytes(json);
                for (int cut = 1; cut < bytes.Length; cut++)
                {
                    int truncatedLength = cut;
                    Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<string>(bytes.AsSpan(0, truncatedLength), option));
                }
            }

            for (char control = '\0'; control < 0x20; control++)
            {
                string beforeEscape = "\"" + prefix + control + "\\n\"";
                string afterEscape = "\"" + prefix + "\\n" + control + "\"";
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<string>(beforeEscape, option));
                Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<string>(afterEscape, option));
            }
        }
    }
}
