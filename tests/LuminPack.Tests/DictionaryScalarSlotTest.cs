using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks in-place reuse of existing string and scalar values in int-keyed dictionaries against fresh reads.</summary>
public class DictionaryScalarSlotTest
{
    /// <summary>Checks matching keys refill the existing dictionary, keep its order and retain equal string instances.</summary>
    [Fact]
    public void MatchingKeysRefillExistingSlots()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, string?> { [1] = "one", [2] = null, [3] = new string('x', 40) });
        string kept = new string('x', 40);
        var map = new Dictionary<int, string?> { [1] = "old", [2] = "two", [3] = kept };
        var instance = map;
        LuminPackSerializer.Deserialize(bytes, ref map);
        Assert.Same(instance, map);
        Assert.Equal(new[] { 1, 2, 3 }, map.Keys);
        Assert.Equal("one", map[1]);
        Assert.Null(map[2]);
        Assert.Same(kept, map[3]);

        byte[] numbers = LuminPackSerializer.Serialize(new Dictionary<int, long> { [5] = -1, [6] = long.MaxValue });
        var existing = new Dictionary<int, long> { [5] = 0, [6] = 0 };
        LuminPackSerializer.Deserialize(numbers, ref existing);
        Assert.Equal(-1, existing[5]);
        Assert.Equal(long.MaxValue, existing[6]);
    }

    /// <summary>Checks reordered, missing, stale and additional keys produce the content of a fresh read.</summary>
    [Fact]
    public void ChangedShapesMatchFreshReads()
    {
        var source = new Dictionary<int, string> { [4] = "d", [1] = "a", [3] = "c" };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        foreach (var start in new[]
        {
            new Dictionary<int, string> { [1] = "x", [3] = "y", [4] = "z" },
            new Dictionary<int, string> { [4] = "x", [1] = "y" },
            new Dictionary<int, string> { [4] = "x", [1] = "y", [3] = "z", [9] = "stale" },
            new Dictionary<int, string> { [7] = "x", [8] = "y", [9] = "z" }
        })
        {
            var map = start;
            LuminPackSerializer.Deserialize(bytes, ref map);
            Assert.Same(start, map);
            Assert.Equal(source.OrderBy(pair => pair.Key), map.OrderBy(pair => pair.Key));
        }
    }

    /// <summary>Checks duplicate incoming keys still throw and a custom comparer keeps the staged path.</summary>
    [Fact]
    public void DuplicatesAndComparersKeepTheirBehavior()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, int> { [1] = 10, [2] = 20 });
        // Count, then key/value pairs: overwrite the second key with the first.
        BitConverter.GetBytes(1).CopyTo(bytes, 4 + 8);
        var map = new Dictionary<int, int> { [1] = 0, [2] = 0 };
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes, ref map));

        byte[] valid = LuminPackSerializer.Serialize(new Dictionary<int, int> { [2] = 20, [1] = 10 });
        var comparer = new ReverseComparer();
        var custom = new Dictionary<int, int>(comparer) { [1] = 0, [2] = 0 };
        LuminPackSerializer.Deserialize(valid, ref custom);
        Assert.Same(comparer, custom.Comparer);
        Assert.Equal(10, custom[1]);
        Assert.Equal(20, custom[2]);
    }

    /// <summary>Checks a reused member dictionary inside a reused object refills in place.</summary>
    [Fact]
    public void MemberDictionariesRefillInPlace()
    {
        var source = new ScalarSlotHolder { Names = new() { [1] = "a", [2] = "b" }, Counts = new() { [1] = 3, [2] = 4 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        var target = new ScalarSlotHolder { Names = new() { [1] = "", [2] = "" }, Counts = new() { [1] = 0, [2] = 0 } };
        var names = target.Names;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Same(names, target!.Names);
        Assert.Equal(source.Names, target.Names);
        Assert.Equal(source.Counts, target.Counts);
    }

    /// <summary>An equality comparer distinct from the default instance.</summary>
    private sealed class ReverseComparer : IEqualityComparer<int>
    {
        /// <summary>Compares two keys.</summary>
        /// <param name="x">The first key.</param>
        /// <param name="y">The second key.</param>
        /// <returns>Whether the keys are equal.</returns>
        public bool Equals(int x, int y) => x == y;

        /// <summary>Hashes a key.</summary>
        /// <param name="obj">The key.</param>
        /// <returns>The hash code.</returns>
        public int GetHashCode(int obj) => ~obj;
    }
}

/// <summary>Holds dictionaries eligible for scalar slot reuse.</summary>
[LuminPackable]
public partial class ScalarSlotHolder
{
    /// <summary>The names.</summary>
    public Dictionary<int, string>? Names;
    /// <summary>The counts.</summary>
    public Dictionary<int, int>? Counts;
}
