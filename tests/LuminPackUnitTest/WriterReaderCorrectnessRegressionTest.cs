using System.Buffers;
using LuminPack;
using LuminPack.Option;
using LuminPack.Attribute;

namespace LuminPackUnitTest;

[LuminPackable]
public partial class LargeArrayWriterModel
{
    public int Prefix;
    public int[] Values = [];
    public int Suffix;
}

internal static class WriterReaderCorrectnessRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(MultiSegmentUnmanagedSequenceHasNoSegmentGaps),
            MultiSegmentUnmanagedSequenceHasNoSegmentGaps);
        RunCase(results, nameof(LargeUtf8StringGrowsOnlyOnSlowPath), LargeUtf8StringGrowsOnlyOnSlowPath);
        RunCase(results, nameof(NullAndLargeUnmanagedArraysUseOneSafeBulkCopy),
            NullAndLargeUnmanagedArraysUseOneSafeBulkCopy);
    }

    private static void NullAndLargeUnmanagedArraysUseOneSafeBulkCopy()
    {
        int[]? nullArray = null;
        var nullRoundTrip = LuminPackSerializer.Deserialize<int[]?>(LuminPackSerializer.Serialize(nullArray));
        Assert(nullRoundTrip is null, "A null unmanaged array did not round-trip as null.");

        var values = Enumerable.Range(0, 200_000).ToArray();
        var direct = LuminPackSerializer.Deserialize<int[]>(LuminPackSerializer.Serialize(values));
        Assert(direct is not null && direct.SequenceEqual(values),
            "A large direct unmanaged array exceeded the initial writer buffer.");

        var model = new LargeArrayWriterModel { Prefix = 17, Values = values, Suffix = 29 };
        var generated = LuminPackSerializer.Deserialize<LargeArrayWriterModel>(LuminPackSerializer.Serialize(model));
        Assert(generated is not null && generated.Prefix == 17 && generated.Suffix == 29 &&
               generated.Values.SequenceEqual(values),
            "A generated large unmanaged array overwrote adjacent fields or exceeded writer capacity.");
    }

    private static void LargeUtf8StringGrowsOnlyOnSlowPath()
    {
        var value = new GeneratorIdentifierModel
        {
            @event = 42,
            @class = new string('界', 200_000),
            中文字段 = "tail"
        };
        var option = new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF8,
            StringRecording = LuminPackStringRecording.Length
        };

        var payload = LuminPackSerializer.Serialize(value, option);
        var roundTrip = LuminPackSerializer.Deserialize<GeneratorIdentifierModel>(payload, option);
        Assert(roundTrip is not null && roundTrip.@class == value.@class && roundTrip.中文字段 == "tail",
            "A UTF-8 string larger than the initial writer buffer did not grow and round-trip correctly.");
    }

    private static void MultiSegmentUnmanagedSequenceHasNoSegmentGaps()
    {
        var first = new SequenceSegment<int>(new[] { 1, 2, 3 });
        var second = first.Append(new[] { 4, 5 });
        var third = second.Append(new[] { 6, 7, 8, 9 });
        var sequence = new ReadOnlySequence<int>(first, 0, third, third.Memory.Length);

        var payload = LuminPackSerializer.Serialize(sequence);
        var roundTrip = LuminPackSerializer.Deserialize<ReadOnlySequence<int>>(payload);

        Assert(roundTrip.ToArray().SequenceEqual(Enumerable.Range(1, 9)),
            "Multi-segment unmanaged ReadOnlySequence payload contained segment gaps or overwritten bytes.");
    }

    private sealed class SequenceSegment<T> : ReadOnlySequenceSegment<T>
    {
        internal SequenceSegment(ReadOnlyMemory<T> memory) => Memory = memory;

        internal SequenceSegment<T> Append(ReadOnlyMemory<T> memory)
        {
            var next = new SequenceSegment<T>(memory)
            {
                RunningIndex = RunningIndex + Memory.Length
            };
            Next = next;
            return next;
        }

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
