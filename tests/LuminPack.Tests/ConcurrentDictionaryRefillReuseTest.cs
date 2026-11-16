#if NET8_0_OR_GREATER && LUMINPACK_NEXT_GENERATOR
using System.Collections.Concurrent;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks binary concurrent dictionary reuse that reads pairs over the existing keys and values.</summary>
[Collection(nameof(RuntimeStateCollection))]
public sealed class ConcurrentDictionaryRefillReuseTest
{
    /// <summary>Keeps the dictionary, its keys and its value objects in enumeration order, allocating only the enumerator.</summary>
    [Fact]
    public void RepeatedInputKeepsKeysAndValues()
    {
        // Small consecutive integer keys occupy their own buckets, so both dictionaries enumerate in key order.
        var source = new ConcurrentDictionary<int, ConcurrentRefillItem>(
            Enumerable.Range(0, 200).Select(index => KeyValuePair.Create(index, new ConcurrentRefillItem { Id = index })));
        byte[] wire = LuminPackSerializer.Serialize(source);
        ConcurrentDictionary<int, ConcurrentRefillItem>? target = null;
        LuminPackSerializer.Deserialize(wire, ref target);
        var alias = target;
        var item = target![7];
        LuminPackSerializer.Deserialize(wire, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        LuminPackSerializer.Deserialize(wire, ref target);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Same(alias, target);
        Assert.Same(item, target![7]);
        Assert.Equal(source.Select(pair => (pair.Key, pair.Value.Id)).OrderBy(pair => pair.Key),
            target.Select(pair => (pair.Key, pair.Value.Id)).OrderBy(pair => pair.Key));
        Assert.True(allocated < 256, "allocated " + allocated);

        var single = new ConcurrentDictionary<string, ConcurrentRefillItem>();
        single["only"] = new ConcurrentRefillItem { Id = 1 };
        var singleTarget = new ConcurrentDictionary<string, ConcurrentRefillItem>();
        string key = new string('o', 1) + "nly";
        singleTarget[key] = new ConcurrentRefillItem { Id = 2 };
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(single), ref singleTarget);
        Assert.Same(key, singleTarget!.Keys.Single());
        Assert.Equal(1, singleTarget[key].Id);

        var strings = new ConcurrentDictionary<int, string>(Enumerable.Range(0, 100).Select(index => KeyValuePair.Create(index, "v" + index)));
        byte[] stringWire = LuminPackSerializer.Serialize(strings);
        var stringTarget = new ConcurrentDictionary<int, string>(strings);
        string value = stringTarget[42];
        LuminPackSerializer.Deserialize(stringWire, ref stringTarget);
        Assert.Same(value, stringTarget[42]);
        Assert.Equal(strings.OrderBy(pair => pair.Key), stringTarget.OrderBy(pair => pair.Key));
    }

    /// <summary>Adds, updates and removes keys for changed key sets and enumeration orders, reusing values by key.</summary>
    [Fact]
    public void ChangedKeysAndOrdersMatchTheRecord()
    {
        foreach ((int existingFirst, int existingCount, int incomingFirst, int incomingCount) in new[]
                 { (0, 5, 2, 6), (0, 6, 0, 3), (0, 3, 0, 6), (0, 4, 10, 4), (0, 40, 0, 40), (0, 40, 1, 40) })
        {
            // A large source capacity enumerates in another bucket order than the default-sized target.
            var source = new ConcurrentDictionary<string, ConcurrentRefillItem>(1, 1009);
            for (int index = incomingFirst; index < incomingFirst + incomingCount; index++)
                source["key-" + index] = new ConcurrentRefillItem { Id = index };
            var target = new ConcurrentDictionary<string, ConcurrentRefillItem>();
            for (int index = existingFirst; index < existingFirst + existingCount; index++)
                target["key-" + index] = new ConcurrentRefillItem { Id = -index };
            var old = target.ToDictionary(pair => pair.Key, pair => pair.Value);

            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);
            AssertContents(source, target!);
            foreach (var pair in target!)
            {
                if (old.TryGetValue(pair.Key, out var previous))
                    Assert.Same(previous, pair.Value);
            }
            Assert.True(target.TryAdd("late", new ConcurrentRefillItem()));
        }
    }

    /// <summary>A truncated record throws and leaves a usable dictionary; an empty record clears it.</summary>
    [Fact]
    public void TruncatedAndEmptyRecords()
    {
        var source = new ConcurrentDictionary<int, string>(Enumerable.Range(0, 10).Select(index => KeyValuePair.Create(index, "v" + index)));
        byte[] wire = LuminPackSerializer.Serialize(source);
        var target = new ConcurrentDictionary<int, string>(Enumerable.Range(5, 10).Select(index => KeyValuePair.Create(index, "old")));
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(wire.AsSpan(0, wire.Length - 1), ref target));
        LuminPackSerializer.Deserialize(wire, ref target);
        Assert.Equal(source.OrderBy(pair => pair.Key), target!.OrderBy(pair => pair.Key));

        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(new ConcurrentDictionary<int, string>()), ref target);
        Assert.Empty(target!);
    }

    /// <summary>A repeated key keeps its first value, as a fresh read does.</summary>
    [Fact]
    public void RepeatedKeysMatchTheFreshRead()
    {
        var source = new ConcurrentDictionary<string, int>(new[] { KeyValuePair.Create("AA", 1), KeyValuePair.Create("BB", 2) });
        byte[] bytes = LuminPackSerializer.Serialize(source);
        byte[] key = LuminPackSerializer.Serialize("AA");
        int first = sizeof(int), second = first + key.Length + sizeof(int);
        Assert.Contains(bytes.AsSpan(second, key.Length).ToArray(), new[] { key, LuminPackSerializer.Serialize("BB") });
        bytes.AsSpan(first, key.Length).CopyTo(bytes.AsSpan(second));

        ConcurrentDictionary<string, int>? fresh = null;
        LuminPackSerializer.Deserialize(bytes, ref fresh);
        foreach (var existing in new[] { new[] { "AA", "BB" }, new[] { "CC" } })
        {
            var target = new ConcurrentDictionary<string, int>(existing.Select(name => KeyValuePair.Create(name, -1)));
            LuminPackSerializer.Deserialize(bytes, ref target);
            Assert.Equal(fresh!.OrderBy(pair => pair.Key), target!.OrderBy(pair => pair.Key));
        }
    }

    private static void AssertContents(ConcurrentDictionary<string, ConcurrentRefillItem> expected, ConcurrentDictionary<string, ConcurrentRefillItem> actual) =>
        Assert.Equal(expected.Select(pair => (pair.Key, pair.Value.Id)).OrderBy(pair => pair.Key, StringComparer.Ordinal),
            actual.Select(pair => (pair.Key, pair.Value.Id)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
}

/// <summary>An ordinary value reused in place.</summary>
[LuminPackable]
public partial class ConcurrentRefillItem
{
    /// <summary>The identifier.</summary>
    public int Id;
}
#endif
