using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackUnitTest;

internal static class SerializerReentrancyTest
{
    private static readonly LuminPackSerializerOption OuterOption =
        LuminPackSerializerOption.Token with { StringEncoding = LuminPackStringEncoding.UTF16 };

    public static void Run(List<string> results)
    {
        RunCase(results, nameof(BinaryCallbacksCanReenterSerializer), BinaryCallbacksCanReenterSerializer);
        RunCase(results, nameof(JsonCallbacksCanReenterSerializer), JsonCallbacksCanReenterSerializer);
        RunCase(results, nameof(PublicDefaultMutationDoesNotChangeImplicitDefaults),
            PublicDefaultMutationDoesNotChangeImplicitDefaults);
    }

    private static void BinaryCallbacksCanReenterSerializer()
    {
        var value = new ReentrantSerializerModel { Value = "outer-亮度-😀" };
        var payload = LuminPackSerializer.Serialize(value, OuterOption);
        var result = LuminPackSerializer.Deserialize<ReentrantSerializerModel>(payload, OuterOption);

        Assert(result?.Value == value.Value,
            "A nested callback serialization corrupted the outer binary serializer state.");
    }

    private static void JsonCallbacksCanReenterSerializer()
    {
        var value = new ReentrantSerializerModel { Value = "outer-json-亮度-😀" };
        var json = LuminPackSerializer.SerializeJson(value, OuterOption);
        var result = LuminPackSerializer.DeserializeJson<ReentrantSerializerModel>(json, OuterOption);

        Assert(result?.Value == value.Value,
            "A nested callback serialization corrupted the outer JSON serializer state.");
    }

    private static void PublicDefaultMutationDoesNotChangeImplicitDefaults()
    {
        var expected = LuminPackSerializer.Serialize("stable-default");
        var shared = LuminPackSerializerOption.Default;
        var originalEncoding = shared.StringEncoding;
        var originalRecording = shared.StringRecording;
        var originalFormat = shared.StandardFormat;

        try
        {
            shared.StringEncoding = LuminPackStringEncoding.UTF16;
            shared.StringRecording = LuminPackStringRecording.Token;
            var actual = LuminPackSerializer.Serialize("stable-default");
            Assert(actual.AsSpan().SequenceEqual(expected),
                "Mutating the public Default instance changed option-less serialization globally.");
        }
        finally
        {
            shared.StringEncoding = originalEncoding;
            shared.StringRecording = originalRecording;
            shared.StandardFormat = originalFormat;
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

[LuminPackable]
public sealed class ReentrantSerializerModel
{
    private static readonly byte[] NestedPayload = LuminPackSerializer.Serialize("nested-reader-state");

    public string? Value { get; set; }

    [LuminPackOnSerializing]
    public void OnSerializing()
    {
        _ = LuminPackSerializer.Serialize("nested-writer-state");
    }

    [LuminPackOnDeserializing]
    public static void OnDeserializing()
    {
        _ = LuminPackSerializer.Deserialize<string>(NestedPayload);
    }
}
