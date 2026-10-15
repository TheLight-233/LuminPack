using System.Buffers;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;

namespace LuminPackUnitTest;

/// <summary>Provides large array writer model functionality.</summary>
[LuminPackable]
public partial class LargeArrayWriterModel
{
    /// <summary>Stores the prefix.</summary>
    public int Prefix;
    /// <summary>Stores the values.</summary>
    public int[] Values = [];
    /// <summary>Stores the suffix.</summary>
    public int Suffix;
}

/// <summary>Provides null header probe model functionality.</summary>
[LuminPackable]
public partial class NullHeaderProbeModel
{
    /// <summary>Stores the value.</summary>
    public int Value;
}

/// <summary>Provides writer reader correctness regression test functionality.</summary>
internal static class WriterReaderCorrectnessRegressionTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(MultiSegmentUnmanagedSequenceHasNoSegmentGaps),
            MultiSegmentUnmanagedSequenceHasNoSegmentGaps);
        RunCase(results, nameof(LargeUtf8StringGrowsOnlyOnSlowPath), LargeUtf8StringGrowsOnlyOnSlowPath);
        RunCase(results, nameof(NullAndLargeUnmanagedArraysUseOneSafeBulkCopy),
            NullAndLargeUnmanagedArraysUseOneSafeBulkCopy);
        RunCase(results, nameof(NullObjectHeadersAdvanceExactlyOnce),
            NullObjectHeadersAdvanceExactlyOnce);
    }

    /// <summary>Verifies null object headers advance exactly once.</summary>
    private static void NullObjectHeadersAdvanceExactlyOnce()
    {
        NullHeaderProbeModel? model = null;
        Uri? uri = null;
        System.Globalization.CultureInfo? culture = null;
        TimeZoneInfo? timeZone = null;
        Type? type = null;

        AssertSingleNullObjectHeader(model, "generated object");

        LuminBufferWriter buffer = LuminBufferWriterPool.Rent();
        try
        {
            var writer = new LuminPackWriter(buffer);
            global::LuminPack.Generated.LuminPackExtensions_LuminPackUnitTest.WriteValue(ref writer, in uri);
            AssertSingleNullObjectHeader(ref writer, "Uri formatter");

            writer = new LuminPackWriter(buffer);
            global::LuminPack.Generated.LuminPackExtensions_LuminPackUnitTest.WriteValue(ref writer, in culture);
            AssertSingleNullObjectHeader(ref writer, "CultureInfo formatter");

            writer = new LuminPackWriter(buffer);
            global::LuminPack.Generated.LuminPackExtensions_LuminPackUnitTest.WriteValue(ref writer, in timeZone);
            AssertSingleNullObjectHeader(ref writer, "TimeZoneInfo formatter");

            writer = new LuminPackWriter(buffer);
            global::LuminPack.Generated.LuminPackExtensions_LuminPackUnitTest.WriteValue(ref writer, in type);
            AssertSingleNullObjectHeader(ref writer, "Type formatter");
        }
        finally
        {
            LuminBufferWriterPool.Return(buffer);
        }

        List<NullHeaderProbeModel?> values =
        [
            null,
            new NullHeaderProbeModel { Value = 73 }
        ];
        List<NullHeaderProbeModel?> result = LuminPackSerializer.Deserialize<List<NullHeaderProbeModel?>>(
            LuminPackSerializer.Serialize(values));
        Assert(result is { Count: 2 } && result[0] is null && result[1]?.Value == 73,
            "A null generated object consumed the following collection element.");
    }

    /// <summary>Verifies assert single null object header.</summary>
    /// <param name="value">The value consumed or produced by this operation.</param>
    /// <param name="formatter">The formatter used by this operation.</param>
    private static void AssertSingleNullObjectHeader(NullHeaderProbeModel? value, string formatter)
    {
        byte[] payload = LuminPackSerializer.Serialize(in value);
        Assert(payload.Length == 1 && payload[0] == LuminPackCode.NullObject,
            $"The {formatter} wrote {payload.Length} bytes for a one-byte null object header.");
        Assert(LuminPackSerializer.Sizeof(value) == payload.Length,
            $"The {formatter} serializer and size evaluator disagree for null.");
    }

    /// <summary>Verifies assert single null object header.</summary>
    /// <param name="writer">The writer receiving the output data.</param>
    /// <param name="formatter">The formatter used by this operation.</param>
    private static void AssertSingleNullObjectHeader(ref LuminPackWriter writer, string formatter)
    {
        ReadOnlySpan<byte> payload = writer.GetSpan();
        Assert(payload.Length == 1 && payload[0] == LuminPackCode.NullObject,
            $"The {formatter} wrote {payload.Length} bytes for a one-byte null object header.");
    }

    /// <summary>Verifies null and large unmanaged arrays use one safe bulk copy.</summary>
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

    /// <summary>Verifies large utf8 string grows only on slow path.</summary>
    private static void LargeUtf8StringGrowsOnlyOnSlowPath()
    {
        var value = new GeneratorIdentifierModel
        {
            @event = 42,
            @class = new string('\u754c', 200_000),
            \u4e2d\u6587\u5b57\u6bb5 = "tail"
        };
        var option = new LuminPackSerializerOption
        {
            StringEncoding = LuminPackStringEncoding.UTF8,
            StringRecording = LuminPackStringRecording.Length
        };

        var payload = LuminPackSerializer.Serialize(value, option);
        var roundTrip = LuminPackSerializer.Deserialize<GeneratorIdentifierModel>(payload, option);
        Assert(roundTrip is not null && roundTrip.@class == value.@class && roundTrip.\u4e2d\u6587\u5b57\u6bb5 == "tail",
            "A UTF-8 string larger than the initial writer buffer did not grow and round-trip correctly.");
    }

    /// <summary>Verifies multi segment unmanaged sequence has no segment gaps.</summary>
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

    /// <summary>Provides sequence segment functionality.</summary>
    /// <typeparam name="T">The type represented by <c>T</c>.</typeparam>
    private sealed class SequenceSegment<T> : ReadOnlySequenceSegment<T>
    {
        /// <summary>Initializes a new instance of <c>SequenceSegment</c>.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        internal SequenceSegment(ReadOnlyMemory<T> memory) => Memory = memory;

        /// <summary>Verifies append.</summary>
        /// <param name="memory">The memory used by this operation.</param>
        /// <returns>The result of the operation.</returns>
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
