#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary reuse of collection wrappers that reads string and object elements over the previous items.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class WrapperRefillReuseTest
{
    /// <summary>Keeps the wrappers and string references and allocates nothing for repeated identical input.</summary>
    [Fact]
    public void RepeatedStringReuseKeepsIdentityWithoutAllocation()
    {
        var items = Enumerable.Range(0, 300).Select(index => "item-" + index).ToList();

        var collection = new Collection<string>(new List<string>());
        AssertStableReuse(LuminPackSerializer.Serialize(new Collection<string>(items)), ref collection, items);

        var readOnly = new ReadOnlyCollection<string>(Array.Empty<string>());
        AssertStableReuse(LuminPackSerializer.Serialize(new ReadOnlyCollection<string>(items)), ref readOnly, items);

        var builder = new ReadOnlyCollectionBuilder<string>();
        AssertStableReuse(LuminPackSerializer.Serialize(new ReadOnlyCollectionBuilder<string>(items)), ref builder, items);
    }

    /// <summary>Grows and shrinks reused wrappers, reusing object elements and invalidating builder enumerators.</summary>
    [Fact]
    public void ObjectElementsAreReusedAcrossLengthChanges()
    {
        foreach (int existing in new[] { 0, 2, 8 })
        {
            foreach (int incoming in new[] { 0, 1, 3, 8, 20 })
            {
                var source = Enumerable.Range(0, incoming).Select(index => new WrapperRefillItem { Id = index }).ToList();
                var old = Enumerable.Range(0, existing).Select(index => new WrapperRefillItem { Id = -index - 1 }).ToList();

                var collection = new Collection<WrapperRefillItem>(old.ToList());
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new Collection<WrapperRefillItem>(source)), ref collection);
                AssertReused(source, old, collection);
                collection.Add(new WrapperRefillItem { Id = 99 });
                Assert.Equal(incoming + 1, collection.Count);

                var readOnly = new ReadOnlyCollection<WrapperRefillItem>(old.ToArray());
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new ReadOnlyCollection<WrapperRefillItem>(source)), ref readOnly);
                Assert.Equal(source.Select(item => item.Id), readOnly.Select(item => item.Id));
                if (existing == incoming)
                    Assert.Equal(old, readOnly);

                var builder = new ReadOnlyCollectionBuilder<WrapperRefillItem>(old);
                var enumerator = builder.GetEnumerator();
                LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new ReadOnlyCollectionBuilder<WrapperRefillItem>(source)), ref builder);
                AssertReused(source, old, builder);
                if (existing != 0 || incoming != 0)
                    Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
                builder.Add(new WrapperRefillItem { Id = 99 });
                Assert.Equal(incoming + 1, builder.Count);
                Assert.Equal(99, builder.ToReadOnlyCollection()[incoming].Id);
            }
        }
    }

    /// <summary>A derived collection keeps its overridden clear and still receives the record.</summary>
    [Fact]
    public void DerivedCollectionStillClears()
    {
        var source = new Collection<string>(new List<string> { "a", "b" });
        Collection<string> target = new CountingCollection { "x", "y", "z" };
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);
        Assert.Equal(source, target);
        Assert.Equal(1, ((CountingCollection)target).Clears);
    }

    /// <summary>Reads into the exact dictionary behind a reused read-only dictionary, keeping the values by key.</summary>
    [Fact]
    public void ReadOnlyDictionaryReusesItsBackingDictionary()
    {
        var source = new ReadOnlyDictionary<string, WrapperRefillItem>(Enumerable.Range(0, 40)
            .ToDictionary(index => "key-" + index, index => new WrapperRefillItem { Id = index }));
        byte[] wire = LuminPackSerializer.Serialize(source);
        var backing = Enumerable.Range(0, 20).ToDictionary(index => "key-" + index, index => new WrapperRefillItem { Id = -index });
        var item = backing["key-3"];
        var target = new ReadOnlyDictionary<string, WrapperRefillItem>(backing);
        var alias = target;
        LuminPackSerializer.Deserialize(wire, ref target);
        Assert.Same(alias, target);
        Assert.Same(item, target["key-3"]);
        Assert.Equal(source.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.Id)),
            target.OrderBy(pair => pair.Key).Select(pair => (pair.Key, pair.Value.Id)));

        var sorted = new SortedDictionary<string, WrapperRefillItem> { ["old"] = new WrapperRefillItem() };
        var other = new ReadOnlyDictionary<string, WrapperRefillItem>(sorted);
        var otherAlias = other;
        LuminPackSerializer.Deserialize(wire, ref other);
        Assert.NotSame(otherAlias, other);
        Assert.Equal(40, other.Count);
        Assert.Equal("old", Assert.Single(sorted).Key);
    }

    private static void AssertStableReuse<T>(byte[] wire, ref T target, List<string> expected) where T : class, IEnumerable<string>
    {
        var alias = target;
        LuminPackSerializer.Deserialize(wire, ref target);
        alias = target;
        string first = target.First();
        LuminPackSerializer.Deserialize(wire, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        LuminPackSerializer.Deserialize(wire, ref target);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(alias, target);
        Assert.Same(first, target.First());
        Assert.Equal(expected, target);
        Assert.Equal(0, allocated);
    }

    private static void AssertReused(List<WrapperRefillItem> source, List<WrapperRefillItem> old, IList<WrapperRefillItem> actual)
    {
        Assert.Equal(source.Select(item => item.Id), actual.Select(item => item.Id));
        for (int index = 0; index < Math.Min(old.Count, source.Count); index++)
            Assert.Same(old[index], actual[index]);
    }

    private sealed class CountingCollection : Collection<string>
    {
        public int Clears;

        protected override void ClearItems()
        {
            Clears++;
            base.ClearItems();
        }
    }
}

/// <summary>An ordinary element reused in place.</summary>
[LuminPackable]
public partial class WrapperRefillItem
{
    /// <summary>The identifier.</summary>
    public int Id;
}
#endif
