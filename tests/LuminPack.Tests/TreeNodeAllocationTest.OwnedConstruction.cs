using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public sealed partial class TreeNodeAllocationTest
{
    /// <summary>Rejects invalid complete-buffer counts before reading elements or renting ordering storage.</summary>
    [Fact]
    public void FrameworkHashConstructionChecksItsBufferRange()
    {
        long[] items = [7, 11];
        Assert.Equal("LuminPackException", Assert.ThrowsAny<Exception>(() => LuminTreeStorage.CreateFrameworkImmutableHashSet(items, -1)).GetType().Name);
        Assert.Equal("LuminPackException", Assert.ThrowsAny<Exception>(() => LuminTreeStorage.CreateFrameworkImmutableHashSet(items, 3)).GetType().Name);
        Assert.Same(ImmutableHashSet<long>.Empty, LuminTreeStorage.CreateFrameworkImmutableHashSet(items, 0));
        Assert.Equal(new long[] { 7 }, LuminTreeStorage.CreateFrameworkImmutableHashSet(items, 1).ToArray());
        Assert.Equal(new long[] { 7, 11 }, items);
    }

    /// <summary>Requires duplicate and colliding framework sets to allocate only their final owner and reachable nodes.</summary>
    /// <param name="count">The decoded element count around collision-list rotation boundaries.</param>
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(12)]
    [InlineData(17)]
    [InlineData(65)]
    [InlineData(257)]
    public void FrameworkHashBucketsAllocateOnlyTheirResult(int count)
    {
        var gate = typeof(LuminTreeStorage).GetNestedType("GroupedHashLayout", BindingFlags.NonPublic)!;
        Assert.True((bool)gate.GetField("Available", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!);
        for (int shape = 0; shape < 3; shape++)
        {
            long[] items = Enumerable.Range(0, count).Select(index =>
            {
                int value = shape == 1 ? index % 3 : index;
                return ((long)value << 32) | (uint)(shape == 2 ? value ^ (index % 3) : value);
            }).Append(long.MinValue).ToArray();
            long[] original = (long[])items.Clone();
            long[] input = items.Take(count).ToArray();
            var reference = ImmutableHashSet.CreateRange(input);
            Func<object> build = () => LuminTreeStorage.CreateFrameworkImmutableHashSet(items, count);
            var result = (ImmutableHashSet<long>)build();
            Assert.Equal(reference.ToArray(), result.ToArray());

            object root = typeof(ImmutableHashSet<long>).GetField("_root", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(result)!;
            object bucket = root.GetType().GetField("_value", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(root)!;
            object tail = bucket.GetType().GetField("_additionalElements", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(bucket)!;
            long ownerBytes = MinimumAllocation(() => RuntimeHelpers.GetUninitializedObject(result.GetType()));
            long hashBytes = MinimumAllocation(() => RuntimeHelpers.GetUninitializedObject(root.GetType()));
            long listBytes = MinimumAllocation(() => RuntimeHelpers.GetUninitializedObject(tail.GetType()));
            int hashes = input.Select(item => item.GetHashCode()).Distinct().Count();
            long expected = ownerBytes + hashes * hashBytes + (reference.Count - hashes) * listBytes;
            Assert.Equal(expected, MinimumAllocation(build));
            if (count == 2)
            {
                Assert.Equal(expected, MinimumAllocation(() => LuminTreeStorage.CreateFrameworkImmutableHashSet(items[0], items[1])));
                Assert.Equal(reference.ToArray(), LuminTreeStorage.CreateFrameworkImmutableHashSet(items[0], items[1]).ToArray());
            }

            var changed = result;
            var expectedChanged = reference;
            for (int step = 0; step < 16; step++)
            {
                long item = input[(step * 37) % count];
                changed = (step & 1) == 0 ? changed.Remove(item) : changed.Add(item);
                expectedChanged = (step & 1) == 0 ? expectedChanged.Remove(item) : expectedChanged.Add(item);
                Assert.Equal(expectedChanged.ToArray(), changed.ToArray());
            }
            Assert.Equal(original, items);
            Assert.Equal(reference.ToArray(), result.ToArray());
        }
    }

    /// <summary>Checks ordered prefixes with missing left or right children around each small tree height.</summary>
    [Fact]
    public void ImmutableSortedTreesLinkOnlyTheRequestedPrefix()
    {
        var records = Enumerable.Range(0, 66).Select(i => new TreeRecord { Id = i }).ToArray();
        var pairs = records.Select(record => KeyValuePair.Create(record.Id, record)).ToArray();
        for (int count = 1; count < records.Length; count++)
        {
            var set = LuminTreeStorage.CreateImmutableSortedSet(records, count);
            var map = LuminTreeStorage.CreateImmutableSortedDictionary(pairs, count);
            Assert.Equal(count, set.Count);
            Assert.Equal(count, map.Count);
            for (int i = 0; i < count; i++)
            {
                Assert.Same(records[i], set[i]);
                Assert.Same(records[i], map[i]);
            }
            Assert.DoesNotContain(records[count], set);
            Assert.DoesNotContain(count, map.Keys);
            Assert.Same(records[count], set.Add(records[count]).Remove(records[0]).Max);
            Assert.Same(records[count], map.Add(count, records[count]).Remove(0)[count]);
            Assert.Same(records[0], set.Min);
            Assert.Same(records[0], map[0]);
        }
    }

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
