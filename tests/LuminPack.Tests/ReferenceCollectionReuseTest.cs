using System.Collections;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Protects exact mutable reference storage behind sequence interfaces and writable views.</summary>
public sealed class ReferenceCollectionReuseTest
{
    /// <summary>Retains the exact container, nested objects, strings and collection storage in every binary profile.</summary>
    [Fact]
    public void SequenceInterfacesReuseExactListsAndArrays()
    {
        CheckInterfaces<LuminPackUtf8LengthOptions>();
        CheckInterfaces<LuminPackUtf8TokenOptions>();
        CheckInterfaces<LuminPackUtf16LengthOptions>();
        CheckInterfaces<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Keeps slice boundaries and reference identity for both mutable contiguous view families.</summary>
    [Fact]
    public void MutableViewsReuseOnlyTheirSelectedSlice()
    {
        CheckViews<LuminPackUtf8LengthOptions>();
        CheckViews<LuminPackUtf8TokenOptions>();
        CheckViews<LuminPackUtf16LengthOptions>();
        CheckViews<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Unknown and derived interface destinations retain the original replacement boundary.</summary>
    [Fact]
    public void UnknownAndDerivedInterfaceImplementationsAreNotObserved()
    {
        IEnumerable<ReferenceReuseValue> source = Make();
        byte[] bytes = LuminPackSerializer.Serialize(source);
        IEnumerable<ReferenceReuseValue> unknown = new UnobservedSequence();
        LuminPackSerializer.Deserialize(bytes, ref unknown);
        Assert.IsType<ReferenceReuseValue[]>(unknown);
        var derived = new DerivedList { new() { Number = 719 } };
        IList<ReferenceReuseValue> target = derived;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.IsType<List<ReferenceReuseValue>>(target);
        Assert.Equal(719, derived[0].Number);
    }

    /// <summary>A changed view length creates new storage without mutating its previous backing array.</summary>
    [Fact]
    public void ChangedViewLengthsRetainFreshReplacement()
    {
        ReferenceReuseValue[] original = Make();
        Memory<ReferenceReuseValue> target = original;
        Memory<ReferenceReuseValue> source = new[] { new ReferenceReuseValue { Number = 137 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Single(target.ToArray());
        Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<ReferenceReuseValue>)target, out var storage));
        Assert.NotSame(original, storage.Array);
        Assert.Equal(0, original[0].Number);
        ArraySegment<ReferenceReuseValue> segment = new(original);
        LuminPackSerializer.Deserialize(bytes, ref segment);
        Assert.Single(segment);
        Assert.NotSame(original, segment.Array);
        Assert.Equal(0, original[0].Number);
    }

    /// <summary>Compares every truncated selected view with the established concrete array element operation.</summary>
    [Fact]
    public void ViewFailuresRetainTheConcreteArrayPrefix()
    {
        Memory<ReferenceReuseValue> source = Make();
        byte[] bytes = LuminPackSerializer.Serialize(source);
        for (int prefix = 0; prefix < bytes.Length; prefix++)
        {
            var actualBacking = Make(719);
            var expected = Make(719);
            Memory<ReferenceReuseValue> actual = actualBacking;
            byte[] input = bytes.AsSpan(0, prefix).ToArray();
            Exception? before = Record.Exception(() => LuminPackSerializer.Deserialize(input, ref expected));
            Exception? after = Record.Exception(() => LuminPackSerializer.Deserialize(input, ref actual));
            Assert.NotNull(before);
            Assert.NotNull(after);
            Assert.Equal(before.GetType(), after.GetType());
            Assert.Equal(expected.Select(item => item.Number), actual.Span.ToArray().Select(item => item.Number));
            Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<ReferenceReuseValue>)actual, out var storage));
            Assert.Same(actualBacking, storage.Array);
        }
    }

    /// <summary>Checks every exact interface family against its concrete mutable element behavior.</summary>
    /// <typeparam name="TOptions">The complete binary string profile.</typeparam>
    private static void CheckInterfaces<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        IList<ReferenceReuseValue> source = Make();
        byte[] bytes = LuminPackSerializer.Serialize<TOptions>(source);
        IEnumerable<ReferenceReuseValue> sequence = Make().ToList();
        var sequenceRoot = sequence;
        var first = Capture(sequence.First());
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref sequence);
        Verify(sequenceRoot, sequence, first);
        ICollection<ReferenceReuseValue> collection = Make();
        var collectionRoot = collection;
        first = Capture(collection.First());
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref collection);
        Verify(collectionRoot, collection, first);
        IList<ReferenceReuseValue> list = Make().ToList();
        var listRoot = list;
        first = Capture(list[0]);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref list);
        Verify(listRoot, list, first);
        IReadOnlyList<ReferenceReuseValue> readOnlyList = Make();
        var readOnlyListRoot = readOnlyList;
        first = Capture(readOnlyList[0]);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref readOnlyList);
        Verify(readOnlyListRoot, readOnlyList, first);
        IReadOnlyCollection<ReferenceReuseValue> readOnlyCollection = Make().ToList();
        var readOnlyCollectionRoot = readOnlyCollection;
        first = Capture(readOnlyCollection.First());
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref readOnlyCollection);
        Verify(readOnlyCollectionRoot, readOnlyCollection, first);
        source = new ReferenceReuseValue[] { Make()[0], null! };
        bytes = LuminPackSerializer.Serialize<TOptions>(source);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref list);
        Assert.Null(list[1]);
        source = null!;
        bytes = LuminPackSerializer.Serialize<TOptions>(source);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref list);
        Assert.Null(list);
        IList<string> text = new List<string> { new('a', 7), new('b', 9) };
        var textRoot = text;
        string oldText = text[0];
        bytes = LuminPackSerializer.Serialize<TOptions>(text);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref text);
        Assert.Same(textRoot, text);
        Assert.Same(oldText, text[0]);
    }

    /// <summary>Retains array owners, offsets, sentinels and nested storage in mutable views.</summary>
    /// <typeparam name="TOptions">The complete binary string profile.</typeparam>
    private static void CheckViews<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        ReferenceReuseValue[] backing = Make();
        Memory<ReferenceReuseValue> target = backing.AsMemory(1, 1);
        Memory<ReferenceReuseValue> source = new[] { Make()[1] };
        var first = Capture(backing[1]);
        var sentinel = backing[0];
        byte[] bytes = LuminPackSerializer.Serialize<TOptions>(source);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref target);
        Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<ReferenceReuseValue>)target, out var storage));
        Assert.Same(backing, storage.Array);
        Assert.Equal(1, storage.Offset);
        Assert.Equal(1, storage.Count);
        VerifyElement(first, target.Span[0]);
        Assert.Same(sentinel, backing[0]);
        ArraySegment<ReferenceReuseValue> segment = new(backing, 1, 1);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref segment);
        Assert.Same(backing, segment.Array);
        Assert.Equal(1, segment.Offset);
        VerifyElement(first, segment[0]);
        string[] texts = ["sentinel", new('a', 7), "old", "last"];
        Memory<string> text = texts.AsMemory(1, 2);
        Memory<string> textSource = new[] { texts[1], null! };
        string original = texts[1];
        bytes = LuminPackSerializer.Serialize<TOptions>(textSource);
        Memory<string> fresh = default;
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref fresh);
        LuminPackSerializer.Deserialize<TOptions>(bytes, ref text);
        Assert.Same(original, text.Span[0]);
        Assert.Equal(fresh.Span[1], text.Span[1]);
        Assert.Equal("sentinel", texts[0]);
        Assert.Equal("last", texts[3]);
    }

    /// <summary>Captures storage identities before a reusable read.</summary>
    /// <param name="value">The existing element.</param>
    /// <returns>The original object graph identities.</returns>
    private static Identity Capture(ReferenceReuseValue value) => new(value, value.Child, value.Tags, value.Name);

    /// <summary>Checks the reused container and its first nested graph.</summary>
    /// <param name="original">The selected container.</param>
    /// <param name="current">The resulting sequence.</param>
    /// <param name="first">The previous element storage.</param>
    private static void Verify(object original, IEnumerable<ReferenceReuseValue> current, Identity first)
    {
        Assert.Same(original, current);
        VerifyElement(first, current.First());
        Assert.Equal(2, current.Count());
    }

    /// <summary>Checks every selected nested storage identity and decoded value.</summary>
    /// <param name="first">The original storage.</param>
    /// <param name="current">The decoded element.</param>
    private static void VerifyElement(Identity first, ReferenceReuseValue current)
    {
        Assert.Same(first.Value, current);
        Assert.Same(first.Child, current.Child);
        Assert.Same(first.Tags, current.Tags);
        Assert.Same(first.Name, current.Name);
        Assert.Equal("child", current.Child.Text);
    }

    /// <summary>Creates independent nested reference elements for each operation.</summary>
    /// <param name="number">The first element value.</param>
    /// <returns>The independent array.</returns>
    private static ReferenceReuseValue[] Make(int number = 0) => Enumerable.Range(0, 2)
        .Select(index => new ReferenceReuseValue { Number = number + index, Name = "value" + index, Child = new() { Text = "child" }, Tags = ["a", "b"] }).ToArray();

    private readonly record struct Identity(ReferenceReuseValue Value, ReferenceReuseChild Child, List<string> Tags, string Name);

    private sealed class DerivedList : List<ReferenceReuseValue>;

    private sealed class UnobservedSequence : IEnumerable<ReferenceReuseValue>
    {
        /// <summary>Rejects an unexpected traversal of an existing unknown destination.</summary>
        /// <returns>No enumerator is available for the destination.</returns>
        public IEnumerator<ReferenceReuseValue> GetEnumerator() => throw new InvalidOperationException("The destination must not be observed.");

        /// <summary>Rejects an unexpected nongeneric traversal.</summary>
        /// <returns>No enumerator is available for the destination.</returns>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}

/// <summary>Provides ordinary object members that can retain their existing nested storage.</summary>
[LuminPackable]
public sealed class ReferenceReuseValue
{
    public int Number;
    public string Name { get; set; } = string.Empty;
    public ReferenceReuseChild Child = null!;
    public List<string> Tags = null!;
}

/// <summary>Provides a nested reference node for reuse identity checks.</summary>
[LuminPackable]
public sealed class ReferenceReuseChild
{
    public string Text = string.Empty;
}
