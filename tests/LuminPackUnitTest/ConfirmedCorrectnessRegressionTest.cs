using LuminPack;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Utility;
using LuminPack.Attribute;
using LuminPack.Option;
using System.Text;


namespace LuminPackUnitTest;

[LuminPackable]
public partial class GeneratorIdentifierModel
{
    public int @event;
    public string @class = string.Empty;
    public string 中文字段 = string.Empty;
}

[LuminPackable]
public partial class JsonFreshNestedModel
{
    public int Value = 91;
    public string? Name = "constructor-nested";
}

[LuminPackable]
public partial class JsonFreshScalarModel
{
    public int MissingNumber = 73;
    public long Value = 91;
}

[LuminPackable]
public partial class JsonFreshSmallModel
{
    public int MissingNumber = 73;
    public JsonFreshNestedModel? Nested = new();
}

[LuminPackable]
public partial class JsonFreshManagedNestedModel
{
    public int Value;
    public string? Name;
}

public class JsonFreshManagedBaseModel
{
    public string? Label;
    public List<int>? Numbers;
}

[LuminPackable]
public partial class JsonFreshManagedDerivedModel : JsonFreshManagedBaseModel
{
    public JsonFreshManagedNestedModel? Nested;
    public Dictionary<string, long>? Values;
}

public class JsonFreshInitializerBaseModel
{
    public string? BaseText = "base-initializer";
}

[LuminPackable]
public partial class JsonFreshInitializerDerivedModel : JsonFreshInitializerBaseModel
{
    public string? Text;
}

[LuminPackable]
public partial class JsonFreshExplicitConstructorModel
{
    public static int ConstructorCalls;
    public string? Text;

    public JsonFreshExplicitConstructorModel()
    {
        ConstructorCalls++;
        Text = "constructor";
    }
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
        RunCase(results, nameof(JsonFreshResultPreservesMissingFieldSemantics), JsonFreshResultPreservesMissingFieldSemantics);
        RunCase(results, nameof(JsonFreshResultPublishesOnlyAfterSuccess), JsonFreshResultPublishesOnlyAfterSuccess);
        RunCase(results, nameof(JsonFreshResultHonorsConstructorProof), JsonFreshResultHonorsConstructorProof);
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

    private static void JsonFreshResultPreservesMissingFieldSemantics()
    {
        const string json = "{\"Nested\":{\"Value\":17}}";
        const string scalarJson = "{\"Value\":17}";

        var utf8 = LuminPackSerializer.DeserializeJson<JsonFreshSmallModel>(json);
        var utf16 = LuminPackSerializer.DeserializeJson<JsonFreshSmallModel>(json,
            new LuminPackSerializerOption { StringEncoding = LuminPackStringEncoding.UTF16 });
        var scalar = LuminPackSerializer.DeserializeJson<JsonFreshScalarModel>(scalarJson);

        Assert(utf8 is not null && utf8.MissingNumber == 0 && utf8.Nested is not null &&
               utf8.Nested.Value == 17 && utf8.Nested.Name is null,
            "UTF-8 direct-result JSON changed missing-field or nested-parser semantics.");
        Assert(utf16 is not null && utf16.MissingNumber == 0 && utf16.Nested is not null &&
               utf16.Nested.Value == 17 && utf16.Nested.Name is null,
            "UTF-16 direct-result JSON changed missing-field or nested-parser semantics.");
        Assert(scalar is not null && scalar.MissingNumber == 0 && scalar.Value == 17,
            "Scalar direct-result JSON changed missing-field semantics.");
    }

    private static void JsonFreshResultPublishesOnlyAfterSuccess()
    {
        ReadOnlySpan<byte> json = Encoding.UTF8.GetBytes("{\"Value\":true}");
        using var state = new LuminPackReaderOptionalState();
        var reader = new LuminPackJsonReader(ref json, state);
        Assert(reader.Read(), "JSON direct-result publication test could not read its first token.");

        var original = new JsonFreshManagedNestedModel { Value = 1234 };
        JsonFreshManagedNestedModel? value = original;
        var threw = false;
        try
        {
            LuminPackParseProvider.Cache<JsonFreshManagedNestedModel>.Parser!.DeserializeJson(
                ref reader, ref value);
        }
        catch (FormatException)
        {
            threw = true;
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }

        Assert(threw, "Malformed JSON unexpectedly completed direct-result parsing.");
        Assert(ReferenceEquals(value, original) && original.Value == 1234,
            "Direct-result JSON published a partially initialized instance after an exception.");
    }

    private static void JsonFreshResultHonorsConstructorProof()
    {
        const string managedJson =
            "{\"Label\":\"safe\",\"Numbers\":[1,2,3],\"Nested\":{\"Value\":17,\"Name\":\"Nested\"},\"Values\":[[\"score\",99]]}";
        var managed = LuminPackSerializer.DeserializeJson<JsonFreshManagedDerivedModel>(managedJson);
        Assert(managed is not null && managed.Label == "safe" &&
               managed.Numbers is not null && managed.Numbers.SequenceEqual([1, 2, 3]) &&
               managed.Nested is not null && managed.Nested.Value == 17 && managed.Nested.Name == "Nested" &&
               managed.Values is not null && managed.Values["score"] == 99,
            "Managed-reference direct-result JSON did not preserve inherited/nested/collection fields.");

        var missingManaged = LuminPackSerializer.DeserializeJson<JsonFreshManagedDerivedModel>("{}");
        Assert(missingManaged is not null && missingManaged.Label is null && missingManaged.Numbers is null &&
               missingManaged.Nested is null && missingManaged.Values is null,
            "Managed-reference direct-result JSON changed missing-property defaults.");

        var initialized = LuminPackSerializer.DeserializeJson<JsonFreshInitializerDerivedModel>("{}");
        Assert(initialized is not null && initialized.BaseText is null && initialized.Text is null,
            "Base initializer control escaped the temp-first JSON path.");

        JsonFreshExplicitConstructorModel.ConstructorCalls = 0;
        ReadOnlySpan<byte> malformedJson = Encoding.UTF8.GetBytes("{\"Text\":true}");
        using var state = new LuminPackReaderOptionalState();
        var reader = new LuminPackJsonReader(ref malformedJson, state);
        Assert(reader.Read(), "Explicit-constructor JSON control could not read its first token.");
        JsonFreshExplicitConstructorModel? explicitValue = null;
        try
        {
            LuminPackParseProvider.Cache<JsonFreshExplicitConstructorModel>.Parser!
                .DeserializeJson(ref reader, ref explicitValue);
        }
        catch (FormatException)
        {
        }
        catch (InvalidOperationException)
        {
        }

        Assert(JsonFreshExplicitConstructorModel.ConstructorCalls == 0 && explicitValue is null,
            "Explicit constructor ran before malformed JSON parsing completed.");
        explicitValue = LuminPackSerializer.DeserializeJson<JsonFreshExplicitConstructorModel>("{}");
        Assert(JsonFreshExplicitConstructorModel.ConstructorCalls == 1 &&
               explicitValue is not null && explicitValue.Text is null,
            "Explicit-constructor temp-first JSON changed missing-property semantics.");
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
