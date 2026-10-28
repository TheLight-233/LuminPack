using System.Collections.Immutable;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects checked hash-tree copying across leaf shapes, collisions and element storage.</summary>
public sealed class ImmutableHashTraversalTest
{
    /// <summary>Preserves the collection's enumeration order with either distinct or colliding hash codes.</summary>
    /// <param name="count">The number of distinct items.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(31)]
    [InlineData(64)]
    [InlineData(1000)]
    public void SetOrderMatchesEnumeration(int count)
    {
        int[] values = Enumerable.Range(0, count).Select(index => index * 7919 - 50000).ToArray();
        foreach (var comparer in new IEqualityComparer<int>[] { EqualityComparer<int>.Default, new CollisionComparer() })
        {
            var set = ImmutableHashSet.CreateRange(comparer, Enumerable.Reverse(values));
            var output = new int[count + 2];
            output[0] = output[^1] = int.MinValue;
            LuminTreeStorage.CopyTo(set, output.AsSpan(1, count));
            Assert.Equal(set.ToArray(), output.AsSpan(1, count).ToArray());
            Assert.Equal(int.MinValue, output[0]);
            Assert.Equal(int.MinValue, output[^1]);
        }
    }

    /// <summary>Preserves reference-bearing buckets and null values without allocating replacement elements.</summary>
    [Fact]
    public void ManagedBucketsKeepTheirOriginalReferences()
    {
        var keys = Enumerable.Range(-48, 97).ToArray();
        var map = keys.ToImmutableDictionary(key => key, key => key % 7 == 0 ? null : new Payload(key), new CollisionComparer());
        var output = new KeyValuePair<int, Payload?>[map.Count + 2];
        LuminTreeStorage.CopyTo(map, output.AsSpan(1, map.Count));
        var expected = map.ToArray();
        for (int index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index].Key, output[index + 1].Key);
            Assert.Same(expected[index].Value, output[index + 1].Value);
        }
        Assert.Equal(default, output[0]);
        Assert.Equal(default, output[^1]);
    }

    /// <summary>Retains checked destination stores and their already copied prefix when storage is too short.</summary>
    [Fact]
    public void ShortDestinationsKeepTheirBoundsAndWrittenPrefix()
    {
        var values = Enumerable.Range(0, 127).Select(index => index * 31).ToArray();
        foreach (var comparer in new IEqualityComparer<int>[] { EqualityComparer<int>.Default, new CollisionComparer() })
        {
            var set = ImmutableHashSet.CreateRange(comparer, values);
            int[] expected = set.ToArray();
            foreach (int length in new[] { 0, 1, 15, 63, 126 })
            {
                var output = Enumerable.Repeat(int.MinValue, 129).ToArray();
                Assert.Throws<IndexOutOfRangeException>(() => LuminTreeStorage.CopyTo(set, output.AsSpan(1, length)));
                Assert.Equal(expected[..length], output.AsSpan(1, length).ToArray());
                Assert.Equal(int.MinValue, output[0]);
                Assert.All(output.Skip(length + 1), item => Assert.Equal(int.MinValue, item));
                Assert.Equal(expected, set.ToArray());
            }
        }
    }

    /// <summary>Copies complete wide values without narrowing their destination storage.</summary>
    [Fact]
    public void WideValuesKeepEveryField()
    {
        var set = Enumerable.Range(0, 97).Select(index => new Wide(index, ~index, (long)index << 36, long.MinValue + index)).ToImmutableHashSet();
        var output = new Wide[set.Count];
        LuminTreeStorage.CopyTo(set, output);
        Assert.Equal(set.ToArray(), output);
    }

    /// <summary>Exercises a fixed hash bucket and its collision tree.</summary>
    private sealed class CollisionComparer : IEqualityComparer<int>
    {
        /// <summary>Compares the original integer identities.</summary>
        /// <param name="left">The first identity.</param>
        /// <param name="right">The second identity.</param>
        /// <returns>Whether the identities match.</returns>
        public bool Equals(int left, int right) => left == right;

        /// <summary>Maps several identities to each bucket while retaining a hash tree.</summary>
        /// <param name="value">The original identity.</param>
        /// <returns>A deliberately colliding hash code.</returns>
        public int GetHashCode(int value) => value % 7;
    }

    /// <summary>Provides reference storage whose identity must survive copying.</summary>
    private sealed class Payload
    {
        internal readonly int Value;

        /// <summary>Initializes the reference payload.</summary>
        /// <param name="value">The retained value.</param>
        internal Payload(int value) => Value = value;
    }

    /// <summary>Provides storage wider than individual fixed 32-bit accesses.</summary>
    private readonly record struct Wide(long First, long Second, long Third, long Fourth);
}
