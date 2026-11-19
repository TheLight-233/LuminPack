#if LUMINPACK_NEXT_GENERATOR
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using LuminPack;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Covers collection invariants from the Nino regression probes.</summary>
public sealed class NinoCollectionRegressionTest
{
    [Fact]
    public void SetKeepsFirstOccurrencesInFreshAndReusedTargets()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new[] { 1, 2, 3, 4, 5, 6, 7, 3, 1 });
        var reused = new HashSet<int> { -1, -2, -3 };
        var original = reused;
        LuminPackSerializer.Deserialize(bytes, ref reused);
        Assert.Same(original, reused);

        ISet<int> asSet = new HashSet<int> { -1 };
        IReadOnlySet<int> asReadOnlySet = new HashSet<int> { -1 };
        var originalSet = asSet;
        var originalReadOnlySet = asReadOnlySet;
        LuminPackSerializer.Deserialize(bytes, ref asSet);
        LuminPackSerializer.Deserialize(bytes, ref asReadOnlySet);
        Assert.Same(originalSet, asSet);
        Assert.Same(originalReadOnlySet, asReadOnlySet);

        var fresh = LuminPackSerializer.Deserialize<HashSet<int>>(bytes);
        foreach (var set in new[] { fresh, reused, (HashSet<int>)asSet, (HashSet<int>)asReadOnlySet })
        {
            Assert.Equal(7, set.Count);
            Assert.Equal(Enumerable.Range(1, 7), set);
            Assert.False(set.Add(3));
            Assert.True(set.Remove(3));
            Assert.False(set.Contains(3));
            Assert.True(set.Add(3));
        }
    }

    [Fact]
    public void SetCompactsDuplicateStringsAndNullElements()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new string?[] { null, "a", "a", "b", null, "c" });
        var set = new HashSet<string?> { "old" };
        LuminPackSerializer.Deserialize(bytes, ref set);

        Assert.Equal(4, set.Count);
        Assert.Equal(new string?[] { null, "a", "b", "c" }, set);
        Assert.True(set.Remove(null));
        Assert.False(set.Contains(null));
        Assert.True(set.Remove("a"));
        Assert.False(set.Contains("a"));
    }

    [Fact]
    public void ObservableRefillPublishesResetAndEachNewElement()
    {
        var target = new ObservableCollection<int> { -1 };
        var original = target;
        var actions = new List<(NotifyCollectionChangedAction Action, int Count)>();
        target.CollectionChanged += (_, args) => actions.Add((args.Action, original.Count));

        byte[] bytes = LuminPackSerializer.Serialize(new ObservableCollection<int> { 7, 8 });
        LuminPackSerializer.Deserialize(bytes, ref target);

        Assert.Same(original, target);
        Assert.Equal(new[] { 7, 8 }, target);
        Assert.Equal(new[]
        {
            (NotifyCollectionChangedAction.Reset, 0),
            (NotifyCollectionChangedAction.Add, 1),
            (NotifyCollectionChangedAction.Add, 2)
        }, actions);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(33)]
    [InlineData(257)]
    public void BitArrayRefillInvalidatesAnExistingEnumerator(int length)
    {
        var target = new BitArray(length, true);
        var original = target;
        var enumerator = target.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        byte[] bytes = LuminPackSerializer.Serialize(new BitArray(length));
        LuminPackSerializer.Deserialize(bytes, ref target);

        Assert.Same(original, target);
        Assert.Equal(length, target.Length);
        Assert.All(target.Cast<bool>(), bit => Assert.False(bit));
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }
}
#endif
