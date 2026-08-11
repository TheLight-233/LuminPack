using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

internal static class BufferResizeColdPathRegressionTest
{
    private const int GrowthBlockLength = 230_000;

    public static void Run(List<string> results)
    {
        RunCase(results, nameof(CapacityPolicyUses32KiBAndFiftyPercentGrowth),
            CapacityPolicyUses32KiBAndFiftyPercentGrowth);
        RunCase(results, nameof(BinaryCheckPreservesBytesAcrossMultipleGrowths),
            BinaryCheckPreservesBytesAcrossMultipleGrowths);
        RunCase(results, nameof(LazyInitialAllocationOccursOnceAndPreservesBytes),
            LazyInitialAllocationOccursOnceAndPreservesBytes);
        RunCase(results, nameof(JsonCheckPreservesValidPayloadAcrossMultipleGrowths),
            JsonCheckPreservesValidPayloadAcrossMultipleGrowths);
        RunCase(results, nameof(LargeGeneratedStringAndArrayRoundTripAfterGrowth),
            LargeGeneratedStringAndArrayRoundTripAfterGrowth);
    }

    private static void CapacityPolicyUses32KiBAndFiftyPercentGrowth()
    {
        using var buffer = new LuminBufferWriter(true);
        Assert(buffer.TotalLength == 32 * 1024,
            "The native writer should start at 32 KiB instead of retaining 256 KiB per pooled instance.");

        var initialCapacity = buffer.TotalLength;
        buffer.EnsureCapacity(initialCapacity + 1);
        Assert(buffer.TotalLength == initialCapacity * 3 / 2,
            "The first capacity expansion did not use the configured 1.5x growth factor.");

        var firstGrowthCapacity = buffer.TotalLength;
        buffer.EnsureCapacity(firstGrowthCapacity + 1);
        Assert(buffer.TotalLength == firstGrowthCapacity * 3 / 2,
            "Repeated capacity expansion did not preserve the configured 1.5x growth factor.");

        using var largeReserve = new LuminBufferWriter(false);
        largeReserve.EnsureCapacity(1_000_000);
        Assert(largeReserve.TotalLength >= 1_000_000 && largeReserve.TotalLength <= 1_170_000,
            "A direct large reservation retained excessive headroom.");
    }

    private static void LazyInitialAllocationOccursOnceAndPreservesBytes()
    {
        using var buffer = new LuminBufferWriter(false);
        Assert(!buffer.UseFirstBuffer && buffer.TotalLength == 0,
            "A lazy buffer unexpectedly allocated storage in its constructor.");

        var first = buffer.GetFullSpan();
        Assert(buffer.UseFirstBuffer && first.Length > 0,
            "GetFullSpan did not perform the lazy initial allocation.");
        first[0] = 0x6D;
        var firstCapacity = buffer.TotalLength;

        var second = buffer.GetFullSpan();
        Assert(buffer.TotalLength == firstCapacity && second[0] == 0x6D,
            "An existing buffer was reallocated or cleared by a later GetFullSpan call.");
    }

    private static void BinaryCheckPreservesBytesAcrossMultipleGrowths()
    {
        using var buffer = new LuminBufferWriter(true);
        var initialCapacity = buffer.TotalLength;
        var writer = new LuminPackWriter(buffer);

        WriteBlockAndCheck(ref writer, GrowthBlockLength, 0x3A);
        var firstGrowthCapacity = buffer.TotalLength;
        WriteBlockAndCheck(ref writer, GrowthBlockLength, 0xC7);
        var secondGrowthCapacity = buffer.TotalLength;

        Assert(firstGrowthCapacity > initialCapacity,
            "The first binary threshold crossing did not grow the buffer.");
        Assert(secondGrowthCapacity > firstGrowthCapacity,
            "The second binary threshold crossing did not grow the buffer.");

        var payload = writer.GetSpan();
        Assert(payload.Length == GrowthBlockLength * 2,
            "Binary growth published an unexpected byte count.");
        Assert(AllEqual(payload[..GrowthBlockLength], 0x3A) &&
               AllEqual(payload[GrowthBlockLength..], 0xC7),
            "Binary growth lost or changed bytes written before a native-buffer relocation.");
    }

    private static void JsonCheckPreservesValidPayloadAcrossMultipleGrowths()
    {
        using var buffer = new LuminBufferWriter(true);
        var state = new LuminPackWriterOptionalState(new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF8
        });
        var writer = new LuminPackJsonWriter(buffer, state);
        var first = new string('A', GrowthBlockLength);
        var second = new string('B', GrowthBlockLength);
        var initialCapacity = buffer.TotalLength;

        writer.WriteArrayStart();
        writer.WriteString(first);
        buffer.Check(ref writer);
        var firstGrowthCapacity = buffer.TotalLength;
        writer.WriteString(second);
        buffer.Check(ref writer);
        var secondGrowthCapacity = buffer.TotalLength;
        writer.WriteArrayEnd();

        Assert(firstGrowthCapacity > initialCapacity,
            "The first JSON threshold crossing did not grow the buffer.");
        Assert(secondGrowthCapacity > firstGrowthCapacity,
            "The second JSON threshold crossing did not grow the buffer.");

        using var document = JsonDocument.Parse(writer.GetSpan().ToArray());
        var values = document.RootElement.EnumerateArray().ToArray();
        Assert(values.Length == 2 && values[0].GetString() == first && values[1].GetString() == second,
            "JSON growth corrupted a string written before or after a native-buffer relocation.");
    }

    private static void LargeGeneratedStringAndArrayRoundTripAfterGrowth()
    {
        var value = new MultiGrowthPayload
        {
            Prefix = new string('界', 100_000),
            Values = Enumerable.Range(0, 200_000).ToArray(),
            Suffix = new string('Z', 230_000)
        };
        var option = new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF8,
            StringRecording = LuminPackStringRecording.Length
        };

        using var buffer = new LuminBufferWriter(true);
        buffer.Option.StringEncoding = option.StringEncoding;
        buffer.Option.StringRecording = option.StringRecording;
        LuminPackSerializer.Serialize(value, buffer);

        Assert(buffer.CurrentIndex > 1_000_000 && buffer.TotalLength > 1_000_000,
            "The generated binary payload did not cross the expected growth boundaries.");
        var result = LuminPackSerializer.Deserialize<MultiGrowthPayload>(buffer.GetSpan(), option);
        Assert(result is not null && result.Prefix == value.Prefix && result.Suffix == value.Suffix &&
               result.Values.SequenceEqual(value.Values),
            "A generated payload containing both a large string and a large array failed after growth.");
    }

    private static void WriteBlockAndCheck(ref LuminPackWriter writer, int count, byte value)
    {
        writer.EnsureAdditionalCapacity(count);
        MemoryMarshal.CreateSpan(ref writer.GetCurrentSpanReference(), count).Fill(value);
        writer.Advance(count);
        writer.CheckBuffer();
    }

    private static bool AllEqual(ReadOnlySpan<byte> bytes, byte expected)
    {
        foreach (var value in bytes)
        {
            if (value != expected)
                return false;
        }

        return true;
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

[LuminPackable]
public sealed partial class MultiGrowthPayload
{
    public string Prefix { get; set; } = string.Empty;
    public int[] Values { get; set; } = [];
    public string Suffix { get; set; } = string.Empty;
}
