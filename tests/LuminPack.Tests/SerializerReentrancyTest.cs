using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;

namespace LuminPackUnitTest;

/// <summary>Provides serializer reentrancy test functionality.</summary>
internal static class SerializerReentrancyTest
{
    private static readonly TestSerializationProfile OuterOption =
        TestSerializationProfile.Token with { StringEncoding = LuminPackStringEncoding.UTF16 };

    /// <summary>Verifies run.</summary>
    /// <param name="results">The results used by this operation.</param>
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(BinaryCallbacksCanReenterSerializer), BinaryCallbacksCanReenterSerializer);
        RunCase(results, nameof(JsonCallbacksCanReenterSerializer), JsonCallbacksCanReenterSerializer);
        RunCase(results, nameof(ExplicitProfilesDoNotChangeImplicitDefaults),
            ExplicitProfilesDoNotChangeImplicitDefaults);
    }

    /// <summary>Verifies binary callbacks can reenter serializer.</summary>
    private static void BinaryCallbacksCanReenterSerializer()
    {
        var value = new ReentrantSerializerModel { Value = "outer-\u4EAE\u5EA6-\uD83D\uDE00" };
        var payload = LuminPackSerializer.Serialize(value, OuterOption);
        var result = LuminPackSerializer.Deserialize<ReentrantSerializerModel>(payload, OuterOption);

        Assert(result?.Value == value.Value,
            "A nested callback serialization corrupted the outer binary serializer state.");
    }

    /// <summary>Verifies json callbacks can reenter serializer.</summary>
    private static void JsonCallbacksCanReenterSerializer()
    {
        var value = new ReentrantSerializerModel { Value = "outer-json-\u4EAE\u5EA6-\uD83D\uDE00" };
        var json = LuminPackSerializer.SerializeJson(value, OuterOption);
        var result = LuminPackSerializer.DeserializeJson<ReentrantSerializerModel>(json, OuterOption);

        Assert(result?.Value == value.Value,
            "A nested callback serialization corrupted the outer JSON serializer state.");
    }

    /// <summary>Verifies that separate closed profiles cannot mutate the implicit default operation.</summary>
    private static void ExplicitProfilesDoNotChangeImplicitDefaults()
    {
        var expected = LuminPackSerializer.Serialize("stable-default");
        var alternate = LuminPackSerializer.Serialize<string, LuminPackUtf16TokenOptions>("stable-default");
        Assert(!alternate.AsSpan().SequenceEqual(expected), "The explicit profile did not select its own wire representation.");
        var actual = LuminPackSerializer.Serialize("stable-default");
        Assert(actual.AsSpan().SequenceEqual(expected), "An explicit profile changed implicit serialization defaults.");
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

/// <summary>Provides reentrant serializer model functionality.</summary>
[LuminPackable]
public sealed class ReentrantSerializerModel
{
    private static readonly byte[] NestedPayload = LuminPackSerializer.Serialize("nested-reader-state");

    /// <summary>Gets or sets the value.</summary>
    public string? Value { get; set; }

    /// <summary>Verifies on serializing.</summary>
    [LuminPackOnSerializing]
    public void OnSerializing()
    {
        _ = LuminPackSerializer.Serialize("nested-writer-state");
    }

    /// <summary>Verifies on deserializing.</summary>
    [LuminPackOnDeserializing]
    public static void OnDeserializing()
    {
        _ = LuminPackSerializer.Deserialize<string>(NestedPayload);
    }
}
