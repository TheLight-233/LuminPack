using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public sealed partial class TreeNodeAllocationTest
{
    /// <summary>Requires warmed distinct-key construction to allocate only the returned owner and its reachable nodes.</summary>
    /// <param name="count">The number of distinct ordered keys.</param>
    [Theory]
    [InlineData(2)]
    [InlineData(17)]
    public void FreshImmutableTreesAllocateOnlyTheirResult(int count)
    {
        int[] items = Enumerable.Range(0, count).Select(index => index * 7919 - 50000).ToArray();
        var pairs = items.Select(item => new KeyValuePair<int, int>(item, item * 3)).ToArray();
        var factories = new (string Layout, Type[] Arguments, Func<object> Build)[]
        {
            ("ImmutableSortedSetLayout`1", [typeof(int)], () => LuminTreeStorage.CreateImmutableSortedSet(items, count)),
            ("ImmutableHashSetLayout`1", [typeof(int)], () => LuminTreeStorage.CreateImmutableHashSet(items, count)),
            ("ImmutableSortedDictionaryLayout`2", [typeof(int), typeof(int)], () => LuminTreeStorage.CreateImmutableSortedDictionary(pairs, count)),
            ("ImmutableDictionaryLayout`2", [typeof(int), typeof(int)], () => LuminTreeStorage.CreateImmutableDictionary(pairs, count)),
        };
        foreach (var factory in factories)
        {
            Assert.True((bool)Layout(factory.Layout, factory.Arguments, "Available"));
            object result = factory.Build();
            Type ownerType = result.GetType();
            object root = ownerType.GetField("_root", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(result)!;
            Type nodeType = root.GetType();
            // Derive sizes from the actual framework types rather than assuming pointer widths or BCL field layouts.
            long ownerBytes = MinimumAllocation(() => RuntimeHelpers.GetUninitializedObject(ownerType));
            long nodeBytes = MinimumAllocation(() => RuntimeHelpers.GetUninitializedObject(nodeType));
            Assert.Equal(ownerBytes + count * nodeBytes, MinimumAllocation(factory.Build));
            object other = factory.Build();
            Assert.NotSame(result, other);
            if (result is IImmutableSet<int> set)
            {
                var changed = set.Remove(items[0]).Add(int.MaxValue);
                Assert.Equal(count, changed.Count);
                Assert.False(changed.Contains(items[0]));
                Assert.True(set.Contains(items[0]));
                Assert.False(set.Contains(int.MaxValue));
                Assert.True(((IImmutableSet<int>)other).SetEquals(items));
            }
            else
            {
                var map = (IImmutableDictionary<int, int>)result;
                var changed = map.SetItem(items[0], 999).Remove(items[^1]);
                Assert.Equal(count - 1, changed.Count);
                Assert.Equal(items[0] * 3, map[items[0]]);
                Assert.True(map.ContainsKey(items[^1]));
                Assert.Equal(pairs.OrderBy(pair => pair.Key), ((IImmutableDictionary<int, int>)other).OrderBy(pair => pair.Key));
            }
        }
        Assert.Empty(ImmutableSortedSet<int>.Empty);
        Assert.Empty(ImmutableHashSet<int>.Empty);
        Assert.Empty(ImmutableSortedDictionary<int, int>.Empty);
        Assert.Empty(ImmutableDictionary<int, int>.Empty);
    }

    /// <summary>Measures warmed allocations, excluding delegates, probes and pool initialization.</summary>
    /// <param name="create">The operation whose returned object must escape.</param>
    /// <returns>The smallest per-call allocation from three complete batches.</returns>
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MinimumAllocation(Func<object> create)
    {
        const int iterations = 64;
        for (int index = 0; index < iterations; index++) GC.KeepAlive(create());
        long minimum = long.MaxValue;
        for (int sample = 0; sample < 3; sample++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < iterations; index++) GC.KeepAlive(create());
            minimum = Math.Min(minimum, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        Assert.Equal(0, minimum % iterations);
        return minimum / iterations;
    }
}
