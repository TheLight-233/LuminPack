using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects checked red-black traversal order, partial prefixes, reference identity and public fallbacks.</summary>
public sealed class RedBlackTraversalTest
{
    /// <summary>Checks primitive and wide raw element storage at every tested complete and short destination.</summary>
    [Fact]
    public void RawSetsKeepEveryCheckedPrefixAndSliceGuard()
    {
        foreach (int count in new[] { 0, 1, 2, 3, 5, 16, 33, 129 })
        {
            var integers = new SortedSet<int>(Enumerable.Range(0, count).Select(index => index * 17 - 3000));
            Check(integers.ToArray(), (Span<int> destination) => LuminTreeStorage.CopyTo(integers, destination), int.MaxValue);
            var guids = new SortedSet<Guid>(Enumerable.Range(0, count).Select(index => new Guid(index, 17, 29, 3, 5, 7, 11, 13, 17, 19, 23)));
            Check(guids.ToArray(), (Span<Guid> destination) => LuminTreeStorage.CopyTo(guids, destination), Guid.Empty);
        }
    }

    /// <summary>Keeps the original object references without calling the comparer during traversal.</summary>
    [Fact]
    public void ReferenceSetsKeepIdentityAndAvoidComparisonCallbacks()
    {
        int comparisons = 0;
        var comparer = Comparer<Item>.Create((left, right) => { comparisons++; return left.Value.CompareTo(right.Value); });
        var values = Enumerable.Range(0, 65).Select(index => new Item { Value = index }).ToArray();
        var set = new SortedSet<Item>(values, comparer);
        int before = comparisons;
        var sentinel = new Item { Value = -1 };
        Check(values, (Span<Item> destination) => LuminTreeStorage.CopyTo(set, destination), sentinel);
        Assert.Equal(before, comparisons);
        var copied = new Item[values.Length];
        LuminTreeStorage.CopyTo(set, copied);
        for (int index = 0; index < values.Length; index++) Assert.Same(values[index], copied[index]);
    }

    /// <summary>Preserves reference-bearing and wide pair storage through the same checked traversal.</summary>
    [Fact]
    public void DictionariesKeepPairOrderReferencesAndCheckedPrefixes()
    {
        foreach (int count in new[] { 0, 1, 2, 7, 31, 63 })
        {
            var referencePairs = new SortedDictionary<int, Item>(Enumerable.Range(0, count).ToDictionary(index => index * 7, index => new Item { Value = index }));
            Check(referencePairs.ToArray(), (Span<KeyValuePair<int, Item>> destination) => LuminTreeStorage.CopyTo(referencePairs, destination), default);
            var widePairs = new SortedDictionary<long, Guid>(Enumerable.Range(0, count).ToDictionary(index => index * 7919L, index => new Guid(index, 17, 29, 3, 5, 7, 11, 13, 17, 19, 23)));
            Check(widePairs.ToArray(), (Span<KeyValuePair<long, Guid>> destination) => LuminTreeStorage.CopyTo(widePairs, destination), default);
        }
    }

    /// <summary>Retains the established enumerator fallback for derived collections and range views.</summary>
    [Fact]
    public void DerivedCollectionsAndViewsKeepTheirPublicFallback()
    {
        var derived = new DerivedSet { 17, -3, 29, 5 };
        Check(derived.ToArray(), (Span<int> destination) => LuminTreeStorage.CopyTo(derived, destination), int.MaxValue);
        var parent = new SortedSet<int>(Enumerable.Range(0, 50));
        var view = parent.GetViewBetween(11, 29);
        Check(view.ToArray(), (Span<int> destination) => LuminTreeStorage.CopyTo(view, destination), int.MaxValue);
        var dictionary = new DerivedDictionary { [-3] = "a", [17] = "b", [29] = "c" };
        Check(dictionary.ToArray(), (Span<KeyValuePair<int, string>> destination) => LuminTreeStorage.CopyTo(dictionary, destination), default);
    }

    /// <summary>Copies to a bounded slice without allocating an adapter around a ref-struct span.</summary>
    /// <typeparam name="T">The original element storage.</typeparam>
    /// <param name="destination">The complete destination window.</param>
    private delegate void Copier<T>(Span<T> destination);

    /// <summary>Checks complete output, spare capacity and every truncated prefix without weakening the failure type.</summary>
    /// <typeparam name="T">The exact element storage.</typeparam>
    /// <param name="expected">The public enumeration order.</param>
    /// <param name="copy">The actual runtime copy entry.</param>
    /// <param name="sentinel">The value outside each destination slice.</param>
    private static void Check<T>(T[] expected, Copier<T> copy, T sentinel)
    {
        var full = Enumerable.Repeat(sentinel, expected.Length + 7).ToArray();
        copy(full.AsSpan(3, expected.Length + 2));
        Assert.True(full.AsSpan(3, expected.Length).SequenceEqual(expected));
        Assert.All(full[..3], value => Assert.Equal(sentinel, value));
        Assert.All(full[(expected.Length + 3)..], value => Assert.Equal(sentinel, value));
        for (int length = 0; length < expected.Length; length++)
        {
            var shortBuffer = Enumerable.Repeat(sentinel, expected.Length + 7).ToArray();
            Assert.Throws<IndexOutOfRangeException>(() => copy(shortBuffer.AsSpan(3, length)));
            Assert.True(shortBuffer.AsSpan(3, length).SequenceEqual(expected.AsSpan(0, length)));
            Assert.All(shortBuffer[..3], value => Assert.Equal(sentinel, value));
            Assert.All(shortBuffer[(length + 3)..], value => Assert.Equal(sentinel, value));
        }
    }

    /// <summary>Provides a reference identity whose comparison is supplied by the source collection.</summary>
    private sealed class Item
    {
        internal int Value;
    }

    /// <summary>Exercises the existing derived-set fallback.</summary>
    private sealed class DerivedSet : SortedSet<int>;

    /// <summary>Exercises the existing derived-dictionary fallback.</summary>
    private sealed class DerivedDictionary : SortedDictionary<int, string>;
}
