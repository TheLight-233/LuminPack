using System.Text;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;


namespace LuminPackUnitTest;

/// <summary>Provides generator identifier model functionality.</summary>
[LuminPackable]
public partial class GeneratorIdentifierModel
{
    /// <summary>Stores the event.</summary>
    public int @event;
    /// <summary>Stores the class.</summary>
    public string @class = string.Empty;
    /// <summary>Stores a value in the Unicode-name regression fixture.</summary>
    public string \u4e2d\u6587\u5b57\u6bb5 = string.Empty;
}

/// <summary>Provides json fresh nested model functionality.</summary>
[LuminPackable]
public partial class JsonFreshNestedModel
{
    /// <summary>Stores the value.</summary>
    public int Value = 91;
    /// <summary>Stores the name.</summary>
    public string? Name = "constructor-nested";
}

/// <summary>Provides json fresh scalar model functionality.</summary>
[LuminPackable]
public partial class JsonFreshScalarModel
{
    /// <summary>Stores the missing number.</summary>
    public int MissingNumber = 73;
    /// <summary>Stores the value.</summary>
    public long Value = 91;
}

/// <summary>Provides json fresh small model functionality.</summary>
[LuminPackable]
public partial class JsonFreshSmallModel
{
    /// <summary>Stores the missing number.</summary>
    public int MissingNumber = 73;
    /// <summary>Stores the nested.</summary>
    public JsonFreshNestedModel? Nested = new();
}

/// <summary>Provides json fresh managed nested model functionality.</summary>
[LuminPackable]
public partial class JsonFreshManagedNestedModel
{
    /// <summary>Stores the value.</summary>
    public int Value;
    /// <summary>Stores the name.</summary>
    public string? Name;
}

/// <summary>Provides json fresh managed base model functionality.</summary>
public class JsonFreshManagedBaseModel
{
    /// <summary>Stores the label.</summary>
    public string? Label;
    /// <summary>Stores the numbers.</summary>
    public List<int>? Numbers;
}

/// <summary>Provides json fresh managed derived model functionality.</summary>
[LuminPackable]
public partial class JsonFreshManagedDerivedModel : JsonFreshManagedBaseModel
{
    /// <summary>Stores the nested.</summary>
    public JsonFreshManagedNestedModel? Nested;
    /// <summary>Stores the values.</summary>
    public Dictionary<string, long>? Values;
}

/// <summary>Provides json fresh initializer base model functionality.</summary>
public class JsonFreshInitializerBaseModel
{
    /// <summary>Stores the base text.</summary>
    public string? BaseText = "base-initializer";
}

/// <summary>Provides json fresh initializer derived model functionality.</summary>
[LuminPackable]
public partial class JsonFreshInitializerDerivedModel : JsonFreshInitializerBaseModel
{
    /// <summary>Stores the text.</summary>
    public string? Text;
}

/// <summary>Provides json fresh explicit constructor model functionality.</summary>
[LuminPackable]
public partial class JsonFreshExplicitConstructorModel
{
    /// <summary>Stores the constructor calls.</summary>
    public static int ConstructorCalls;
    /// <summary>Stores the text.</summary>
    public string? Text;

    /// <summary>Initializes a new instance of <c>JsonFreshExplicitConstructorModel</c>.</summary>
    public JsonFreshExplicitConstructorModel()
    {
        ConstructorCalls++;
        Text = "constructor";
    }
}

/// <summary>Provides confirmed correctness regression test functionality.</summary>
internal static class ConfirmedCorrectnessRegressionTest
{
    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
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

    /// <summary>Verifies reused collections grow before direct fill.</summary>
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

    /// <summary>Verifies serializing list does not invalidate enumerator.</summary>
    private static void SerializingListDoesNotInvalidateEnumerator()
    {
        var list = new List<int> { 1, 2, 3 };
        var enumerator = list.GetEnumerator();
        Assert(enumerator.MoveNext(), "The test enumerator did not start.");

        _ = LuminPackSerializer.Serialize(list);

        Assert(enumerator.MoveNext() && enumerator.Current == 2,
            "Read-only List serialization changed the collection version.");
    }

    /// <summary>Verifies utf8 char span matches string encoding.</summary>
    private static void Utf8CharSpanMatchesStringEncoding()
    {
        const string value = "LuminPack-\u4E2D\u6587-\uD83D\uDE00";
        Span<byte> stringBuffer = stackalloc byte[256];
        Span<byte> spanBuffer = stackalloc byte[256];
        var stringWriter = new LuminPackWriter(ref stringBuffer);
        var spanWriter = new LuminPackWriter(ref spanBuffer);

        stringWriter.Write<LuminPackUtf8TokenOptions>(value);
        var expectedToken = System.Text.Encoding.UTF8.GetBytes(value + "\0");
        Assert(stringBuffer[..stringWriter.Position].SequenceEqual(expectedToken), "Complete UTF-8 token bytes changed.");
        spanWriter.Write<LuminPackUtf8LengthOptions>(value);
        var payload = System.Text.Encoding.UTF8.GetBytes(value);
        Assert(BitConverter.ToInt32(spanBuffer) == payload.Length && BitConverter.ToInt32(spanBuffer[4..]) == value.Length &&
            spanBuffer.Slice(8, payload.Length).SequenceEqual(payload), "Complete UTF-8 length bytes changed.");
    }

    /// <summary>Verifies marshal memory helpers preserve data.</summary>
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

    /// <summary>Verifies buffer writer get memory honors written range.</summary>
    private static void BufferWriterGetMemoryHonorsWrittenRange()
    {
        using var writer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize("written-range", writer);
        var expected = writer.GetSpan()[2..].ToArray();

        Assert(writer.GetMemory(2).Span.SequenceEqual(expected),
            "GetMemory ignored its index or exposed unwritten capacity.");
    }

    /// <summary>Verifies generator handles keywords and unicode names.</summary>
    private static void GeneratorHandlesKeywordsAndUnicodeNames()
    {
        var value = new GeneratorIdentifierModel
        {
            @event = 17,
            @class = "keyword",
            \u4e2d\u6587\u5b57\u6bb5 = "\u4E2D\u6587\u503C"
        };

        var binary = LuminPackSerializer.Deserialize<GeneratorIdentifierModel>(LuminPackSerializer.Serialize(value));
        Assert(binary is not null && binary.@event == value.@event &&
               binary.@class == value.@class && binary.\u4e2d\u6587\u5b57\u6bb5 == value.\u4e2d\u6587\u5b57\u6bb5,
            "Generated binary code did not handle escaped identifiers.");

        using var jsonBuffer = new LuminBufferWriter(true);
        LuminPackSerializer.SerializeJson<LuminPackUtf8LengthOptions>(value, jsonBuffer);
        var json = System.Text.Encoding.UTF8.GetString(jsonBuffer.GetSpan());
        Assert(json.Contains("\u0022\u4E2D\u6587\u5B57\u6BB5\u0022", StringComparison.Ordinal),
            "Generated UTF-8 JSON property bytes corrupted a Unicode member name.");

        var jsonRoundTrip = LuminPackSerializer.DeserializeJson<GeneratorIdentifierModel, LuminPackUtf8LengthOptions>(jsonBuffer);
        Assert(jsonRoundTrip is not null && jsonRoundTrip.@event == value.@event &&
               jsonRoundTrip.@class == value.@class && jsonRoundTrip.\u4e2d\u6587\u5b57\u6bb5 == value.\u4e2d\u6587\u5b57\u6bb5,
            "Generated JSON code did not handle escaped identifiers or Unicode names.");
    }

    /// <summary>Verifies json fresh result preserves missing field semantics.</summary>
    private static void JsonFreshResultPreservesMissingFieldSemantics()
    {
        const string json = "{\"Nested\":{\"Value\":17}}";
        const string scalarJson = "{\"Value\":17}";

        var utf8 = LuminPackSerializer.DeserializeJson<JsonFreshSmallModel>(json);
        var utf16 = LuminPackSerializer.DeserializeJson<JsonFreshSmallModel>(json,
            new TestSerializationProfile { StringEncoding = LuminPackStringEncoding.UTF16 });
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

    /// <summary>Verifies json fresh result publishes only after success.</summary>
    private static void JsonFreshResultPublishesOnlyAfterSuccess()
    {
        // The default JSON reader consumes UTF-16 bytes.
        ReadOnlySpan<byte> json = System.Runtime.InteropServices.MemoryMarshal.AsBytes("{\"Value\":true}".AsSpan());
        using var state = new LuminPackReaderOptionalState();
        var reader = new LuminPackJsonReader(ref json, state);
        Assert(reader.Read(), "JSON direct-result publication test could not read its first token.");

        var original = new JsonFreshManagedNestedModel { Value = 1234 };
        JsonFreshManagedNestedModel? value = original;
        var threw = false;
        try
        {
            reader.Read(ref value);
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

    /// <summary>Verifies json fresh result honors constructor proof.</summary>
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
        ReadOnlySpan<byte> malformedJson = System.Runtime.InteropServices.MemoryMarshal.AsBytes("{\"Text\":true}".AsSpan());
        using var state = new LuminPackReaderOptionalState();
        var reader = new LuminPackJsonReader(ref malformedJson, state);
        Assert(reader.Read(), "Explicit-constructor JSON control could not read its first token.");
        JsonFreshExplicitConstructorModel? explicitValue = null;
        try
        {
            reader.Read(ref explicitValue);
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
