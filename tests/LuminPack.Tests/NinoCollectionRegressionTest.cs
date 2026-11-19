#if LUMINPACK_NEXT_GENERATOR
extern alias NinoLimits;

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using LuminPack;
using LuminPack.Utility;
using Xunit;
using Framed = NinoLimits::LuminPack.Tests.InputLimits.FramedGroupingRegressionFixture;

namespace LuminPackUnitTest;

/// <summary>Covers collection invariants and grouping boundaries from the Nino regression probes.</summary>
public sealed class NinoCollectionRegressionTest
{
    [Fact]
    public void DefaultGroupingKeepsTheLegacyWire()
    {
        IGrouping<int, int> group = new LuminPackGrouping<int, int>(7, new[] { 9 });
        byte[] expected = [7, 0, 0, 0, 1, 0, 0, 0, 9, 0, 0, 0];
        Assert.Equal(expected, LuminPackSerializer.Serialize(group));
        var result = LuminPackSerializer.Deserialize<IGrouping<int, int>>(expected);
        Assert.Equal(7, result.Key);
        Assert.Equal(new[] { 9 }, result);
        Assert.Equal(new byte[] { 255 }, LuminPackSerializer.Serialize((IGrouping<int, int>?)null));
    }

    [Fact]
    public void FramedGroupingDistinguishesNullGroupAndNullKey()
    {
        IGrouping<string?, int> group = new LuminPackGrouping<string?, int>(null, new[] { 9 });
        byte[] expected = [0, 255, 255, 255, 255, 1, 0, 0, 0, 9, 0, 0, 0];
        Assert.Equal(expected, Framed.WriteGrouping(group));
        var result = Framed.ReadGrouping(expected);
        Assert.NotNull(result);
        Assert.Null(result.Key);
        Assert.Equal(new[] { 9 }, result);

        Assert.Equal(new byte[] { 255 }, Framed.WriteGrouping(null));
        Assert.Null(Framed.ReadGrouping([255]));
        Assert.Throws<InvalidDataException>(() => Framed.ReadGrouping([1]));
        Assert.ThrowsAny<Exception>(() => Framed.ReadGrouping([0]));

        var lookup = new[] { 9 }.ToLookup(_ => (string?)null);
        byte[] lookupBytes = [1, 0, 0, 0, .. expected];
        Assert.Equal(lookupBytes, Framed.WriteLookup(lookup));
        Assert.Equal(new[] { 9 }, Framed.ReadLookup(lookupBytes)[null]);
        Assert.Equal(new[] { 9 }, Framed.ReadLookup([2, 0, 0, 0, 255, .. expected])[null]);
        Assert.Throws<ArgumentException>(() => Framed.ReadLookup([2, 0, 0, 0, .. expected, .. expected]));
    }

    [Fact]
    public void FramedGroupingReadsRawKeyAndNestedBoundaries()
    {
        var raw = Framed.RoundTripRawKey(new LuminPackGrouping<byte, int>(255, new[] { 9 }));
        Assert.Equal((byte)255, raw.Key);
        Assert.Equal(new[] { 9 }, raw);

        var nested = Framed.RoundTripMember(new LuminPackGrouping<string?, int>(null, new[] { 4 }));
        Assert.NotNull(nested.Group);
        Assert.Null(nested.Group.Key);
        Assert.Equal(new[] { 4 }, nested.Group);
        Assert.Equal(71, nested.Tail);

        var empty = Framed.RoundTripMember(null);
        Assert.Null(empty.Group);
        Assert.Equal(71, empty.Tail);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void LookupPreservesNullKeyAndItsPosition(int nullIndex)
    {
        var words = new List<string?> { "one", "two" };
        words.Insert(nullIndex, null);
        var lookup = words.ToLookup(word => word, word => word?.Length ?? 42);

        byte[] binary = Framed.WriteLookup(lookup);
        var binaryResult = Framed.ReadLookup(binary);
        Assert.Equal(binary, Framed.WriteLookup(binaryResult));
        string json = LuminPackSerializer.SerializeJson(lookup);
        var jsonResult = LuminPackSerializer.DeserializeJson<ILookup<string?, int>>(json);
        foreach (var result in new[] { binaryResult, jsonResult })
        {
            Assert.Equal(3, result.Count);
            Assert.Equal(words, result.Select(group => group.Key));
            Assert.True(result.Contains(null));
            Assert.Equal(new[] { 42 }, result[null]);
            Assert.Equal(new[] { 3 }, result["one"]);
            Assert.Empty(result["miss"]);
        }
    }

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
