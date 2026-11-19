#if NET10_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.Tracing;
using LuminPack;
using Xunit;
using StrictUtf8 = LuminPack.Option.LuminPackOptions<LuminPack.Option.LuminPackUtf8LengthOptions, LuminPack.Option.LuminPackGeneralFormat, LuminPack.Option.LuminPackDefaultDepth, LuminPack.Option.LuminPackStrictJson>;

namespace LuminPackUnitTest;

/// <summary>Checks JSON key seeds when a concurrent dictionary changes during scratch rental.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ConcurrentDictionaryJsonSnapshotTest
{
    /// <summary>Bounds key seeding and still looks up values by key after a concurrent change.</summary>
    /// <param name="derived">Whether the target uses the public fallback traversal.</param>
    /// <param name="grow">Whether the target gains entries instead of losing them.</param>
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void ReuseToleratesGrowthAndShrinkageBeforeKeyTraversal(bool derived, bool grow)
    {
        ConcurrentDictionary<string, string> map = derived ? new DerivedMap() : new ConcurrentDictionary<string, string>();
        var alias = map;
        string existing = new string("stable".AsSpan());
        map[grow ? "old" : "incoming"] = existing;
        byte[] wire = """{"incoming":"stable","second":"new"}"""u8.ToArray();
        using var mutation = new ScratchMutation(() =>
        {
            if (grow)
            {
                for (int index = 0; index < 80; index++)
                    alias["added" + index] = "extra";
                alias["incoming"] = existing;
            }
            else
                alias.Clear();
        });

        LuminPackSerializer.DeserializeJson<ConcurrentDictionary<string, string>, StrictUtf8>(wire, ref map);

        Assert.True(mutation.WasApplied);
        Assert.Same(alias, map);
        Assert.Equal(2, alias.Count);
        Assert.Equal("stable", alias["incoming"]);
        Assert.Equal("new", alias["second"]);
        if (grow)
            Assert.Same(existing, alias["incoming"]);
        else
            Assert.NotSame(existing, alias["incoming"]);
    }

    private sealed class DerivedMap : ConcurrentDictionary<string, string>;

    // The rent event runs synchronously after Count is read. Only the staging pool for this pair type can trigger the mutation.
    private sealed class ScratchMutation(Action mutate) : EventListener
    {
        private static readonly int PoolId = ArrayPool<KeyValuePair<string, string>>.Shared.GetHashCode();
        private int _wasApplied;

        internal bool WasApplied => _wasApplied != 0;

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "System.Buffers.ArrayPoolEventSource")
                EnableEvents(source, EventLevel.Verbose);
        }

        protected override void OnEventWritten(EventWrittenEventArgs data)
        {
            if (data.EventName == "BufferRented" && data.Payload is { Count: > 2 } payload &&
                payload[2] is int poolId && poolId == PoolId && Interlocked.Exchange(ref _wasApplied, 1) == 0)
                mutate();
        }
    }
}
#endif
