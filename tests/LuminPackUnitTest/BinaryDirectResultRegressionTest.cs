using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;


namespace LuminPackUnitTest;

[LuminPackable]
public partial class BinaryDirectNestedModel
{
    public int Value;
    public List<int>? Numbers;
    public Dictionary<int, string>? Labels;
}

[LuminPackable]
public partial class BinaryDirectEnvelope<T>
{
    public long Sequence;
    public T? Payload;
    public int[]? Samples;
}

[LuminPackable]
public partial class BinaryDirectRootModel
{
    public int Id;
    public BinaryDirectNestedModel? Nested;
    public BinaryDirectEnvelope<BinaryDirectNestedModel>? Envelope;
    public string? Tail;
}

internal static class BinaryDirectResultRegressionTest
{
    public static void Run(List<string> results)
    {
        RunCase(results, nameof(SmallNestedGenericAndFreshCollectionsRoundTrip),
            SmallNestedGenericAndFreshCollectionsRoundTrip);
        RunCase(results, nameof(PartiallyReadFreshResultIsNotPublished),
            PartiallyReadFreshResultIsNotPublished);
    }

    private static void SmallNestedGenericAndFreshCollectionsRoundTrip()
    {
        var nested = new BinaryDirectNestedModel
        {
            Value = 17,Numbers = [1, 2, 3, 5, 8],
            Labels = new Dictionary<int, string> { [1] = "one", [2] = "two" }
        };
        var value = new BinaryDirectRootModel
        {
            Id = 42,Nested = nested,
            Envelope = new BinaryDirectEnvelope<BinaryDirectNestedModel>
            {Sequence = 99,
                Payload = new BinaryDirectNestedModel
                {
                    Value = 23,Numbers = [13, 21],
                    Labels = new Dictionary<int, string> { [3] = "three" }
                },
                Samples = [4, 9, 16, 25]
            },
            Tail = "complete"
        };

        var payload = LuminPackSerializer.Serialize(value);
        var result = LuminPackSerializer.Deserialize<BinaryDirectRootModel>(payload);

        Assert(result is not null && result.Id == 42 && result.Tail == "complete",
            "Small direct-result DTO did not round-trip scalar fields.");
        Assert(result.Nested is not null && result.Nested.Value == 17 &&result.Nested.Numbers is not null && result.Nested.Numbers.SequenceEqual([1, 2, 3, 5, 8]) &&result.Nested.Labels is not null && result.Nested.Labels[2] == "two",
            "named nested direct-result DTO did not round-trip fresh collections.");
        Assert(result.Envelope is not null && result.Envelope.Sequence == 99 &&result.Envelope.Samples is not null && result.Envelope.Samples.SequenceEqual([4, 9, 16, 25]) &&result.Envelope.Payload is not null && result.Envelope.Payload.Value == 23 &&result.Envelope.Payload.Numbers is not null && result.Envelope.Payload.Numbers.SequenceEqual([13, 21]),
            "Generic direct-result DTO on fresh unmanaged array did not round-trip.");
    }

    private static void PartiallyReadFreshResultIsNotPublished()
    {
        var payload = LuminPackSerializer.Serialize(new BinaryDirectRootModel
        {
            Id = 7,Nested = new BinaryDirectNestedModel { Value = 11 },
            Envelope = new BinaryDirectEnvelope<BinaryDirectNestedModel>
            {Sequence = 13,
                Payload = new BinaryDirectNestedModel { Value = 17 },
                Samples = [19, 23]
            },
            Tail = "must-throw"
        });
        Array.Resize(ref payload, payload.Length - 1);

        var original = new BinaryDirectRootModel { Id = 1234, Tail = "original" };
        BinaryDirectRootModel? target = original;
        ReadOnlySpan<byte> span = payload;
        var reader = new LuminPackReader(ref span);
        var threw = false;
        try
        {
            global::LuminPack.Generated.LuminPackExtensions_LuminPackUnitTest
                .ReadValue(ref reader, ref target);
        }
        catch (Exception)
        {
            threw = true;
        }

        Assert(threw, "Truncated direct-result payload unexpectedly completed.");
        Assert(ReferenceEquals(target, original) && original.Id == 1234 && original.Tail == "original",
            "Binary direct-result parsing published a partially initialized object after failure.");
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
            results.Add($"✗ {name} - FAILED: {ex.Message}");
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
