using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Utility;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackUnitTest;

[LuminPackable]
public partial class GeneratorIdentifierModel
{
    public int @event;
    public string @class = string.Empty;
    public string 中文字段 = string.Empty;
}

internal static class ConfirmedCorrectnessRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(ReusedCollectionsGrowBeforeDirectFill), ReusedCollectionsGrowBeforeDirectFill);
        RunCase(results, nameof(SerializingListDoesNotInvalidateEnumerator), SerializingListDoesNotInvalidateEnumerator);
        RunCase(results, nameof(Utf8CharSpanMatchesStringEncoding), Utf8CharSpanMatchesStringEncoding);
        RunCase(results, nameof(MarshalMemoryHelpersPreserveData), MarshalMemoryHelpersPreserveData);
        RunCase(results, nameof(BufferWriterGetMemoryHonorsWrittenRange), BufferWriterGetMemoryHonorsWrittenRange);
        RunCase(results, nameof(GeneratorHandlesKeywordsAndUnicodeNames), GeneratorHandlesKeywordsAndUnicodeNames);
    }

    private static void ReusedCollectionsGrowBeforeDirectFill()
    {
        var expected = Enumerable.Range(1, 8).ToArray();

        var list = new List<int>(1) { 99 };
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(expected.ToList()).AsSpan(), ref list);
        Assert(list.SequenceEqual(expected), "A reused List with insufficient capacity was truncated.");

        var queue = new Queue<int>(1);
        queue.Enqueue(99);
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new Queue<int>(expected)).AsSpan(), ref queue);
        Assert(queue.SequenceEqual(expected), "A reused Queue with insufficient capacity was not expanded.");

        var sourceStack = new Stack<int>(expected);
        var stack = new Stack<int>(1);
        stack.Push(99);
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(sourceStack).AsSpan(), ref stack);
        Assert(stack.SequenceEqual(sourceStack), "A reused Stack with insufficient capacity was not expanded.");
    }

    private static void SerializingListDoesNotInvalidateEnumerator()
    {
        var list = new List<int> { 1, 2, 3 };
        var enumerator = list.GetEnumerator();
        Assert(enumerator.MoveNext(), "The test enumerator did not start.");

        _ = LuminPackSerializer.Serialize(list);

        Assert(enumerator.MoveNext() && enumerator.Current == 2,
            "Read-only List serialization changed the collection version.");
    }

    private static void Utf8CharSpanMatchesStringEncoding()
    {
        const string value = "LuminPack-中文-😀";
        Span<byte> stringBuffer = stackalloc byte[256];
        Span<byte> spanBuffer = stackalloc byte[256];
        var stringWriter = new LuminPackWriter(ref stringBuffer);
        var spanWriter = new LuminPackWriter(ref spanBuffer);

        var stringTokenBytes = stringWriter.WriteUtf8WithToken(0, value);
        var spanTokenBytes = spanWriter.WriteUtf8WithToken(0, value.AsSpan());
        Assert(stringTokenBytes == spanTokenBytes &&
               stringBuffer[..(stringTokenBytes + 1)].SequenceEqual(spanBuffer[..(spanTokenBytes + 1)]),
            "ReadOnlySpan<char> token encoding did not produce UTF-8 bytes.");

        stringBuffer.Clear();
        spanBuffer.Clear();
        var stringLengthBytes = stringWriter.WriteUtf8WithLength(0, value);
        var spanLengthBytes = spanWriter.WriteUtf8WithLength(0, value.AsSpan());
        Assert(stringLengthBytes == spanLengthBytes &&
               stringBuffer[..(stringLengthBytes + 8)].SequenceEqual(spanBuffer[..(spanLengthBytes + 8)]),
            "ReadOnlySpan<char> length-prefixed encoding did not match the string overload.");
    }

    private static unsafe void MarshalMemoryHelpersPreserveData()
    {
        byte[] left = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11];
        byte[] right = [21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31];
        var originalLeft = left.ToArray();
        var originalRight = right.ToArray();

        fixed (byte* leftPtr = left)
        fixed (byte* rightPtr = right)
            LuminPackMarshal.SwapMemory(leftPtr, rightPtr, left.Length);

        Assert(left.SequenceEqual(originalRight) && right.SequenceEqual(originalLeft),
            "SwapMemory lost bytes in its non-8-byte tail.");

        var threw = false;
        try
        {
            LuminPackMarshal.ProcessMemoryBlocks(null, null, 1, 0, static (_, _, _) => { });
        }
        catch (ArgumentOutOfRangeException)
        {
            threw = true;
        }

        Assert(threw, "ProcessMemoryBlocks accepted a zero block size and could loop forever.");
    }

    private static void BufferWriterGetMemoryHonorsWrittenRange()
    {
        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize("written-range", writer);
        var expected = writer.GetSpan()[2..].ToArray();

        Assert(writer.GetMemory(2).Span.SequenceEqual(expected),
            "GetMemory ignored its index or exposed unwritten capacity.");
    }

    private static void GeneratorHandlesKeywordsAndUnicodeNames()
    {
        var value = new GeneratorIdentifierModel
        {
            @event = 17,
            @class = "keyword",
            中文字段 = "中文值"
        };

        var binary = LuminPackSerializer.Deserialize<GeneratorIdentifierModel>(LuminPackSerializer.Serialize(value));
        Assert(binary is not null && binary.@event == value.@event &&
               binary.@class == value.@class && binary.中文字段 == value.中文字段,
            "Generated binary code did not handle escaped identifiers.");

        var jsonOption = new LuminPackSerializerOption { StringEncoding = LuminPackStringEncoding.UTF8 };
        using var jsonBuffer = new LuminBufferWriter(true);
        jsonBuffer.Option.StringEncoding = jsonOption.StringEncoding;
        jsonBuffer.Option.StringRecording = jsonOption.StringRecording;
        jsonBuffer.Option.StandardFormat = jsonOption.StandardFormat;
        LuminPackSerializer.SerializeJson(value, jsonBuffer);
        var json = System.Text.Encoding.UTF8.GetString(jsonBuffer.GetSpan());
        Assert(json.Contains("\"中文字段\"", StringComparison.Ordinal),
            "Generated UTF-8 JSON property bytes corrupted a Unicode member name.");

        var jsonRoundTrip = LuminPackSerializer.DeserializeJson<GeneratorIdentifierModel>(jsonBuffer);
        Assert(jsonRoundTrip is not null && jsonRoundTrip.@event == value.@event &&
               jsonRoundTrip.@class == value.@class && jsonRoundTrip.中文字段 == value.中文字段,
            "Generated JSON code did not handle escaped identifiers or Unicode names.");
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
