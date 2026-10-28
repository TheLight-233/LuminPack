using System.Runtime.InteropServices;
using LuminPack.Core;
using System.Text;
using LuminPack;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Provides json ordered property test functionality.</summary>
public class JsonOrderedPropertyTest
{
    /// <summary>Verifies generic name collisions and empty contracts in both encodings and grammar modes.</summary>
    [Fact]
    public void GenericNamesAndEmptyContractsKeepTheirOrderedPaths()
    {
        var source = new JsonOrderedGenericModel<int, string> { First = 731, Second = "value" };
        foreach (var encoding in new[] { LuminPackStringEncoding.UTF8, LuminPackStringEncoding.UTF16 })
        foreach (bool strict in new[] { false, true })
        {
            var option = new TestSerializationProfile { StringEncoding = encoding, ValidateJsonSyntax = strict };
            string json = LuminPackSerializer.SerializeJson(source, option);
            var value = LuminPackSerializer.DeserializeJson<JsonOrderedGenericModel<int, string>>(json, option);
            Assert.Equal(source.First, value.First);
            Assert.Equal(source.Second, value.Second);
            Assert.Equal("{}", LuminPackSerializer.SerializeJson(new JsonOrderedEmptyModel(), option));
            Assert.NotNull(LuminPackSerializer.DeserializeJson<JsonOrderedEmptyModel>("{}", option));
        }
    }

    /// <summary>Verifies ordered and fallback paths can alternate.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    /// <param name="strict">The strict used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8, false)]
    [InlineData(LuminPackStringEncoding.UTF8, true)]
    [InlineData(LuminPackStringEncoding.UTF16, false)]
    [InlineData(LuminPackStringEncoding.UTF16, true)]
    public void OrderedAndFallbackPathsCanAlternate(LuminPackStringEncoding encoding, bool strict)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, ValidateJsonSyntax = strict };
        // Ordered prefix, unknown nested value, escaped name, reordered field,
        // whitespace, duplicate and then an ordered suffix in the same object.
        const string json = "{\"F00\":10,\"F01\":11,\"unknown\":{\"x\":[{\"F02\":99}]}," +
            "\"F0\\u0032\":12,\"F07\":17, \"F03\" : 13,\"F03\":23,\"F04\":14,\"F05\":15}";
        var result = LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(json, option);
        Assert.Equal(10, result.F00);
        Assert.Equal(11, result.F01);
        Assert.Equal(12, result.F02);
        Assert.Equal(23, result.F03);
        Assert.Equal(14, result.F04);
        Assert.Equal(15, result.F05);
        Assert.Equal(0, result.F06);
        Assert.Equal(17, result.F07);

        var unicode = LuminPackSerializer.DeserializeJson<JsonSmallDispatchModel>(
            "{\u0022AA\u0022:1,\u0022AB\u0022:2,\u0022\u540D\u79F0\u0022:\u0022\u4E2D\u6587\uD83D\uDE00\\n\u0022}", option);
        Assert.Equal("\u4E2D\u6587\uD83D\uDE00\n", unicode.\u540d\u79f0);
    }

    /// <summary>Verifies strict ordered path rejects malformed separators and truncations.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void StrictOrderedPathRejectsMalformedSeparatorsAndTruncations(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, ValidateJsonSyntax = true };
        string[] invalid =
        {
            "{\"F00\":0\"F01\":1}", "{,\"F00\":0}", "{\"F00\":0,,\"F01\":1}",
            "{\"F00\":0,}", "{\"F00\":0,\"F01\":}", "{\"F00\":0,\"F01\":]}",
            "{\"F00\":0,\"F01\"1}", "{\"F00\":0,\"F01\"::1}",
            "{\"F00\":0,\"F01\":1]", "{\"F00\":0}{}"
        };
        foreach (string json in invalid)
            Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(json, option));

        const string complete = "{\"F00\":0,\"F01\":1,\"F02\":2}";
        var bytes = encoding == LuminPackStringEncoding.UTF8
            ? Encoding.UTF8.GetBytes(complete) : Encoding.Unicode.GetBytes(complete);
        for (int length = 1; length < bytes.Length; length++)
        {
            int cut = length;
            Assert.ThrowsAny<Exception>(() =>
                LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(bytes.AsSpan(0, cut), option));
        }
    }

    /// <summary>Verifies random property orders preserve values.</summary>
    /// <param name="encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void RandomPropertyOrdersPreserveValues(LuminPackStringEncoding encoding)
    {
        var option = new TestSerializationProfile { StringEncoding = encoding, ValidateJsonSyntax = true };
        var random = new Random(20261011);
        for (int i = 0; i < 100; i++)
        {
            string json = "{" + string.Join(",", Enumerable.Range(0, 12)
                .OrderBy(_ => random.Next()).Select(n => $"\"F{n:D2}\":{n + 10}")) + "}";
            var value = LuminPackSerializer.DeserializeJson<JsonLargeDispatchModel>(json, option);
            Assert.Equal("{\"F00\":10,\"F01\":11,\"F02\":12,\"F03\":13,\"F04\":14,\"F05\":15," +
                "\"F06\":16,\"F07\":17,\"F08\":18,\"F09\":19,\"F10\":20,\"F11\":21}",
                LuminPackSerializer.SerializeJson(value, option));
        }
    }

    /// <summary>Compares short complete and truncated property windows with the original token reader.</summary>
    [Fact]
    public void AdjacentPropertyValuesMatchCanonicalTokenSelection()
    {
        CheckPropertyWindows<TestUtf8Length>();
        CheckPropertyWindows<TestUtf16Length>();
        CheckPropertyWindows<TestStrictUtf8Length>();
        CheckPropertyWindows<TestStrictUtf16Length>();
    }

    /// <summary>Checks both comma states, all short windows, unaligned input and scalar completion.</summary>
    /// <typeparam name="TOptions">The statically selected encoding and grammar profile.</typeparam>
    private static void CheckPropertyWindows<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var encoding = default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode;
        foreach (bool comma in new[] { false, true })
        foreach (string gap in new[] { "", "\n " })
        foreach (string tail in new[] { "0", "-1", "1.25", "-", "\"x\"", "\"a\\n\\u4E2D\"", "null", "true", "[]", "\"bad" })
        {
            string prefix = (comma ? "," : "") + "\"Name\":";
            byte[] data = encoding.GetBytes(prefix + gap + tail + "}");
            int prefixLength = encoding.GetByteCount(prefix);
            for (int length = 0; length <= data.Length; length++)
            foreach (int offset in new[] { 0, 1 })
            for (int kind = 0; kind <= 2; kind++)
            {
                string expected = ObservePropertyWindow<TOptions>(data, length, prefixLength, comma, kind, offset, canonical: true);
                string actual = ObservePropertyWindow<TOptions>(data, length, prefixLength, comma, kind, offset, canonical: false);
                Assert.Equal(expected, actual);
            }
        }
    }

    /// <summary>Captures the full public token operation over a shortened, pinned, guarded window.</summary>
    /// <typeparam name="TOptions">The statically selected encoding and grammar profile.</typeparam>
    /// <param name="data">The complete backing payload retained behind each short window.</param>
    /// <param name="length">The exposed input length.</param>
    /// <param name="prefixLength">The complete known property-prefix width.</param>
    /// <param name="comma">Whether the initial state follows a preceding property value.</param>
    /// <param name="kind">Zero for token selection, one for string completion, two for integer completion.</param>
    /// <param name="offset">The unaligned byte offset after the leading guard.</param>
    /// <param name="canonical">Whether to replay the original checked token operation after the known prefix.</param>
    /// <returns>The matched flag, public reader state, value, scratch widths and exception observation.</returns>
    private static unsafe string ObservePropertyWindow<TOptions>(byte[] data, int length, int prefixLength, bool comma, int kind, int offset, bool canonical)
        where TOptions : struct, ILuminPackOptions
    {
        byte[] backing = Enumerable.Repeat((byte)0xCC, data.Length + 34).ToArray();
        data.CopyTo(backing, 16 + offset);
        fixed (byte* pinned = backing)
        {
            ReadOnlySpan<byte> window = new ReadOnlySpan<byte>(pinned + 16 + offset, length);
            var reader = new LuminPackJsonReader(ref window, new LuminPackReaderOptionalState());
            reader.Depth = 1;
            reader.ObjectContainerBits = 1;
            reader.GrammarState = comma ? LuminPackJsonReader.JsonStateAfterValue : LuminPackJsonReader.JsonStateNameOrEnd;
            bool matched = false;
            string value = "not consumed", failure = "none";
            try
            {
                if (canonical)
                {
                    if (length >= prefixLength)
                    {
                        reader.Position = prefixLength;
                        if (default(TOptions).ValidateJsonSyntax) reader.GrammarState = LuminPackJsonReader.JsonStateValue;
                        if (!reader.Read<TOptions>()) throw new FormatException("Expected a JSON property value");
                        matched = true;
                    }
                }
                else matched = reader.TryReadPropertyName<TOptions>("Name"u8, "Name".AsSpan());
                if (matched && kind == 1)
                {
                    string? text = reader.Read<string, TOptions>();
                    value = text is null ? "null" : Convert.ToHexString(MemoryMarshal.AsBytes(text.AsSpan()));
                }
                else if (matched && kind == 2) value = reader.Read<long, TOptions>().ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception exception) { failure = exception.GetType().FullName + ":" + exception.Message; }
            Assert.Equal((byte)0xCC, pinned[0]);
            Assert.True(backing.AsSpan(0, 16 + offset).IndexOfAnyExcept((byte)0xCC) < 0);
            Assert.True(backing.AsSpan(16 + offset, data.Length).SequenceEqual(data));
            Assert.True(backing.AsSpan(16 + offset + data.Length).IndexOfAnyExcept((byte)0xCC) < 0);
            return $"{matched}|{reader.Position}|{reader.CurrentTokenType}|{reader.Depth}|{reader.GrammarState}|{reader.ObjectContainerBits}|{reader.Utf8StringScratch?.Length}|{reader.Utf16StringScratch?.Length}|{value}|{failure}";
        }
    }
}
