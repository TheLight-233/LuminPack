using System.Collections.Immutable;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Code;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks the run-time probed collection storage and the tree-backed collection paths built on it.</summary>
/// <remarks>Every serializer call names its concrete type, so it binds the generated concrete overload.</remarks>
public class CollectionStorageLayoutTest
{
    /// <summary>Checks that records crossing the reusable buffer's growth points keep the bytes of the allocating entry.</summary>
    /// <remarks>Each record is several times the initial native capacity, so the capacity and collection-threshold growth
    /// refreshes the cursor fields more than once within one operation.</remarks>
    [Fact]
    public void RecordsCrossingBufferGrowthKeepTheirBytes()
    {
        var map = new Dictionary<int, int>();
        for (int i = 0; i < 40_000; i++) map[i] = i * 3;
        var sparse = new Dictionary<int, int>(map);
        for (int i = 0; i < 40_000; i += 7) sparse.Remove(i);
        var set = new HashSet<int>(Enumerable.Range(0, 40_000));
        var texts = Enumerable.Range(0, 20_000).Select(i => new string((char)('a' + i % 26), i % 37)).ToList();
        var sorted = new SortedSet<int>(Enumerable.Range(0, 40_000));

        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(map, buffer);
        Assert.Equal(LuminPackSerializer.Serialize(map), buffer.ToArrayAndReset());
        LuminPackSerializer.Serialize(sparse, buffer);
        Assert.Equal(LuminPackSerializer.Serialize(sparse), buffer.ToArrayAndReset());
        LuminPackSerializer.Serialize(set, buffer);
        Assert.Equal(LuminPackSerializer.Serialize(set), buffer.ToArrayAndReset());
        LuminPackSerializer.Serialize(texts, buffer);
        byte[] textBytes = buffer.ToArrayAndReset();
        Assert.Equal(LuminPackSerializer.Serialize(texts), textBytes);
        LuminPackSerializer.Serialize(sorted, buffer);
        Assert.Equal(LuminPackSerializer.Serialize(sorted), buffer.ToArrayAndReset());

        List<string>? read = null;
        LuminPackSerializer.Deserialize(textBytes.AsSpan(), ref read);
        Assert.Equal(texts, read);
        Dictionary<int, int>? readMap = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(sparse).AsSpan(), ref readMap);
        Assert.Equal(sparse.OrderBy(p => p.Key), readMap!.OrderBy(p => p.Key));
    }

    /// <summary>Checks that every probed family is located on the test runtime and that the hash-table entries are recognized.</summary>
    [Fact]
    public void ProbedLayoutsAreLocated()
    {
        foreach (int offset in new[]
                 {
                     LuminCollectionLayouts.ListItems, LuminCollectionLayouts.ListSize, LuminCollectionLayouts.StackArray, LuminCollectionLayouts.StackSize,
                     LuminCollectionLayouts.QueueArray, LuminCollectionLayouts.QueueHead, LuminCollectionLayouts.QueueTail, LuminCollectionLayouts.QueueSize,
                     LuminCollectionLayouts.HashSetBuckets, LuminCollectionLayouts.HashSetEntries, LuminCollectionLayouts.HashSetCount,
                     LuminCollectionLayouts.HashSetFreeList, LuminCollectionLayouts.HashSetVersion, LuminCollectionLayouts.HashSetComparer,
                     LuminCollectionLayouts.DictionaryBuckets, LuminCollectionLayouts.DictionaryEntries, LuminCollectionLayouts.DictionaryCount,
                     LuminCollectionLayouts.DictionaryFreeCount, LuminCollectionLayouts.DictionaryVersion, LuminCollectionLayouts.DictionaryComparer,
                     LuminCollectionLayouts.BitArrayArray, LuminCollectionLayouts.BitArrayLength, LuminCollectionLayouts.CollectionItems,
                     LuminCollectionLayouts.ObservableCollectionItems, LuminCollectionLayouts.ReadOnlyCollectionList,
                     LuminCollectionLayouts.ReadOnlyObservableCollectionList, LuminCollectionLayouts.BuilderItems, LuminCollectionLayouts.BuilderSize
                 })
            Assert.True(offset >= 0);
        Assert.True(LuminPackMarshal.HashSetView<int>.Recognized);
        Assert.True(LuminPackMarshal.HashSetView<string>.Recognized);
        Assert.True(LuminPackMarshal.DictionaryView<int, string>.Recognized);
        Assert.True(LuminPackMarshal.DictionaryView<string, long>.Recognized);
    }

    /// <summary>Checks the probed fields against the collections' public state.</summary>
    [Fact]
    public void ProbedFieldsHoldThePublicState()
    {
        var list = new List<int> { 1, 2, 3 };
        Assert.Equal(3, LuminFieldLayout.Field<int>(list, LuminCollectionLayouts.ListSize));
        var queue = new Queue<int>(new[] { 4, 5 });
        queue.Dequeue();
        Assert.Equal(1, LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueSize));
        Assert.Equal(1, LuminFieldLayout.Field<int>(queue, LuminCollectionLayouts.QueueHead));
        var set = new HashSet<string> { "a", "b" };
        var view = LuminPackMarshal.GetHashSetView(set);
        Assert.Equal(2, view._count);
        Assert.True(view._comparer is null || view._comparer.Equals("a", "a"));
        int version = view._version;
        set.Add("c");
        Assert.NotEqual(version, view._version);
        set.Remove("a");
        Assert.Equal(1, view._freeCount);
        var dictionary = new Dictionary<int, string> { [1] = "x" };
        var dictionaryView = LuminPackMarshal.GetDictionaryView(dictionary);
        Assert.Equal(1, dictionaryView._count);
        Assert.Equal(1, dictionaryView._entries[0].Key);
        Assert.Equal("x", dictionaryView._entries[0].Value);
    }

    /// <summary>Checks round trips of the tree-backed collections with raw and reference elements.</summary>
    [Fact]
    public void TreeCollectionsRoundTrip()
    {
        var ints = Enumerable.Range(-50, 300).ToArray();
        var strings = ints.Select(i => "s" + i).ToArray();
        var pairs = ints.Select(i => new KeyValuePair<int, string>(i, "v" + i)).ToArray();

        foreach (int count in new[] { 0, 1, 2, 300 })
        {
            Check(new SortedSet<int>(ints.Take(count)));
            Check(new SortedSet<string>(strings.Take(count), StringComparer.Ordinal), ordinal: true);
            Check(ImmutableSortedSet.CreateRange(ints.Take(count)));
            Check(ImmutableHashSet.CreateRange(ints.Take(count)));
            Check(ImmutableHashSet.CreateRange(strings.Take(count)));
            Check(new SortedDictionary<int, string>(pairs.Take(count).ToDictionary(p => p.Key, p => p.Value)));
            Check(ImmutableSortedDictionary.CreateRange(pairs.Take(count)));
            Check(ImmutableDictionary.CreateRange(pairs.Take(count)));
        }
    }

    /// <summary>Checks that input the one-pass builds cannot take produces the collections of the original construction.</summary>
    [Fact]
    public void UnorderedDuplicateAndCollidingInputUseTheOriginalConstruction()
    {
        // Sets share the count-prefixed element record of a list, so a list supplies unordered and duplicate elements.
        byte[] unordered = LuminPackSerializer.Serialize(new List<int> { 5, 3, 3, 9, -1 });
        ImmutableHashSet<int>? hashSet = null;
        LuminPackSerializer.Deserialize(unordered.AsSpan(), ref hashSet);
        Assert.True(hashSet!.SetEquals(new[] { 5, 3, 9, -1 }));
        Assert.Equal(4, hashSet.Count);
        ImmutableSortedSet<int>? sortedSet = null;
        LuminPackSerializer.Deserialize(unordered.AsSpan(), ref sortedSet);
        Assert.Equal(new[] { -1, 3, 5, 9 }, sortedSet!);

        // Hash order of strings is not key order, which a sorted dictionary must rebuild.
        var source = ImmutableDictionary.CreateRange(Enumerable.Range(0, 64).Select(i => new KeyValuePair<string, int>("k" + i, i)));
        byte[] hashOrdered = LuminPackSerializer.Serialize(source);
        ImmutableSortedDictionary<string, int>? sorted = null;
        LuminPackSerializer.Deserialize(hashOrdered.AsSpan(), ref sorted);
        Assert.Equal(source.OrderBy(p => p.Key, Comparer<string>.Default), sorted!);

        var colliding = Enumerable.Range(0, 40).Select(i => new CollidingKey { Value = i }).ToArray();
        byte[] collidingBytes = LuminPackSerializer.Serialize(ImmutableHashSet.CreateRange(colliding));
        ImmutableHashSet<CollidingKey>? collidingSet = null;
        LuminPackSerializer.Deserialize(collidingBytes.AsSpan(), ref collidingSet);
        Assert.True(collidingSet!.SetEquals(colliding));
        Assert.Contains(new CollidingKey { Value = 39 }, collidingSet);

        var collidingPairs = ImmutableDictionary.CreateRange(colliding.Select(k => new KeyValuePair<CollidingKey, int>(k, k.Value)));
        byte[] collidingPairBytes = LuminPackSerializer.Serialize(collidingPairs);
        ImmutableDictionary<CollidingKey, int>? collidingDictionary = null;
        LuminPackSerializer.Deserialize(collidingPairBytes.AsSpan(), ref collidingDictionary);
        Assert.Equal(17, collidingDictionary![new CollidingKey { Value = 17 }]);
        Assert.Equal(40, collidingDictionary.Count);
    }

    /// <summary>Checks that duplicate keys still throw and that a reused sorted dictionary keeps its comparer.</summary>
    [Fact]
    public void DuplicateKeysThrowAndReusedComparersAreKept()
    {
        // Sorted and hash dictionaries share the count-prefixed key/value record.
        var duplicate = new ImmutableSortedDictionaryDuplicateSource();
        byte[] bytes = duplicate.Bytes;
        ImmutableSortedDictionary<int, string>? sorted = null;
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref sorted));
        ImmutableDictionary<int, string>? hashed = null;
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref hashed));
        SortedDictionary<int, string>? mutable = null;
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref mutable));

        var payload = LuminPackSerializer.Serialize(new SortedDictionary<int, string> { [1] = "a", [2] = "b", [3] = "c" });
        var descending = new SortedDictionary<int, string>(Comparer<int>.Create((a, b) => b.CompareTo(a))) { [9] = "z" };
        var reused = descending;
        LuminPackSerializer.Deserialize(payload.AsSpan(), ref reused);
        Assert.Same(descending, reused);
        Assert.Equal(new[] { 3, 2, 1 }, reused!.Keys);
        reused.Add(0, "d");
        Assert.Equal(0, reused.Keys.Last());
    }

    /// <summary>Checks linked lists linked in one pass, reused lists and the probed node fields.</summary>
    [Fact]
    public void LinkedListsLinkTheirNodes()
    {
        foreach (int count in new[] { 0, 1, 2, 500 })
        {
            var value = new LinkedList<int>(Enumerable.Range(10, count));
            byte[] bytes = LuminPackSerializer.Serialize(value);
            LinkedList<int>? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value, copy!);
            Assert.Equal(value.Reverse(), copy!.Reverse());
            if (count == 0) continue;
            Assert.Same(copy, copy.First!.List);
            Assert.Same(copy.First, copy.Last!.Next ?? copy.First);
            copy.AddFirst(-1);
            copy.AddLast(-2);
            copy.Remove(copy.Find(10)!);
            Assert.Equal(count + 1, copy.Count);
            Assert.Equal(-1, copy.First!.Value);
            Assert.Equal(-2, copy.Last!.Value);
        }

        var strings = new LinkedList<string?>(new[] { "a", null, "c" });
        byte[] stringBytes = LuminPackSerializer.Serialize(strings);
        var reused = new LinkedList<string?>(new[] { "old" });
        var target = reused;
        LuminPackSerializer.Deserialize(stringBytes.AsSpan(), ref target);
        Assert.Same(reused, target);
        Assert.Equal(strings, target!);
    }

    /// <summary>Checks that a concurrent dictionary keeps the first pair of a repeated key and interface dictionaries still reject one.</summary>
    [Fact]
    public void RepeatedKeysKeepTheirOriginalRules()
    {
        byte[] bytes = new ImmutableSortedDictionaryDuplicateSource().Bytes;
        System.Collections.Concurrent.ConcurrentDictionary<int, string>? concurrent = null;
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref concurrent);
        Assert.Single(concurrent!);
        Assert.Equal("a", concurrent![7]);
        IDictionary<int, string>? dictionary = null;
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(bytes.AsSpan(), ref dictionary));

        var source = new System.Collections.Concurrent.ConcurrentDictionary<int, string>(Enumerable.Range(0, 300).ToDictionary(i => i, i => "v" + i));
        byte[] sourceBytes = LuminPackSerializer.Serialize(source);
        System.Collections.Concurrent.ConcurrentDictionary<int, string>? copy = null;
        LuminPackSerializer.Deserialize(sourceBytes.AsSpan(), ref copy);
        Assert.Equal(source.OrderBy(p => p.Key), copy!.OrderBy(p => p.Key));
        Assert.True(copy.TryAdd(1000, "x"));
        Assert.False(copy.TryAdd(5, "y"));

        IDictionary<int, string> interfaceSource = Enumerable.Range(0, 100).ToDictionary(i => i, i => "w" + i);
        byte[] interfaceBytes = LuminPackSerializer.Serialize(interfaceSource);
        IDictionary<int, string>? interfaceCopy = null;
        LuminPackSerializer.Deserialize(interfaceBytes.AsSpan(), ref interfaceCopy);
        Assert.Equal(interfaceSource, interfaceCopy!);
        interfaceCopy!.Add(-1, "n");
        Assert.Equal("n", interfaceCopy[-1]);
    }

    /// <summary>Checks sorted lists written from and read into their own storage, with unordered and duplicate input.</summary>
    [Fact]
    public void SortedListsUseTheirStorage()
    {
        foreach (int count in new[] { 0, 1, 2, 400 })
        {
            var value = new SortedList<int, string>(Enumerable.Range(-5, count).ToDictionary(i => i * 3, i => "v" + i));
            byte[] bytes = LuminPackSerializer.Serialize(value);
            SortedList<int, string>? copy = null;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
            Assert.Equal(value, copy!);
            Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
            copy!.Add(int.MaxValue, "max");
            copy.Add(-1000, "min");
            Assert.Equal(-1000, copy.Keys[0]);
            Assert.Equal(count + 2, copy.Count);
        }

        // A hash dictionary's record has the same count-prefixed pairs in another order.
        var unordered = new Dictionary<int, string> { [9] = "c", [1] = "a", [5] = "b" };
        SortedList<int, string>? sorted = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(unordered).AsSpan(), ref sorted);
        Assert.Equal(new[] { 1, 5, 9 }, sorted!.Keys);
        SortedList<int, string>? duplicate = null;
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize(new ImmutableSortedDictionaryDuplicateSource().Bytes.AsSpan(), ref duplicate));
    }

    /// <summary>Checks priority queues written from their heap array and rebuilt by one heapify, and the read-only wrappers.</summary>
    [Fact]
    public void HeapsAndReadOnlyWrappersRoundTrip()
    {
        var random = new Random(7);
        var queue = new PriorityQueue<string, int>();
        for (int i = 0; i < 300; i++) queue.Enqueue("e" + i, random.Next(1000));
        byte[] bytes = LuminPackSerializer.Serialize(queue);
        PriorityQueue<string, int>? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        var expected = new List<int>();
        while (queue.TryDequeue(out _, out int priority)) expected.Add(priority);
        var actual = new List<int>();
        while (copy!.TryDequeue(out _, out int priority)) actual.Add(priority);
        Assert.Equal(expected, actual);

        var reused = new PriorityQueue<string, int>(Comparer<int>.Create((a, b) => b.CompareTo(a)));
        reused.Enqueue("old", 1);
        var target = reused;
        byte[] small = LuminPackSerializer.Serialize(new PriorityQueue<string, int>(new[] { ("a", 1), ("b", 5), ("c", 3) }));
        LuminPackSerializer.Deserialize(small.AsSpan(), ref target);
        Assert.Same(reused, target);
        Assert.Equal("b", target!.Dequeue());

        var observable = new System.Collections.ObjectModel.ReadOnlyObservableCollection<int>(new System.Collections.ObjectModel.ObservableCollection<int>(Enumerable.Range(0, 200)));
        byte[] observableBytes = LuminPackSerializer.Serialize(observable);
        System.Collections.ObjectModel.ReadOnlyObservableCollection<int>? observableCopy = null;
        LuminPackSerializer.Deserialize(observableBytes.AsSpan(), ref observableCopy);
        Assert.Equal(observable, observableCopy!);
        Assert.Equal(observableBytes, LuminPackSerializer.Serialize(observableCopy));

        var readOnly = new System.Collections.ObjectModel.ReadOnlyDictionary<int, string>(Enumerable.Range(0, 100).ToDictionary(i => i, i => "r" + i));
        byte[] readOnlyBytes = LuminPackSerializer.Serialize(readOnly);
        System.Collections.ObjectModel.ReadOnlyDictionary<int, string>? readOnlyCopy = null;
        LuminPackSerializer.Deserialize(readOnlyBytes.AsSpan(), ref readOnlyCopy);
        Assert.Equal(readOnly, readOnlyCopy!);
        Assert.Equal("r42", readOnlyCopy![42]);
    }

    /// <summary>Round-trips a sorted set and checks order, membership and later mutation.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value.</param>
    /// <param name="ordinal">The position of the item.</param>
    private static void Check<T>(SortedSet<T> value, bool ordinal = false)
    {
        byte[] bytes = LuminPackSerializer.Serialize(value);
        Assert.Equal(EnumerationRecord(value), bytes.Length);
        SortedSet<T>? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        if (!ordinal) Assert.Equal(value, copy!);
        else Assert.True(copy!.SetEquals(value));
        Assert.Equal(bytes, LuminPackSerializer.Serialize(new SortedSet<T>(copy!, value.Comparer)));
    }

    /// <summary>Round-trips an immutable sorted set and checks that the built tree answers indexing and mutation.</summary>
    /// <param name="value">The value.</param>
    private static void Check(ImmutableSortedSet<int> value)
    {
        byte[] bytes = LuminPackSerializer.Serialize(value);
        ImmutableSortedSet<int>? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.Equal(value, copy!);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        if (value.Count > 1)
        {
            Assert.Equal(value[value.Count / 2], copy![value.Count / 2]);
            Assert.Equal(value.Count + 1, copy.Add(int.MaxValue).Count);
            Assert.Equal(value.Count - 1, copy.Remove(value[0]).Count);
        }
    }

    /// <summary>Round-trips an immutable hash set and checks membership and mutation.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value.</param>
    private static void Check<T>(ImmutableHashSet<T> value) where T : notnull
    {
        byte[] bytes = LuminPackSerializer.Serialize(value);
        ImmutableHashSet<T>? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.True(copy!.SetEquals(value));
        Assert.Equal(value.Count, copy.Count);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        foreach (var item in value.Take(3))
        {
            Assert.Contains(item, copy);
            Assert.DoesNotContain(item, copy.Remove(item));
        }
    }

    /// <summary>Round-trips a sorted dictionary and checks lookup and mutation.</summary>
    /// <param name="value">The value.</param>
    private static void Check(SortedDictionary<int, string> value)
    {
        byte[] bytes = LuminPackSerializer.Serialize(value);
        SortedDictionary<int, string>? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.Equal(value, copy!);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        if (value.Count > 0)
        {
            int key = value.Keys.ElementAt(value.Count / 2);
            Assert.Equal(value[key], copy![key]);
            Assert.True(copy.Remove(key));
            copy.Add(key, "again");
            copy.Add(int.MinValue, "min");
            Assert.Equal(int.MinValue, copy.Keys.First());
            Assert.Equal(value.Count + 1, copy.Count);
        }
    }

    /// <summary>Round-trips an immutable sorted dictionary and checks lookup and mutation.</summary>
    /// <param name="value">The value.</param>
    private static void Check(ImmutableSortedDictionary<int, string> value)
    {
        byte[] bytes = LuminPackSerializer.Serialize(value);
        ImmutableSortedDictionary<int, string>? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.Equal(value, copy!);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        if (value.Count > 0)
        {
            var first = value.First();
            Assert.Equal(first.Value, copy![first.Key]);
            Assert.Equal(value.Count - 1, copy.Remove(first.Key).Count);
            Assert.Equal(value.Count + 1, copy.Add(int.MaxValue, "max").Count);
        }
    }

    /// <summary>Round-trips an immutable dictionary and checks lookup and mutation.</summary>
    /// <param name="value">The value.</param>
    private static void Check(ImmutableDictionary<int, string> value)
    {
        byte[] bytes = LuminPackSerializer.Serialize(value);
        ImmutableDictionary<int, string>? copy = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes.AsSpan(), ref copy));
        Assert.Equal(value.Count, copy!.Count);
        Assert.Equal(bytes, LuminPackSerializer.Serialize(copy));
        foreach (var pair in value.Take(3))
        {
            Assert.Equal(pair.Value, copy[pair.Key]);
            Assert.False(copy.Remove(pair.Key).ContainsKey(pair.Key));
        }
    }

    /// <summary>Gets the width of a count-prefixed record of four-byte or string elements.</summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The result.</returns>
    private static int EnumerationRecord<T>(SortedSet<T> value)
        => typeof(T) == typeof(int) ? 4 + value.Count * 4 : LuminPackSerializer.Serialize(value.ToList()).Length;

    /// <summary>Writes a dictionary record whose key repeats.</summary>
    private sealed class ImmutableSortedDictionaryDuplicateSource
    {
        internal readonly byte[] Bytes;

        /// <summary>Joins two single-pair records into one record with a repeated key.</summary>
        internal ImmutableSortedDictionaryDuplicateSource()
        {
            // Two single-pair records joined into one count-prefixed record of two pairs with the same key and different values;
            // the immutable builders accept a repeated key only with an equal value.
            byte[] one = LuminPackSerializer.Serialize(new SortedDictionary<int, string> { [7] = "a" });
            byte[] two = LuminPackSerializer.Serialize(new SortedDictionary<int, string> { [7] = "b" });
            Bytes = new byte[one.Length + two.Length - 4];
            BitConverter.TryWriteBytes(Bytes.AsSpan(0, 4), 2);
            one.AsSpan(4).CopyTo(Bytes.AsSpan(4));
            two.AsSpan(4).CopyTo(Bytes.AsSpan(one.Length));
        }
    }
}

/// <summary>A key whose hash codes collide in pairs.</summary>
[LuminPackable]
public partial struct CollidingKey : IEquatable<CollidingKey>
{
    /// <summary>Stores the value.</summary>
    public int Value;

    /// <inheritdoc />
    public readonly bool Equals(CollidingKey other) => Value == other.Value;
    /// <inheritdoc />
    public override readonly bool Equals(object? obj) => obj is CollidingKey other && Equals(other);
    /// <inheritdoc />
    public override readonly int GetHashCode() => Value / 2;
}
