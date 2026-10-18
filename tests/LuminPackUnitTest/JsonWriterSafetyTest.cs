using System.Text;
using System.Text.Json;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>Provides json writer safety test functionality.</summary>
internal static class JsonWriterSafetyTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(LargeStringGrowsBeforeWriting), LargeStringGrowsBeforeWriting);
        RunCase(results, nameof(StringEscapesAndSurrogatePairsRoundTrip), StringEscapesAndSurrogatePairsRoundTrip);
        RunCase(results, nameof(NullStringArrayKeepsCommaState), NullStringArrayKeepsCommaState);
        RunCase(results, nameof(FixedSpanWriterChecksCapacity), FixedSpanWriterChecksCapacity);
        RunCase(results, nameof(Utf8StringsPreserveBytesAndSpanBoundaries), Utf8StringsPreserveBytesAndSpanBoundaries);
        RunCase(results, nameof(Utf8PropertyNamesPreserveBytes), Utf8PropertyNamesPreserveBytes);
        RunCase(results, nameof(Utf8SegmentsGrowAndKeepFollowingValues), Utf8SegmentsGrowAndKeepFollowingValues);
        RunCase(results, nameof(DenseUtf8HandlesEveryBmpCodeUnit), DenseUtf8HandlesEveryBmpCodeUnit);
    }

    /// <summary>Verifies expected utf8 string.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static byte[] ExpectedUtf8String(string value)
    {
        // Normalize invalid UTF-16 with the existing Encoding.UTF8 replacement policy.
        string normalized = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(value));
        var json = new StringBuilder("\"");
        foreach (char c in normalized)
        {
            switch (c)
            {
                case '"': json.Append("\\\""); break;
                case '\\': json.Append("\\\\"); break;
                case '\n': json.Append("\\n"); break;
                case '\r': json.Append("\\r"); break;
                case '\t': json.Append("\\t"); break;
                default:
                    if (c < 0x20) json.Append("\\u").Append(((int)c).ToString("x4"));
                    else json.Append(c);
                    break;
            }
        }
        json.Append('"');
        return Encoding.UTF8.GetBytes(json.ToString());
    }

    /// <summary>Checks utf8 string against the operation's requirements.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    private static void CheckUtf8String(string value)
    {
        byte[] expected = ExpectedUtf8String(value);
        var state = new LuminPackWriterOptionalState();
        byte[] guarded = Enumerable.Repeat((byte)0xCC, expected.Length + 32).ToArray();
        Span<byte> exact = guarded.AsSpan(16, expected.Length);
        var writer = new LuminPackJsonWriter(ref exact, state);
        writer.WriteString(value);
        Assert(writer.GetSpan().SequenceEqual(expected), "UTF-8 string bytes changed.");
        Assert(guarded.AsSpan(0, 16).IndexOfAnyExcept((byte)0xCC) < 0 &&
               guarded.AsSpan(16 + expected.Length).IndexOfAnyExcept((byte)0xCC) < 0,
            "A UTF-8 string escaped its caller-provided span.");

        // Insufficient spans must reject the write without touching the adjacent memory.
        Array.Fill(guarded, (byte)0xCC);
        Span<byte> shortSpan = guarded.AsSpan(16, expected.Length - 1);
        var shortWriter = new LuminPackJsonWriter(ref shortSpan, state);
        bool threw = false;
        try { shortWriter.WriteString(value); }
        catch (Exception) { threw = true; }
        Assert(threw, "An undersized UTF-8 destination was accepted.");
        Assert(guarded.AsSpan(0, 16).IndexOfAnyExcept((byte)0xCC) < 0 &&
               guarded.AsSpan(16 + shortSpan.Length).IndexOfAnyExcept((byte)0xCC) < 0,
            "An insufficient UTF-8 destination corrupted adjacent memory.");
    }

    /// <summary>Verifies utf8 strings preserve bytes and span boundaries.</summary>
    private static void Utf8StringsPreserveBytesAndSpanBoundaries()
    {
        foreach (string value in new[] { "", "ascii", "\u4E2D\u6587", "éΚαλημέρα", "🚀😀",
                     "\uD800", "\uDC00", "\uD800X", "X\uDC00Y", "\uD800\n\uDC00",
                     "\n\t\u0022\\\u0000\u4E2D\uD83D\uDE80", new string(Enumerable.Range(0, 32).Select(i => (char)i).ToArray()),
                     new string('\u4e2d', 128) + "\n" + new string('x', 128),
                     new string('\n', 63) + "😀" + new string('\u4e2d', 128),
                     new string('\n', 64) + "\uD800X", new string('\n', 65) + "\uDC00" })
            CheckUtf8String(value);

        var random = new Random(42);
        for (int sample = 0; sample < 500; sample++)
        {
            char[] chars = new char[random.Next(0, 257)];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = random.Next(4) == 0 ? (char)random.Next(128) : (char)random.Next(65536);
            CheckUtf8String(new string(chars));
        }
    }

    /// <summary>Verifies utf8 property names preserve bytes.</summary>
    private static void Utf8PropertyNamesPreserveBytes()
    {
        foreach (string name in new[] { "", "ascii", "\u4E2D\u6587\uD83D\uDE00", "\n\t\u0022\\\u0000\u4E2D\uD83D\uDE80",
                     new string('\u4e2d', 128) + "\"" + new string('x', 128) })
        {
            byte[] quoted = ExpectedUtf8String(name);
            byte[] expected = Encoding.UTF8.GetBytes("{" + Encoding.UTF8.GetString(quoted) + ":null}");
            foreach (bool raw in new[] { false, true })
            {
                byte[] guarded = Enumerable.Repeat((byte)0xCC, expected.Length + 16).ToArray();
                Span<byte> destination = guarded.AsSpan(0, expected.Length);
                var writer = new LuminPackJsonWriter(ref destination, new LuminPackWriterOptionalState());
                writer.WriteObjectStart();
                if (raw) writer.WritePropertyName(Encoding.UTF8.GetBytes(name).AsSpan());
                else writer.WritePropertyName(name);
                writer.WriteNull();
                writer.WriteObjectEnd();
                Assert(writer.GetSpan().SequenceEqual(expected), "UTF-8 property name bytes changed.");
                Assert(guarded.AsSpan(expected.Length).IndexOfAnyExcept((byte)0xCC) < 0,
                    "A UTF-8 property name wrote past its span.");
            }
        }
    }

    /// <summary>Verifies utf8 segments grow and keep following values.</summary>
    private static void Utf8SegmentsGrowAndKeepFollowingValues()
    {
        string value = new string('\u4e2d', 100_000) + "\n\t\"\\\0😀\uD800" + new string('é', 10_000);
        using var buffer = new LuminBufferWriter(true);
        var writer = new LuminPackJsonWriter(buffer, new LuminPackWriterOptionalState());
        writer.WriteArrayStart();
        writer.WriteString(value);
        writer.WriteString("tail😀");
        writer.WriteArrayEnd();
        string expected = "[" + Encoding.UTF8.GetString(ExpectedUtf8String(value)) + ",\"tail😀\"]";
        Assert(writer.GetSpan().SequenceEqual(Encoding.UTF8.GetBytes(expected)),
            "UTF-8 segment growth corrupted a following value or comma state.");
    }

    /// <summary>Verifies dense utf8 handles every bmp code unit.</summary>
    private static void DenseUtf8HandlesEveryBmpCodeUnit()
    {
        // A leading escape forces the small scalar encoder, including isolated surrogates.
        for (int codeUnit = 0; codeUnit <= char.MaxValue; codeUnit++)
            CheckUtf8String("\n" + (char)codeUnit + "\t");
        foreach (char high in new[] { '\uD800', '\uDBFF' })
            foreach (char low in new[] { '\uDC00', '\uDFFF' })
                CheckUtf8String("\n" + high + low + "\t");
    }

    /// <summary>Verifies large string grows before writing.</summary>
    private static void LargeStringGrowsBeforeWriting()
    {
        var value = new string('x', 300 * 1024) + "-\u5C3E\u90E8-\uD83D\uDE00";
        using var writer = new LuminBufferWriter(true);

        LuminPackSerializer.SerializeJson(value, writer);

        Assert(writer.CurrentIndex > 300 * 1024, "Large JSON string did not publish its full length.");
        var json = Encoding.UTF8.GetString(writer.GetSpan());
        using var document = JsonDocument.Parse(json);
        Assert(document.RootElement.GetString() == value,
            "Large JSON string was corrupted while growing the native buffer.");
    }

    /// <summary>Verifies string escapes and surrogate pairs round trip.</summary>
    private static void StringEscapesAndSurrogatePairsRoundTrip()
    {
        const string value = "quote=\" slash=\\ controls=\b\f\n\r\t emoji=😀";
        var json = LuminPackSerializer.SerializeJson(value);

        using var utf8Document = JsonDocument.Parse(json);
        Assert(utf8Document.RootElement.GetString() == value,
            "UTF-8 JSON escaping or surrogate-pair encoding changed the string.");

        var utf16 = LuminPackSerializer.SerializeJson(value, new TestSerializationProfile
        {
            StringEncoding = LuminPackStringEncoding.UTF16
        });
        using var utf16Document = JsonDocument.Parse(utf16);
        Assert(utf16Document.RootElement.GetString() == value,
            "UTF-16 JSON escaping changed the string.");
    }

    /// <summary>Verifies null string array keeps comma state.</summary>
    private static void NullStringArrayKeepsCommaState()
    {
        string?[] value = ["first", null, "third"];
        var json = LuminPackSerializer.SerializeJson(value);
        using var document = JsonDocument.Parse(json);
        var roundTrip = document.RootElement.EnumerateArray()
            .Select(static element => element.ValueKind == JsonValueKind.Null ? null : element.GetString())
            .ToArray();

        Assert(roundTrip.SequenceEqual(value),
            "A null string value corrupted the surrounding JSON array comma state.");
    }

    /// <summary>Verifies fixed span writer checks capacity.</summary>
    private static void FixedSpanWriterChecksCapacity()
    {
        var state = new LuminPackWriterOptionalState();

        Span<byte> enough = stackalloc byte[32];
        var writer = new LuminPackJsonWriter(ref enough, state);
        writer.WriteString("fixed");
        Assert(Encoding.UTF8.GetString(writer.GetSpan()) == "\"fixed\"",
            "Fixed-span JSON writer failed on a sufficient destination.");

        Span<byte> tooSmall = stackalloc byte[4];
        var smallWriter = new LuminPackJsonWriter(ref tooSmall, state);
        var threw = false;
        try
        {
            smallWriter.WriteString("too-large");
        }
        catch (Exception)
        {
            threw = true;
        }

        Assert(threw, "Fixed-span JSON writer did not reject an insufficient destination.");
    }

    /// <summary>Verifies run case.</summary>
    /// <param name="results">The results used by this operation.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="test">The test used by this operation.</param>
    private static void RunCase(List<string> results, string name, Action test)
    {
        try
        {
            test();
            results.Add($"✓ {name} - PASSED");
        }
        catch (Exception ex)
        {
            results.Add($"✗ {name} - ERROR: {ex.Message}");
        }
    }

    /// <summary>Verifies assert.</summary>
    /// <param name="condition">The condition used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
