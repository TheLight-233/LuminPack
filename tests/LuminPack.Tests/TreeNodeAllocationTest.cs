using System.Collections.Immutable;
using System.Reflection;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks that tree builds allocate nodes through the bound accessors and still match the public construction.</summary>
public sealed class TreeNodeAllocationTest
{
    /// <summary>Reads a private static field of a nested layout type.</summary>
    /// <param name="layout">The open layout type name.</param>
    /// <param name="arguments">The closed type arguments.</param>
    /// <param name="field">The static field.</param>
    /// <returns>The field value.</returns>
    private static object Layout(string layout, Type[] arguments, string field)
    {
        var type = typeof(LuminTreeStorage).GetNestedType(layout, BindingFlags.NonPublic)!.MakeGenericType(arguments);
        return type.GetField(field, BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
    }

    /// <summary>Returns whether a node offset set holds a direct allocation delegate.</summary>
    /// <param name="offsets">The boxed offsets.</param>
    /// <returns>Whether a delegate is bound.</returns>
    private static bool Bound(object offsets) => offsets.GetType().GetField("_factory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(offsets) is not null;

    /// <summary>Checks that every tree family binds its node allocator on runtimes with type-name accessors.</summary>
    [Fact]
    public void AccessorsBindForEveryTreeFamily()
    {
        bool expected = Environment.Version.Major >= 10;
        // Sorted families build through the framework's own constructions; hash families allocate their nodes directly.
        foreach (var (builder, arguments) in new[]
        {
            ("SortedSet`1", new[] { typeof(int) }), ("SortedSet`1", new[] { typeof(string) }), ("SortedSet`1", new[] { typeof(KeyValuePair<int, string>) }),
            ("ImmutableSortedSet`1", new[] { typeof(int) }), ("ImmutableSortedSet`1", new[] { typeof(string) }),
            ("ImmutableSortedDictionary`2", new[] { typeof(int), typeof(string) }), ("ImmutableSortedDictionary`2", new[] { typeof(string), typeof(int) }),
        })
        {
            var trees = typeof(LuminTreeStorage).GetNestedType("BclTrees", BindingFlags.NonPublic);
            bool available = trees is not null && (bool)trees.GetNestedType(builder, BindingFlags.NonPublic)!.MakeGenericType(arguments)
                .GetField("Available", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
            Assert.Equal(expected, available);
        }
        object Part(object hash, string part) => hash.GetType().GetField(part, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hash)!;
        object set = Layout("ImmutableHashSetLayout`1", new[] { typeof(string) }, "Hash");
        Assert.Equal(expected, Bound(Part(set, "Tree")));
        Assert.Equal(expected, Bound(Part(set, "List")));
        // The two-parameter dictionary bucket node keeps the uninitialized-object request.
        object dictionary = Layout("ImmutableDictionaryLayout`2", new[] { typeof(int), typeof(string) }, "Hash");
        Assert.False(Bound(Part(dictionary, "Tree")));
        Assert.Equal(expected, Bound(Part(dictionary, "List")));
    }

    /// <summary>Checks that built trees answer lookups, enumeration and mutation as the public construction does.</summary>
    [Fact]
    public void BuiltTreesMatchThePublicConstruction()
    {
        var model = new TreeModel
        {
            Sorted = new SortedSet<int>(Enumerable.Range(0, 300).Select(i => i * 3)),
            Records = new SortedSet<TreeRecord>(Enumerable.Range(0, 100).Select(i => new TreeRecord { Id = i * 2 })),
            Dictionary = new SortedDictionary<int, string>(Enumerable.Range(0, 200).ToDictionary(i => i, i => "v" + i)),
            ImmutableSorted = ImmutableSortedSet.CreateRange(Enumerable.Range(0, 300).Select(i => "s" + i.ToString("D4"))),
            ImmutableDictionary = ImmutableSortedDictionary.CreateRange(Enumerable.Range(0, 200).ToDictionary(i => i, i => "w" + i)),
            Hash = ImmutableHashSet.CreateRange(Enumerable.Range(0, 300).Select(i => "h" + i)),
            HashDictionary = ImmutableDictionary.CreateRange(Enumerable.Range(0, 300).ToDictionary(i => i, i => "x" + i)),
        };
        byte[] bytes = LuminPackSerializer.Serialize(model);
        TreeModel? fresh = null;
        LuminPackSerializer.Deserialize(bytes, ref fresh);
        TreeModel reused = new() { Sorted = new SortedSet<int> { -1 }, Records = new SortedSet<TreeRecord> { new() { Id = -1 } }, Dictionary = new() { [-1] = "" } };
        LuminPackSerializer.Deserialize(bytes, ref reused!);
        foreach (var result in new[] { fresh!, reused })
        {
            Assert.Equal(model.Sorted, result.Sorted);
            Assert.True(result.Sorted.Contains(297) && !result.Sorted.Contains(298));
            Assert.Equal(0, result.Sorted.Min);
            Assert.Equal(897, result.Sorted.Max);
            Assert.True(result.Sorted.Add(1) && result.Sorted.Remove(0) && result.Sorted.Count == 300);
            Assert.Equal(model.Records.Select(r => r.Id), result.Records.Select(r => r.Id));
            Assert.Contains(new TreeRecord { Id = 198 }, result.Records);
            Assert.Equal(model.Dictionary, result.Dictionary);
            Assert.Equal("v150", result.Dictionary[150]);
            Assert.Equal(model.ImmutableSorted, result.ImmutableSorted);
            Assert.Equal(150, result.ImmutableSorted.IndexOf("s0150"));
            Assert.Equal("s0299", result.ImmutableSorted[299]);
            Assert.Equal(model.ImmutableDictionary, result.ImmutableDictionary);
            Assert.Equal("w42", result.ImmutableDictionary[42]);
            Assert.True(result.Hash.SetEquals(model.Hash) && result.Hash.Contains("h299") && result.Hash.Remove("h1").Count == 299);
            Assert.Equal(model.HashDictionary.OrderBy(p => p.Key), result.HashDictionary.OrderBy(p => p.Key));
            Assert.Equal("x7", result.HashDictionary[7]);
        }
    }

    /// <summary>Compares by id through its own comparison.</summary>
    [LuminPackable]
    public sealed partial class TreeRecord : IComparable<TreeRecord>
    {
        /// <summary>Stores the ordering key.</summary>
        public int Id;

        /// <inheritdoc/>
        public int CompareTo(TreeRecord? other) => other is null ? 1 : Id.CompareTo(other.Id);
    }

    /// <summary>Holds every tree-backed family the builders cover.</summary>
    [LuminPackable]
    public sealed partial class TreeModel
    {
        /// <summary>Stores scalar elements.</summary>
        public SortedSet<int> Sorted = new();
        /// <summary>Stores user-comparable elements.</summary>
        public SortedSet<TreeRecord> Records = new();
        /// <summary>Stores pairs.</summary>
        public SortedDictionary<int, string> Dictionary = new();
        /// <summary>Stores immutable sorted strings.</summary>
        public ImmutableSortedSet<string> ImmutableSorted = ImmutableSortedSet<string>.Empty;
        /// <summary>Stores immutable sorted pairs.</summary>
        public ImmutableSortedDictionary<int, string> ImmutableDictionary = ImmutableSortedDictionary<int, string>.Empty;
        /// <summary>Stores immutable hashed strings.</summary>
        public ImmutableHashSet<string> Hash = ImmutableHashSet<string>.Empty;
        /// <summary>Stores immutable hashed pairs.</summary>
        public ImmutableDictionary<int, string> HashDictionary = ImmutableDictionary<int, string>.Empty;
    }
}
