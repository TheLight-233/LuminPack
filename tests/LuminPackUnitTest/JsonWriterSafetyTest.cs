using System.Text;
using System.Text.Json;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

internal static class JsonWriterSafetyTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(LargeStringGrowsBeforeWriting), LargeStringGrowsBeforeWriting);
        RunCase(results, nameof(StringEscapesAndSurrogatePairsRoundTrip), StringEscapesAndSurrogatePairsRoundTrip);
        RunCase(results, nameof(NullStringArrayKeepsCommaState), NullStringArrayKeepsCommaState);
        RunCase(results, nameof(FixedSpanWriterChecksCapacity), FixedSpanWriterChecksCapacity);
    }

    private static void LargeStringGrowsBeforeWriting()
    {
        var value = new string('x', 300 * 1024) + "-尾部-😀";
        using var writer = new LuminBufferWriter(true);

        LuminPackSerializer.SerializeJson(value, writer);

        Assert(writer.CurrentIndex > 300 * 1024, "Large JSON string did not publish its full length.");
        var json = Encoding.UTF8.GetString(writer.GetSpan());
        using var document = JsonDocument.Parse(json);
        Assert(document.RootElement.GetString() == value,
            "Large JSON string was corrupted while growing the native buffer.");
    }

    private static void StringEscapesAndSurrogatePairsRoundTrip()
    {
        const string value = "quote=\" slash=\\ controls=\b\f\n\r\t emoji=😀";
        var json = LuminPackSerializer.SerializeJson(value);

        using var utf8Document = JsonDocument.Parse(json);
        Assert(utf8Document.RootElement.GetString() == value,
            "UTF-8 JSON escaping or surrogate-pair encoding changed the string.");

        var utf16 = LuminPackSerializer.SerializeJson(value, new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF16
        });
        using var utf16Document = JsonDocument.Parse(utf16);
        Assert(utf16Document.RootElement.GetString() == value,
            "UTF-16 JSON escaping changed the string.");
    }

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

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
