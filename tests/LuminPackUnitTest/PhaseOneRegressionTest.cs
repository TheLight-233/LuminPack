using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPackUnitTest;

// The reader's fast path is the default (trusted JSON, meant for LuminPack-produced documents);
// these regression cases exercise the explicit strict validation mode for external input.
/// <summary>Provides strict json functionality.</summary>
internal static class StrictJson
{
    internal static readonly LuminPack.Option.TestSerializationProfile Utf8 =
        LuminPack.Option.TestSerializationProfile.Utf8 with { ValidateJsonSyntax = true };
}

/// <summary>Provides stray number probe model functionality.</summary>
[LuminPackable]
public partial class StrayNumberProbeModel
{
    /// <summary>Gets or sets the age.</summary>
    public int Age { get; set; }
}

/// <summary>Provides circle ref json probe functionality.</summary>
[LuminPackable]
public partial class CircleRefJsonProbe
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = "";
}

/// <summary>Provides phase one regression test functionality.</summary>
internal static class PhaseOneRegressionTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
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

    /// <summary>Reads var int byte consumes u int16 payload from the current input.</summary>
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
    /// <summary>Verifies json stray number in object does not hang.</summary>
    private static void JsonStrayNumberInObjectDoesNotHang()
    {
        AssertThrows(() => LuminPackSerializer.DeserializeJson<StrayNumberProbeModel>("{\"Age\":1, 5}", StrictJson.Utf8),
            "A stray number in a JSON object was accepted.");
    }

    /// <summary>Verifies json circle ref drain does not hang.</summary>
    private static void JsonCircleRefDrainDoesNotHang()
    {
        AssertThrows(() => LuminPackSerializer.DeserializeJson<CircleRefJsonProbe>("{\"$id\":1,\"Name\":\"a\",5,\"$ref\":1}", StrictJson.Utf8),
            "A stray number in a circle-reference object was accepted.");
    }

    /// <summary>Verifies json nan infinity are rejected.</summary>
    private static void JsonNanInfinityAreRejected()
    {
        AssertThrows(() => LuminPackSerializer.SerializeJson(double.PositiveInfinity),
            "PositiveInfinity was accepted by the JSON writer.");
        AssertThrows(() => LuminPackSerializer.SerializeJson(double.NaN),
            "NaN was accepted by the JSON writer.");
        AssertThrows(() => LuminPackSerializer.SerializeJson(float.NegativeInfinity),
            "NegativeInfinity was accepted by the JSON writer.");
    }

    /// <summary>Verifies json overflow number is rejected.</summary>
    private static void JsonOverflowNumberIsRejected()
    {
        AssertThrows(() => LuminPackSerializer.DeserializeJson<double>("1e999"),
            "An overflowing JSON number was accepted.");
    }

    /// <summary>Verifies json deep nesting is rejected.</summary>
    private static void JsonDeepNestingIsRejected()
    {
        var json = new string('[', 5000) + "0" + new string(']', 5000);
        AssertThrows(() => LuminPackSerializer.DeserializeJson<int[]>(json, StrictJson.Utf8),
            "Deeply nested JSON exceeded the depth limit without an error.");
    }

    // Trailing separators were previously tolerated by the generic tokenizer; the strict
    // sequence check rejects them immediately, so no 100k-element skip walk can recurse.
    /// <summary>Verifies json many separators do not overflow stack.</summary>
    private static void JsonManySeparatorsDoNotOverflowStack()
    {
        var json = "[" + new string(',', 100_000) + "]";
        AssertThrows(() => LuminPackSerializer.DeserializeJson<int[]>(json, StrictJson.Utf8),
            "A stream of separators was accepted.");
    }

    /// <summary>Verifies assert throws.</summary>
    /// <param name="action">The action used by this operation.</param>
    /// <param name="message">The message describing the failure.</param>
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
