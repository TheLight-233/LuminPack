using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPackUnitTest;

// The reader's fast path is the default (trusted JSON, meant for LuminPack-produced documents);
// these regression cases exercise the explicit strict validation mode for external input.
internal static class StrictJson
{
    internal static readonly LuminPack.Option.LuminPackSerializerOption Utf8 =
        LuminPack.Option.LuminPackSerializerOption.Utf8 with { ValidateJsonSyntax = true };
}

[LuminPackable]
public partial class StrayNumberProbeModel
{
    public int Age { get; set; }
}

[LuminPackable]
public partial class CircleRefJsonProbe
{
    public string Name { get; set; } = "";
}

internal static class PhaseOneRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(ReadVarIntByteConsumesUInt16Payload), ReadVarIntByteConsumesUInt16Payload);
        RunCase(results, nameof(JsonStrayNumberInObjectDoesNotHang), JsonStrayNumberInObjectDoesNotHang);
        RunCase(results, nameof(JsonCircleRefDrainDoesNotHang), JsonCircleRefDrainDoesNotHang);
        RunCase(results, nameof(JsonNanInfinityAreRejected), JsonNanInfinityAreRejected);
        RunCase(results, nameof(JsonOverflowNumberIsRejected), JsonOverflowNumberIsRejected);
        RunCase(results, nameof(JsonDeepNestingIsRejected), JsonDeepNestingIsRejected);
        RunCase(results, nameof(JsonManySeparatorsDoNotOverflowStack), JsonManySeparatorsDoNotOverflowStack);
    }

    private static void ReadVarIntByteConsumesUInt16Payload()
    {
        var buffer = new byte[16];
        var span = buffer.AsSpan();
        var writer = new LuminPackWriter(ref span);
        writer.WriteVarInt((ushort)255);
        int written = writer.GetCurrentSpanIndex();

        var reader = new LuminPackReader(ref span);
        byte value = reader.ReadVarIntByte();

        Assert(value == 255, $"ReadVarIntByte returned {value}, expected 255.");
        Assert(reader.GetCurrentSpanIndex() == written,
            $"ReadVarIntByte consumed {reader.GetCurrentSpanIndex()} bytes; writer wrote {written}.");
    }

    // Strict RFC 8259 token sequencing: a stray value inside an object is malformed
    // JSON. It used to be skipped silently; it is now rejected without hanging.
    private static void JsonStrayNumberInObjectDoesNotHang()
    {
        AssertThrows(() => LuminPackSerializer.DeserializeJson<StrayNumberProbeModel>("{\"Age\":1, 5}", StrictJson.Utf8),
            "A stray number in a JSON object was accepted.");
    }

    private static void JsonCircleRefDrainDoesNotHang()
    {
        AssertThrows(() => LuminPackSerializer.DeserializeJson<CircleRefJsonProbe>("{\"$id\":1,\"Name\":\"a\",5,\"$ref\":1}", StrictJson.Utf8),
            "A stray number in a circle-reference object was accepted.");
    }

    private static void JsonNanInfinityAreRejected()
    {
        AssertThrows(() => LuminPackSerializer.SerializeJson(double.PositiveInfinity),
            "PositiveInfinity was accepted by the JSON writer.");
        AssertThrows(() => LuminPackSerializer.SerializeJson(double.NaN),
            "NaN was accepted by the JSON writer.");
        AssertThrows(() => LuminPackSerializer.SerializeJson(float.NegativeInfinity),
            "NegativeInfinity was accepted by the JSON writer.");
    }

    private static void JsonOverflowNumberIsRejected()
    {
        AssertThrows(() => LuminPackSerializer.DeserializeJson<double>("1e999"),
            "An overflowing JSON number was accepted.");
    }

    private static void JsonDeepNestingIsRejected()
    {
        var json = new string('[', 5000) + "0" + new string(']', 5000);
        AssertThrows(() => LuminPackSerializer.DeserializeJson<int[]>(json, StrictJson.Utf8),
            "Deeply nested JSON exceeded the depth limit without an error.");
    }

    // Trailing separators were previously tolerated by the generic tokenizer; the strict
    // sequence check rejects them immediately, so no 100k-element skip walk can recurse.
    private static void JsonManySeparatorsDoNotOverflowStack()
    {
        var json = "[" + new string(',', 100_000) + "]";
        AssertThrows(() => LuminPackSerializer.DeserializeJson<int[]>(json, StrictJson.Utf8),
            "A stream of separators was accepted.");
    }

    private static void AssertThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }

        throw new InvalidOperationException(message);
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
