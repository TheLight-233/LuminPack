using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;

namespace LuminPackUnitTest;

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

    private static void JsonStrayNumberInObjectDoesNotHang()
    {
        var result = LuminPackSerializer.DeserializeJson<StrayNumberProbeModel>("{\"Age\":1, 5}");
        Assert(result?.Age == 1, "A stray number in a JSON object corrupted the parsed result.");
    }

    private static void JsonCircleRefDrainDoesNotHang()
    {
        var result = LuminPackSerializer.DeserializeJson<CircleRefJsonProbe>("{\"$id\":1,\"Name\":\"a\",5,\"$ref\":1}");
        Assert(result?.Name == "a", "A stray number in a JSON object corrupted the circle-reference drain.");
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
        AssertThrows(() => LuminPackSerializer.DeserializeJson<int[]>(json),
            "Deeply nested JSON exceeded the depth limit without an error.");
    }

    private static void JsonManySeparatorsDoNotOverflowStack()
    {
        var json = "[" + new string(',', 100_000) + "]";
        var value = LuminPackSerializer.DeserializeJson<int[]>(json);
        Assert(value is not null,
            "A stream of separators did not terminate cleanly without a stack overflow.");
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
