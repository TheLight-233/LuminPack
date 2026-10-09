using System.Numerics;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

// A merged run of consecutive mergeable fields mixes numeric literals with types
// whose size is only known to the runtime. The evaluated size used to drop the
// latter to zero because the length expression was parsed as an integer.
[LuminPackable]
public sealed partial class MergeTwoGuidsElement
{
    public Guid Id;
    public Guid Owner;
    public string Name = "";
}

[LuminPackable]
public sealed partial class MergeIntAndDateElement
{
    public int Count;
    public DateTime Stamp;
    public string Name = "";
}

[LuminPackable]
public sealed partial class MergeRuntimeSizedElement
{
    public Guid Id;
    public DateTime Created;
    public DateTimeOffset SavedAt;
    public TimeSpan Elapsed;
    public Vector2 Position;
    public decimal Amount;
    public string Name = "";
}

[LuminPackable]
public sealed partial class MergeSizeHolder
{
    public List<MergeTwoGuidsElement> Guids = new();
    public List<MergeIntAndDateElement> Mixed = new();
    public List<MergeRuntimeSizedElement> RuntimeSized = new();
    public MergeRuntimeSizedElement[] RuntimeSizedArray = Array.Empty<MergeRuntimeSizedElement>();
    public MergeTwoGuidsElement Single = new();
}

public class SizeEvaluationMergeTest
{
    [Fact]
    public void EvaluatedSizeMatchesSerializedLengthForMergedRuns()
    {
        var holder = new MergeSizeHolder
        {
            Guids = new List<MergeTwoGuidsElement>
            {
                new()
                {
                    Id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
                    Owner = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210"),
                    Name = "first",
                },
                new()
                {
                    Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
                    Owner = Guid.Parse("66666666-7777-8888-9999-000000000000"),
                    Name = "second",
                },
            },
            Mixed = new List<MergeIntAndDateElement>
            {
                new() { Count = 7, Stamp = new DateTime(2023, 11, 15, 6, 13, 20, DateTimeKind.Utc), Name = "a" },
                new() { Count = 9, Stamp = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), Name = "b" },
            },
            RuntimeSized = new List<MergeRuntimeSizedElement>
            {
                NewRuntimeSizedElement("first"),
                NewRuntimeSizedElement("second"),
            },
            RuntimeSizedArray = new[] { NewRuntimeSizedElement("array") },
            Single = new MergeTwoGuidsElement
            {
                Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Owner = Guid.Parse("99999999-8888-7777-6666-555555555555"),
                Name = "single",
            },
        };

        byte[] payload = LuminPackSerializer.Serialize(holder);
        int evaluated = LuminPackSerializer.Sizeof(holder);

        Assert.Equal(payload.Length, evaluated);

        var roundTrip = LuminPackSerializer.Deserialize<MergeSizeHolder>(payload);
        Assert.NotNull(roundTrip);
        Assert.Equal(holder.Guids.Count, roundTrip!.Guids.Count);
        Assert.Equal(holder.Guids[1].Owner, roundTrip.Guids[1].Owner);
        Assert.Equal(holder.Mixed[1].Stamp, roundTrip.Mixed[1].Stamp);
        Assert.Equal(holder.RuntimeSized[0].Position, roundTrip.RuntimeSized[0].Position);
        Assert.Equal(holder.RuntimeSized[0].SavedAt, roundTrip.RuntimeSized[0].SavedAt);
        Assert.Equal(holder.RuntimeSized[1].Elapsed, roundTrip.RuntimeSized[1].Elapsed);
        Assert.Equal(holder.RuntimeSizedArray[0].Amount, roundTrip.RuntimeSizedArray[0].Amount);
        Assert.Equal(holder.Single.Name, roundTrip.Single.Name);
    }

    [Fact]
    public void SingleMergeableFieldStaysAccurate()
    {
        // A run needs at least two mergeable fields; the lone-field shape must
        // keep working so the fix does not over-correct.
        var element = new MergeTwoGuidsElement
        {
            Id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            Owner = Guid.Parse("fedcba98-7654-3210-fedc-ba9876543210"),
            Name = "only",
        };

        Assert.Equal(LuminPackSerializer.Serialize(element).Length, LuminPackSerializer.Sizeof(element));
    }

    private static MergeRuntimeSizedElement NewRuntimeSizedElement(string name) => new()
    {
        Id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
        Created = new DateTime(2023, 11, 15, 6, 13, 20, DateTimeKind.Utc),
        SavedAt = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000).ToOffset(TimeSpan.FromHours(8)),
        Elapsed = TimeSpan.FromMilliseconds(1500.25),
        Position = new Vector2(1.5f, -2.25f),
        Amount = 1234.5678m,
        Name = name,
    };
}