using System.Buffers;
using System.Collections.Immutable;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects approved immutable reference staging, original element bytes and cleanup.</summary>
public class ImmutableListWriteTest
{
    /// <summary>Preserves all four profiles and complete records across empty, small and growing output.</summary>
    [Fact]
    public void ProfilesNullsAndGrowthKeepOriginalElementBytes()
    {
        CheckProfile<LuminPackUtf8LengthOptions>();
        CheckProfile<LuminPackUtf8TokenOptions>();
        CheckProfile<LuminPackUtf16LengthOptions>();
        CheckProfile<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Compares independent array element framing with the staged immutable writer.</summary>
    /// <typeparam name="TProfile">The original compile-time binary profile.</typeparam>
    private static void CheckProfile<TProfile>() where TProfile : struct, ILuminPackOptions
    {
        foreach (int count in new[] { 0, 1, 17, 257 })
        {
            string?[] values = Enumerable.Range(0, count).Select(index => index % 7 == 0 ? null :
                new string((char)('a' + index % 20), index % 2 == 0 ? 1024 : 37)).ToArray();
            ImmutableList<string?> source = ImmutableList.Create(values);
            Assert.Equal(LuminPackSerializer.Serialize<TProfile>(values), LuminPackSerializer.Serialize<TProfile>(source));
            ImmutableList<string?>? target = null;
            byte[] bytes = LuminPackSerializer.Serialize<TProfile>(source);
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<TProfile>(bytes, ref target));
            Assert.Equal(default(TProfile).StringRecording == LuminPackStringRecording.Token
                ? values.Select(value => value ?? string.Empty) : values, target);
        }
        ImmutableList<string>? absent = null;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, LuminPackSerializer.Serialize<TProfile>(absent));
    }

    /// <summary>Keeps alias repetition, future-object mutations and the originally selected immutable root.</summary>
    [Fact]
    public void SharedObjectsCallbacksAndSourceReplacementKeepOrder()
    {
        var first = new ImmutableWriteNode { Id = 1, Text = "first" };
        var next = new ImmutableWriteNode { Id = 2, Text = "before" };
        ImmutableList<ImmutableWriteNode> source = ImmutableList.Create(first, next, first);
        var expected = source.ToArray();
        var calls = new List<int>();
        ImmutableWriteNode.BeforeWrite = value =>
        {
            calls.Add(value.Id);
            if (value.Id == 1)
            {
                next.Text = "after";
                source = source.SetItem(1, new ImmutableWriteNode { Id = 999 });
            }
        };
        try
        {
            byte[] bytes = LuminPackSerializer.Serialize(source);
            Assert.Equal(new[] { 1, 2, 1 }, calls);
            ImmutableWriteNode.BeforeWrite = null;
            Assert.Equal(LuminPackSerializer.Serialize(expected), bytes);
            Assert.Equal(999, source[1].Id);
        }
        finally { ImmutableWriteNode.BeforeWrite = null; }
    }

    /// <summary>Preserves caller exception identity and clears the complete shared rental on failure.</summary>
    [Fact]
    public void CallbackFailurePreservesExceptionAndClearsTheSharedRental()
    {
        var sentinel = new InvalidOperationException("caller failure");
        var source = ImmutableList.Create(new ImmutableWriteNode { Id = 1 }, new ImmutableWriteNode { Id = 2 }, new ImmutableWriteNode { Id = 3 });
        var calls = new List<int>();
        ImmutableWriteNode.BeforeWrite = value =>
        {
            calls.Add(value.Id);
            if (value.Id == 2) throw sentinel;
        };
        try
        {
            Assert.Same(sentinel, Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Serialize(source)));
            Assert.Equal(new[] { 1, 2 }, calls);
            AssertClearedRental();
        }
        finally { ImmutableWriteNode.BeforeWrite = null; }
    }

    /// <summary>Clears the shared rental when complete string reservation rejects a short fixed output.</summary>
    [Fact]
    public void CapacityFailureClearsStagingWithoutPublishingTheCursor()
    {
        var source = ImmutableList.Create(new ImmutableWriteNode { Id = 1, Text = new string('x', 10000) },
            new ImmutableWriteNode { Id = 2 }, new ImmutableWriteNode { Id = 3 });
        Span<byte> storage = new byte[128];
        var writer = new LuminPackWriter(ref storage);
        Exception? failure = null;
        try { LuminPackSerializer.Serialize(ref writer, source); }
        catch (Exception exception) { failure = exception; }
        Assert.NotNull(failure);
        Assert.Equal(0, writer.Position);
        AssertClearedRental();
    }

    /// <summary>Inspects the same shared element bucket without retaining its returned storage.</summary>
    private static void AssertClearedRental()
    {
        var rental = ArrayPool<ImmutableWriteNode>.Shared.Rent(3);
        try { Assert.All(rental, Assert.Null); }
        finally { ArrayPool<ImmutableWriteNode>.Shared.Return(rental, true); }
    }
}

/// <summary>Exposes observable serialization callbacks on ordinary staged reference elements.</summary>
[LuminPackable]
public sealed partial class ImmutableWriteNode
{
    public int Id;
    public string Text = string.Empty;
    public static Action<ImmutableWriteNode>? BeforeWrite;

    /// <summary>Runs the caller's observer at the original serialization boundary.</summary>
    [LuminPackOnSerializing]
    public void OnSerializing() => BeforeWrite?.Invoke(this);
}
