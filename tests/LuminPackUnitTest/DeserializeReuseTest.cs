using System.Buffers;
using System.Buffers.Binary;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies binary reuse of complete object graphs and allocation-free warmed reads.</summary>
[Collection(nameof(RuntimeStateCollection))]
public partial class DeserializeReuseTest
{
    /// <summary>Checks an ordinary root member publishes immediately while named version framing keeps its staged commit.</summary>
    [Fact]
    public void FailedChildReadPublishesOrdinaryRootMemberButKeepsVersionedCommit()
    {
        var target = new FailureRoot { Number = 1 };
        var versioned = new VersionedFailureRoot { Number = 1 };
        byte[] ordinaryBytes = LuminPackSerializer.Serialize(new FailureRoot { Number = 7 });
        byte[] versionedBytes = LuminPackSerializer.Serialize(new VersionedFailureRoot { Number = 7 });
        var failure = new InvalidOperationException("Expected child read failure.");
        FailureChild.Failure = failure;
        try
        {
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Deserialize<FailureRoot>(ordinaryBytes, ref target)));
            // Ordinary reuse writes the existing root storage directly, so an already read member stays published.
            Assert.Equal(7, target.Number);
            Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => LuminPackSerializer.Deserialize<VersionedFailureRoot>(versionedBytes, ref versioned)));
            // Named version framing keeps its member locals staged and commits only after the complete read.
            Assert.Equal(1, versioned.Number);
        }
        finally { FailureChild.Failure = null; }
        LuminPackSerializer.Deserialize<FailureRoot>(ordinaryBytes, ref target);
        LuminPackSerializer.Deserialize<VersionedFailureRoot>(versionedBytes, ref versioned);
        Assert.Equal(7, target.Number);
        Assert.Equal(7, versioned.Number);
    }

    /// <summary>Checks nested storage, collection elements and unchanged strings in every binary profile.</summary>
    [Fact]
    public void ExistingGraphIsReusedWithoutAllocations()
    {
        CheckGraph<LuminPackDefaultOptions>();
        CheckGraph<LuminPackUtf8TokenOptions>();
        CheckGraph<LuminPackUtf16LengthOptions>();
        CheckGraph<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks reference identity, changed values and allocations for one compiled profile.</summary>
    /// <typeparam name="TOptions">The binary string profile.</typeparam>
    private static void CheckGraph<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new ReuseGraph
        {
            Child = new() { Number = 42, Text = "\u4E2D\uD83D\uDE00" },
            Items = [new() { Number = 7, Text = "item" }],
            Array = [new() { Number = 8, Text = "array" }],
            Names = ["ascii", "\u4E2D\uD83D\uDE00"],
            Numbers = [1, 2, 3], Tail = 99
        };
        byte[] bytes = LuminPackSerializer.Serialize<ReuseGraph, TOptions>(source);
        var target = new ReuseGraph
        {
            Child = new() { Text = new(source.Child.Text.ToCharArray()) },
            Items = [new() { Text = new(source.Items[0].Text.ToCharArray()) }],
            Array = [new() { Text = new(source.Array[0].Text.ToCharArray()) }],
            Names = source.Names.Select(text => new string(text.ToCharArray())).ToList(),
            Numbers = new(8) { 0, 0, 0 }
        };
        var root = target;
        var child = target.Child;
        var items = target.Items;
        var item = items[0];
        var array = target.Array;
        var element = array[0];
        var names = target.Names;
        var text = child.Text;
        var name = names[1];
        var numbers = target.Numbers;

        int consumed = LuminPackSerializer.Deserialize<ReuseGraph, TOptions>(bytes, ref target);
        Assert.Equal(bytes.Length, consumed);
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Same(items, target.Items);
        Assert.Same(item, target.Items[0]);
        Assert.Same(array, target.Array);
        Assert.Same(element, target.Array[0]);
        Assert.Same(names, target.Names);
        Assert.Same(numbers, target.Numbers);
        Assert.Same(text, target.Child.Text);
        Assert.Same(name, target.Names[1]);
        Assert.Equal(42, target.Child.Number);
        Assert.Equal(7, target.Items[0].Number);
        Assert.Equal(8, target.Array[0].Number);
        Assert.Equal(source.Numbers, target.Numbers);
        Assert.Equal(99, target.Tail);

        for (int i = 0; i < 128; i++)
            LuminPackSerializer.Deserialize<ReuseGraph, TOptions>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++)
            LuminPackSerializer.Deserialize<ReuseGraph, TOptions>(bytes, ref target);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    /// <summary>Checks generic member dispatch retains instances and clears incoming null records.</summary>
    [Fact]
    public void GenericFieldsReuseAndClearTheirExistingInstances()
    {
        var target = new GenericGraph<ReuseNode> { Child = new(), Items = [new()] };
        var child = target.Child;
        var item = target.Items[0];
        byte[] bytes = LuminPackSerializer.Serialize(new GenericGraph<ReuseNode>
        {
            Child = new() { Number = 14 }, Items = [new() { Number = 15 }]
        });
        LuminPackSerializer.Deserialize<GenericGraph<ReuseNode>>(bytes, ref target);
        Assert.Same(child, target.Child);
        Assert.Same(item, target.Items[0]);
        Assert.Equal(14, child.Number);
        Assert.Equal(15, item.Number);
        for (int i = 0; i < 128; i++) LuminPackSerializer.Deserialize<GenericGraph<ReuseNode>>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) LuminPackSerializer.Deserialize<GenericGraph<ReuseNode>>(bytes, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        bytes = LuminPackSerializer.Serialize(new GenericGraph<ReuseNode> { Items = [null!] });
        LuminPackSerializer.Deserialize<GenericGraph<ReuseNode>>(bytes, ref target);
        Assert.Null(target.Child);
        Assert.Null(target.Items[0]);

        var value = new GenericValue<ReuseNode> { Child = child };
        bytes = LuminPackSerializer.Serialize(new GenericValue<ReuseNode> { Child = new() { Number = 16 } });
        LuminPackSerializer.Deserialize<GenericValue<ReuseNode>>(bytes, ref value);
        Assert.Same(child, value.Child);
        Assert.Equal(16, child.Number);
        bytes = LuminPackSerializer.Serialize(new GenericValue<ReuseNode>());
        LuminPackSerializer.Deserialize<GenericValue<ReuseNode>>(bytes, ref value);
        Assert.Null(value.Child);

        var versioned = new GenericVersioned<ReuseNode> { Child = child };
        bytes = LuminPackSerializer.Serialize(new GenericVersioned<ReuseNode>());
        LuminPackSerializer.Deserialize<GenericVersioned<ReuseNode>>(bytes, ref versioned);
        Assert.Null(versioned.Child);
        var tracked = new GenericTracked<ReuseNode> { Child = child };
        bytes = LuminPackSerializer.Serialize(new GenericTracked<ReuseNode>());
        LuminPackSerializer.Deserialize<GenericTracked<ReuseNode>>(bytes, ref tracked);
        Assert.Null(tracked.Child);
    }

    /// <summary>Checks null collection records retain the original static formatter callback count.</summary>
    [Fact]
    public void NullFormatterElementsPreserveStaticCallbacks()
    {
        byte[] bytes = LuminPackSerializer.Serialize(new List<StaticCallbackNode> { null! });
        StaticCallbackNode.Reads = 0;
        _ = LuminPackSerializer.Deserialize<List<StaticCallbackNode>>(bytes);
        Assert.Equal(1, StaticCallbackNode.Reads);
        StaticCallbackNode.Reads = 0;
        var list = new List<StaticCallbackNode> { new() };
        LuminPackSerializer.Deserialize<List<StaticCallbackNode>>(bytes, ref list);
        Assert.Null(list[0]);
        Assert.Equal(1, StaticCallbackNode.Reads);
        bytes = LuminPackSerializer.Serialize(new StaticCallbackNode[] { null! });
        StaticCallbackNode.Reads = 0;
        var array = new StaticCallbackNode[] { new() };
        LuminPackSerializer.Deserialize<StaticCallbackNode[]>(bytes, ref array);
        Assert.Null(array[0]);
        Assert.Equal(1, StaticCallbackNode.Reads);
        bytes = LuminPackSerializer.Serialize(new Dictionary<int, StaticCallbackNode> { [1] = null! });
        StaticCallbackNode.Reads = 0;
        var map = new Dictionary<int, StaticCallbackNode> { [1] = new() };
        LuminPackSerializer.Deserialize<Dictionary<int, StaticCallbackNode>>(bytes, ref map);
        Assert.Null(map[1]);
        Assert.Equal(1, StaticCallbackNode.Reads);
        bytes = LuminPackSerializer.Serialize(new ExplicitCallbackMember());
        StaticCallbackNode.Reads = 0;
        var member = new ExplicitCallbackMember { Child = new() };
        LuminPackSerializer.Deserialize<ExplicitCallbackMember>(bytes, ref member);
        Assert.Null(member.Child);
        Assert.Equal(1, StaticCallbackNode.Reads);
        bytes = LuminPackSerializer.Serialize(new GenericVersioned<StaticCallbackNode>());
        StaticCallbackNode.Reads = 0;
        var versioned = new GenericVersioned<StaticCallbackNode> { Child = new() };
        LuminPackSerializer.Deserialize<GenericVersioned<StaticCallbackNode>>(bytes, ref versioned);
        Assert.Null(versioned.Child);
        Assert.Equal(1, StaticCallbackNode.Reads);
    }

    /// <summary>Checks empty and null members, collection growth and retained prefix elements.</summary>
    [Fact]
    public void CollectionsGrowShrinkAndClearWithoutStaleValues()
    {
        var target = new ReuseGraph { Items = [new(), new()], Array = [new()], Names = ["old"] };
        var items = target.Items;
        var first = items[0];
        var source = new ReuseGraph { Items = [new() { Number = 1 }], Names = [], Array = [], Numbers = [] };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize<ReuseGraph>(bytes, ref target);
        Assert.Same(items, target.Items);
        Assert.Same(first, target.Items[0]);
        Assert.Single(target.Items);
        Assert.Empty(target.Array);
        Assert.Empty(target.Names);

        source.Items = [new() { Number = 2 }, new() { Number = 3 }, null!];
        source.Child = null!;
        source.Array = null!;
        source.Names = null!;
        bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize<ReuseGraph>(bytes, ref target);
        Assert.Same(first, target.Items[0]);
        Assert.Equal(2, target.Items[0].Number);
        Assert.Equal(3, target.Items[1].Number);
        Assert.Null(target.Items[2]);
        Assert.Null(target.Child);
        Assert.Null(target.Array);
        Assert.Null(target.Names);
    }

    /// <summary>Checks that an existing constructor-bound instance does not invoke its constructor again.</summary>
    [Fact]
    public void ConstructorBoundInstanceAndPrivateStorageAreReused()
    {
        var source = new ConstructorNode(new() { Number = 71 }, 17);
        var target = new ConstructorNode(new(), 1);
        var root = target;
        var child = target.Child;
        byte[] bytes = LuminPackSerializer.Serialize(source);
        int constructions = ConstructorNode.Constructions;
        LuminPackSerializer.Deserialize<ConstructorNode>(bytes, ref target);
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Equal(71, target.Child.Number);
        Assert.Equal(17, target.Id);
        Assert.Equal(constructions, ConstructorNode.Constructions);
    }

    /// <summary>Checks compatible Union cases reuse their instances while changed tags replace them.</summary>
    [Fact]
    public void UnionReusesOnlyTheMatchingConcreteCase()
    {
        ReuseUnion target = new FirstCase { Child = new() };
        var root = target;
        var child = ((FirstCase)target).Child;
        byte[] bytes = LuminPackSerializer.Serialize<ReuseUnion>(new FirstCase { Child = new() { Number = 29 } });
        LuminPackSerializer.Deserialize<ReuseUnion>(bytes, ref target);
        Assert.Same(root, target);
        Assert.Same(child, ((FirstCase)target).Child);
        Assert.Equal(29, child.Number);
        for (int i = 0; i < 128; i++) LuminPackSerializer.Deserialize<ReuseUnion>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) LuminPackSerializer.Deserialize<ReuseUnion>(bytes, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        bytes = LuminPackSerializer.Serialize<ReuseUnion>(new SecondCase { Number = 5 });
        LuminPackSerializer.Deserialize<ReuseUnion>(bytes, ref target);
        Assert.Equal(5, Assert.IsType<SecondCase>(target).Number);
    }

    /// <summary>Checks version tables and tracked self references retain existing instances.</summary>
    [Fact]
    public void SpecializedModesReuseTheirGraphs()
    {
        var versioned = new VersionedNode { Child = new() };
        var versionedRoot = versioned;
        var child = versioned.Child;
        byte[] bytes = LuminPackSerializer.Serialize(new VersionedNode { Child = new() { Number = 13 } });
        LuminPackSerializer.Deserialize<VersionedNode>(bytes, ref versioned);
        Assert.Same(versionedRoot, versioned);
        Assert.Same(child, versioned.Child);
        Assert.Equal(13, child.Number);
        bytes = LuminPackSerializer.Serialize(new VersionedNode { Child = null! });
        LuminPackSerializer.Deserialize<VersionedNode>(bytes, ref versioned);
        Assert.Null(versioned.Child);

        var source = new TrackedNode { Number = 23 };
        source.Next = source;
        var tracked = new TrackedNode();
        var trackedRoot = tracked;
        bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize<TrackedNode>(bytes, ref tracked);
        Assert.Same(trackedRoot, tracked);
        Assert.Same(tracked, tracked.Next);
        Assert.Equal(23, tracked.Number);
    }

    /// <summary>Checks dictionary identity, comparers, existing values and pooled temporary cleanup.</summary>
    [Fact]
    public void DictionaryRetainsComparerAndReusesValuesByKey()
    {
        var first = new ReuseNode();
        var map = new Dictionary<string, ReuseNode>(StringComparer.OrdinalIgnoreCase) { ["first"] = first, ["stale"] = new() };
        var target = map;
        var source = new Dictionary<string, ReuseNode> { ["FIRST"] = new() { Number = 11 } };
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize<Dictionary<string, ReuseNode>>(bytes, ref target);
        Assert.Same(map, target);
        Assert.Same(StringComparer.OrdinalIgnoreCase, target.Comparer);
        Assert.Single(target);
        Assert.Same(first, target["first"]);
        Assert.Equal(11, first.Number);
        for (int i = 0; i < 128; i++) LuminPackSerializer.Deserialize<Dictionary<string, ReuseNode>>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) LuminPackSerializer.Deserialize<Dictionary<string, ReuseNode>>(bytes, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        var value = new ReuseNode();
        var integerKeys = new Dictionary<int, ReuseNode> { [7] = value };
        bytes = LuminPackSerializer.Serialize(new Dictionary<int, ReuseNode> { [7] = new() { Number = 12 } });
        LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>>(bytes, ref integerKeys);
        Assert.Same(value, integerKeys[7]);
        Assert.Equal(12, value.Number);
    }

    /// <summary>Checks an already rented graph avoids another rent and preserves callback receivers.</summary>
    [Fact]
    public void RentedInstancesReuseFieldsWithoutAnotherRent()
    {
        var target = PooledNode.Rent();
        var root = target;
        var child = target.Child;
        byte[] bytes = LuminPackSerializer.Serialize(new PooledNode { Child = new() { Number = 61 } });
        int rents = PooledNode.Rents;
        LuminPackSerializer.Deserialize<PooledNode>(bytes, ref target);
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Equal(61, child.Number);
        Assert.Equal(rents, PooledNode.Rents);
        Assert.Equal(1, target.Before);
        Assert.Equal(1, target.After);
    }

    /// <summary>Checks separate incoming reference identifiers cannot collapse onto one existing alias.</summary>
    [Fact]
    public void ReferenceTrackingSeparatesPreviouslyAliasedTargets()
    {
        var source = new TrackedNode { Next = new() { Number = 2 } };
        var target = new TrackedNode();
        target.Next = target;
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize<TrackedNode>(bytes, ref target);
        Assert.NotSame(target, target.Next);
        Assert.Equal(2, target.Next!.Number);
    }

    /// <summary>Checks the reusable identity index scales across tracked graphs and resets between operations.</summary>
    [Fact]
    public void LargerTrackedGraphsReuseWithoutAllocations()
    {
        var source = Enumerable.Range(0, 128).Select(index => new TrackedNode { Number = index }).ToArray();
        var target = Enumerable.Range(0, 128).Select(_ => new TrackedNode()).ToArray();
        for (int i = 0; i < source.Length; i++)
        {
            source[i].Next = source[(i + 1) % source.Length];
            target[i].Next = target[(i + 1) % target.Length];
        }
        var identity = (TrackedNode[])target.Clone();
        byte[] bytes = LuminPackSerializer.Serialize(source);
        for (int i = 0; i < 32; i++) LuminPackSerializer.Deserialize<TrackedNode[]>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 64; i++) LuminPackSerializer.Deserialize<TrackedNode[]>(bytes, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        for (int i = 0; i < source.Length; i++)
        {
            Assert.Same(identity[i], target[i]);
            Assert.Equal(i, target[i].Number);
            Assert.Same(target[(i + 1) % target.Length], target[i].Next);
        }
    }

    /// <summary>Checks nested collections and managed structs carry existing element roots through inline reads.</summary>
    [Fact]
    public void NestedCollectionsAndManagedStructsReuseTheirElements()
    {
        var source = new NestedCollections { Lists = [[new() { Number = 41 }]], Arrays = [[new() { Number = 51 }]] };
        var target = new NestedCollections { Lists = [[new()]], Arrays = [[new()]] };
        var list = target.Lists[0];
        var item = list[0];
        var array = target.Arrays[0];
        var element = array[0];
        byte[] bytes = LuminPackSerializer.Serialize(source);
        LuminPackSerializer.Deserialize<NestedCollections>(bytes, ref target);
        Assert.Same(list, target.Lists[0]);
        Assert.Same(item, target.Lists[0][0]);
        Assert.Same(array, target.Arrays[0]);
        Assert.Same(element, target.Arrays[0][0]);
        Assert.Equal(41, item.Number);
        Assert.Equal(51, element.Number);
        var managed = new ManagedNode { Child = new() };
        var child = managed.Child;
        bytes = LuminPackSerializer.Serialize(new ManagedNode { Child = new() { Number = 81 } });
        LuminPackSerializer.Deserialize<ManagedNode>(bytes, ref managed);
        Assert.Same(child, managed.Child);
        Assert.Equal(81, child.Number);
        var nested = new StructPair { Left = new() { Items = [new()] }, Right = new() { Items = [new()] } };
        var left = nested.Left.Items;
        var right = nested.Right.Items;
        bytes = LuminPackSerializer.Serialize(new StructPair
        {
            Left = new() { Items = [new() { Number = 82 }] },
            Right = new() { Items = [new() { Number = 83 }] }
        });
        LuminPackSerializer.Deserialize<StructPair>(bytes, ref nested);
        Assert.Same(left, nested.Left.Items);
        Assert.Same(right, nested.Right.Items);
        Assert.Equal(82, left[0].Number);
        Assert.Equal(83, right[0].Number);
    }

    /// <summary>Checks unchanged and changed Unicode strings through all compiled binary profiles.</summary>
    [Fact]
    public void StringReusePreservesTextAndNormalization()
    {
        CheckStringReuse<LuminPackDefaultOptions>();
        CheckStringReuse<LuminPackUtf8TokenOptions>();
        CheckStringReuse<LuminPackUtf16LengthOptions>();
        CheckStringReuse<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks unchanged string identity and normal allocation for changed immutable text.</summary>
    /// <typeparam name="TOptions">The binary string profile.</typeparam>
    private static void CheckStringReuse<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        foreach (string text in new[] { "a", "ASCII text", "\u4E2D\u6587", "\uD83D\uDE00\uD83D\uDE80", new string('x', 400),
            new string('\u4E2D', 513) + "\uD83D\uDE00", new string('x', 255) + "\uD83D\uDE80" + new string('\u6587', 300) })
        {
            byte[] bytes = LuminPackSerializer.Serialize<string, TOptions>(text);
            string target = new(text.ToCharArray());
            string identity = target;
            Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<string, TOptions>(bytes, ref target));
            Assert.Same(identity, target);
            target = "changed";
            LuminPackSerializer.Deserialize<string, TOptions>(bytes, ref target);
            Assert.Equal(text, target);
            for (int i = 0; i < 32; i++) LuminPackSerializer.Deserialize<string, TOptions>(bytes, ref target);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 128; i++) LuminPackSerializer.Deserialize<string, TOptions>(bytes, ref target);
            Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        }
        byte[] nullBytes = LuminPackSerializer.Serialize<string?, TOptions>(null);
        string? nullable = "before";
        LuminPackSerializer.Deserialize<string?, TOptions>(nullBytes, ref nullable);
        Assert.Equal(default(TOptions).StringRecording == LuminPackStringRecording.Token ? "" : null, nullable);
    }

    /// <summary>Checks reuse cannot bypass original truncated or malformed string failures.</summary>
    [Fact]
    public void StringReusePreservesMalformedInputFailures()
    {
        CheckMalformedStrings<LuminPackDefaultOptions>();
        CheckMalformedStrings<LuminPackUtf8TokenOptions>();
        CheckMalformedStrings<LuminPackUtf16LengthOptions>();
        CheckMalformedStrings<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks ASCII matching preserves header validation and cursor publication at unaligned offsets.</summary>
    [Fact]
    public void AsciiReusePreservesHeadersAndCursor()
    {
        foreach (int length in new[] { 0, 1, 7, 8, 15, 16, 31, 32, 63, 64, 65, 129, 4096 })
        {
            string text = new('x', length);
            byte[] record = LuminPackSerializer.Serialize(text);
            CompareAsciiStringRecord(record, new string(text.ToCharArray()), true);
            CompareAsciiStringRecord(record, new string('y', length));
            // Distinct characters expose window and lane mistakes that a uniform fill cannot.
            var distinctChars = new char[length];
            var otherChars = new char[length];
            for (int index = 0; index < length; index++)
            {
                distinctChars[index] = (char)('A' + index % 26);
                otherChars[index] = (char)('a' + index % 26);
            }
            byte[] distinctRecord = LuminPackSerializer.Serialize(new string(distinctChars));
            CompareAsciiStringRecord(distinctRecord, new string(distinctChars), true);
            CompareAsciiStringRecord(distinctRecord, new string(otherChars));
            if (length != 0)
            {
                byte[] invalid = (byte[])record.Clone();
                invalid[8] = 0xFF;
                CompareAsciiStringRecord(invalid, text);
            }
            foreach (int count in new[] { 0, 1, 3, 4, 7, 8, record.Length - 1 }.Distinct())
                CompareAsciiStringRecord(record.AsSpan(0, count).ToArray(), text);
            foreach (int characters in new[] { -1, 0, length, length + 1, int.MaxValue })
            {
                byte[] changed = (byte[])record.Clone();
                BinaryPrimitives.WriteInt32LittleEndian(changed.AsSpan(4), characters);
                CompareAsciiStringRecord(changed, text);
            }
            foreach (int bytes in new[] { int.MinValue, -2, -1, 0, int.MaxValue })
            {
                byte[] changed = (byte[])record.Clone();
                BinaryPrimitives.WriteInt32LittleEndian(changed, bytes);
                CompareAsciiStringRecord(changed, text);
            }
        }
    }

    /// <summary>Compares ordinary and reusable reads, including unchanged caller cursors after failure.</summary>
    /// <param name="record">The complete or malformed ASCII record.</param>
    /// <param name="candidate">The immutable string available for reuse.</param>
    /// <param name="same">Whether a successful read must retain the candidate instance.</param>
    private static void CompareAsciiStringRecord(byte[] record, string candidate, bool same = false)
    {
        byte[] bytes = new byte[record.Length + 3];
        record.CopyTo(bytes, 3);
        ReadOnlySpan<byte> input = bytes;
        var freshReader = new LuminPackReader(ref input);
        var reuseReader = new LuminPackReader(ref input);
        int freshIndex = 3;
        int reuseIndex = 3;
        string? fresh = null;
        string? reused = null;
        Exception? freshError = null;
        Exception? reuseError = null;
        try { fresh = freshReader.ReadStringAndAdvance(ref freshIndex); }
        catch (Exception exception) { freshError = exception; }
        try { reused = reuseReader.ReadStringAndAdvance(ref reuseIndex, candidate); }
        catch (Exception exception) { reuseError = exception; }
        Assert.Equal(freshError?.GetType(), reuseError?.GetType());
        Assert.Equal(freshIndex, reuseIndex);
        if (freshError is null)
        {
            Assert.Equal(fresh, reused);
            if (same) Assert.Same(candidate, reused);
        }
    }

    /// <summary>Compares fresh and reusable string reads on every truncated prefix and an invalid payload.</summary>
    /// <typeparam name="TOptions">The binary string profile.</typeparam>
    private static void CheckMalformedStrings<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        byte[] bytes = LuminPackSerializer.Serialize<string, TOptions>("\u4E2D\uD83D\uDE00");
        for (int length = 0; length < bytes.Length; length++)
            CompareStringReads<TOptions>(bytes.AsSpan(0, length).ToArray());
        var malformed = (byte[])bytes.Clone();
        malformed[default(TOptions).StringRecording == LuminPackStringRecording.Token ? 0 :
            default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? 8 : 4] = 0xFF;
        CompareStringReads<TOptions>(malformed);
    }

    /// <summary>Requires the same successful text or exception type from both string paths.</summary>
    /// <typeparam name="TOptions">The binary string profile.</typeparam>
    /// <param name="bytes">A complete or malformed input record.</param>
    private static void CompareStringReads<TOptions>(byte[] bytes) where TOptions : struct, ILuminPackOptions
    {
        string? fresh = null;
        string? reused = "\u4E2D\uD83D\uDE00";
        Exception? first = null;
        Exception? second = null;
        try { LuminPackSerializer.Deserialize<string?, TOptions>(bytes, ref fresh); }
        catch (Exception exception) { first = exception; }
        try { LuminPackSerializer.Deserialize<string?, TOptions>(bytes, ref reused); }
        catch (Exception exception) { second = exception; }
        Assert.Equal(first?.GetType(), second?.GetType());
        if (first is null) Assert.Equal(fresh, reused);
    }

    /// <summary>Checks nullable element metadata still constructs missing instances and reuses present ones.</summary>
    [Fact]
    public void NullableCollectionElementsReuseOrConstructAsRequired()
    {
        var target = new NullableCollections { Items = [new(), null], Array = [new(), null] };
        var first = target.Items[0];
        var element = target.Array[0];
        byte[] bytes = LuminPackSerializer.Serialize(new NullableCollections
        {
            Items = [new() { Number = 14 }, new() { Number = 15 }],
            Array = [new() { Number = 24 }, new() { Number = 25 }]
        });
        LuminPackSerializer.Deserialize<NullableCollections>(bytes, ref target);
        Assert.Same(first, target.Items[0]);
        Assert.Same(element, target.Array[0]);
        Assert.Equal(15, target.Items[1]!.Number);
        Assert.Equal(25, target.Array[1]!.Number);
    }

    /// <summary>Checks root container and explicit formatter members clear incoming null objects.</summary>
    [Fact]
    public void NullElementsReplaceExistingValuesAtFormatterBoundaries()
    {
        var array = new ReuseNode?[] { new(), new() };
        var first = array[0];
        byte[] bytes = LuminPackSerializer.Serialize(new ReuseNode?[] { new() { Number = 6 }, null });
        LuminPackSerializer.Deserialize<ReuseNode?[]>(bytes, ref array);
        Assert.Same(first, array[0]);
        Assert.Null(array[1]);
        var list = new List<ReuseNode?> { new(), new() };
        first = list[0];
        bytes = LuminPackSerializer.Serialize(new List<ReuseNode?> { new() { Number = 7 }, null });
        LuminPackSerializer.Deserialize<List<ReuseNode?>>(bytes, ref list);
        Assert.Same(first, list[0]);
        Assert.Null(list[1]);
        var map = new Dictionary<int, ReuseNode?> { [1] = new() };
        bytes = LuminPackSerializer.Serialize(new Dictionary<int, ReuseNode?> { [1] = null });
        LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode?>>(bytes, ref map);
        Assert.Null(map[1]);
        var target = new ExplicitMember { Child = new() };
        bytes = LuminPackSerializer.Serialize(new ExplicitMember());
        LuminPackSerializer.Deserialize<ExplicitMember>(bytes, ref target);
        Assert.Null(target.Child);
    }

    /// <summary>Checks buffer and sequence facades retain the same recursive reuse semantics and consumed units.</summary>
    [Fact]
    public void BufferAndSequenceEntriesReuseExistingGraphs()
    {
        var source = new ReuseGraph { Child = new() { Number = 91 } };
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.Serialize(source, buffer);
        var target = new ReuseGraph();
        var root = target;
        var child = target.Child;
        Assert.Equal(buffer.GetSpan().Length, LuminPackSerializer.Deserialize<ReuseGraph>(buffer, ref target));
        byte[] bytes = buffer.GetSpan().ToArray();
        var first = new Segment(bytes.AsMemory(0, 2));
        var last = first.Append(bytes.AsMemory(2));
        var sequence = new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<ReuseGraph>(sequence, ref target));
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Equal(91, child.Number);
    }

    /// <summary>Provides mixed public and property storage with inline and formatter collection boundaries.</summary>
    [LuminPackable]
    public sealed class ReuseGraph
    {
        public ReuseNode Child { get; set; } = new();
        public List<ReuseNode> Items = [];
        public ReuseNode[] Array = [];
        public List<string> Names = [];
        public List<int> Numbers = [];
        public int Tail;
    }

    /// <summary>Provides inline child storage.</summary>
    [LuminPackable]
    public sealed class ReuseNode
    {
        public int Number;
        public string Text = "";
    }

    /// <summary>Provides constructor-bound automatic-property storage.</summary>
    [LuminPackable]
    public sealed class ConstructorNode
    {
        public static int Constructions;
        public ReuseNode Child { get; }
        public int Id { get; }

        /// <summary>Captures the child and identifier while counting actual construction.</summary>
        /// <param name="child">The initial child.</param>
        /// <param name="id">The initial identifier.</param>
        [LuminPackConstructor]
        public ConstructorNode(ReuseNode child, int id)
        {
            Child = child;
            Id = id;
            Constructions++;
        }
    }

    /// <summary>Provides a reference Union with two distinct cases.</summary>
    [LuminPackable, LuminPackUnion(0, typeof(FirstCase)), LuminPackUnion(1, typeof(SecondCase))]
    public abstract partial class ReuseUnion;

    /// <summary>Provides a Union case containing an inline child.</summary>
    [LuminPackable]
    public sealed partial class FirstCase : ReuseUnion
    {
        public ReuseNode Child = new();
    }

    /// <summary>Provides a replacement Union case.</summary>
    [LuminPackable]
    public sealed partial class SecondCase : ReuseUnion
    {
        public int Number;
    }

    /// <summary>Provides a stable length-table slot containing an object.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed class VersionedNode
    {
        [LuminPackOrder(0)] public ReuseNode Child = new();
    }

    /// <summary>Provides a tracked recursive object.</summary>
    [LuminPackable(GeneratorType.CircleReference)]
    public sealed class TrackedNode
    {
        [LuminPackOrder(0)] public int Number;
        [LuminPackOrder(1)] public TrackedNode? Next;
    }

    /// <summary>Provides an explicit pool entry and observable callback receivers.</summary>
    [LuminPackable]
    public sealed class PooledNode
    {
        public ReuseNode Child = new();
        public static int Rents;
        [LuminPackIgnore] public int Before;
        [LuminPackIgnore] public int After;

        /// <summary>Returns a graph from the test pool boundary.</summary>
        /// <returns>The rented graph.</returns>
        [LuminPackPoolRent]
        public static PooledNode Rent() { Rents++; return new(); }

        /// <summary>Records the existing receiver before reading members.</summary>
        [LuminPackOnDeserializing]
        public void OnReading() => Before++;

        /// <summary>Records the same receiver after committing members.</summary>
        [LuminPackOnDeserialized]
        public void OnRead() => After++;
    }

    /// <summary>Provides recursively nested inline container storage.</summary>
    [LuminPackable]
    public sealed class NestedCollections
    {
        public List<List<ReuseNode>> Lists = [];
        public ReuseNode[][] Arrays = [];
    }

    /// <summary>Provides a value root containing reusable reference storage.</summary>
    [LuminPackable]
    public struct ManagedNode
    {
        public ReuseNode Child;
    }

    /// <summary>Provides repeated managed struct layers with equal member names.</summary>
    [LuminPackable]
    public struct StructPair
    {
        public StructItems Left;
        public StructItems Right;
    }

    /// <summary>Provides collection storage inside a managed struct.</summary>
    [LuminPackable]
    public struct StructItems
    {
        public List<ReuseNode> Items;
    }

    /// <summary>Provides nullable reference elements across inline collection paths.</summary>
    [LuminPackable]
    public sealed class NullableCollections
    {
        public List<ReuseNode?> Items = [];
        public ReuseNode?[] Array = [];
    }

    /// <summary>Provides a member explicitly selecting the ordinary formatter boundary.</summary>
    [LuminPackable]
    public sealed class ExplicitMember
    {
        [LuminPackableObject] public ReuseNode? Child;
    }

    /// <summary>Provides generic reference members without reclassifying their original wire operations.</summary>
    [LuminPackable]
    public sealed class GenericGraph<T>
    {
        public T Child = default!;
        public List<T> Items = [];
    }

    /// <summary>Provides a generic reference slot in a managed value root.</summary>
    [LuminPackable]
    public struct GenericValue<T>
    {
        public T Child;
    }

    /// <summary>Provides a generic reference slot behind a version table.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed class GenericVersioned<T>
    {
        [LuminPackOrder(0)] public T Child = default!;
    }

    /// <summary>Provides a generic reference slot behind identity tracking.</summary>
    [LuminPackable(GeneratorType.CircleReference)]
    public sealed class GenericTracked<T> where T : class
    {
        [LuminPackOrder(0)] public T Child = default!;
    }

    /// <summary>Counts static callbacks on incoming null object records.</summary>
    [LuminPackable]
    public sealed class StaticCallbackNode
    {
        [LuminPackIgnore] public static int Reads;
        public int Number;

        /// <summary>Records each ordinary formatter entry before its null-header check.</summary>
        [LuminPackOnDeserializing]
        public static void OnReading() => Reads++;
    }

    /// <summary>Retains an explicit ordinary formatter boundary around a callback-bearing reference.</summary>
    [LuminPackable]
    public sealed class ExplicitCallbackMember
    {
        [LuminPackableObject] public StaticCallbackNode? Child;
    }

    /// <summary>Provides an explicit child formatter boundary that can fail before member parsing.</summary>
    [LuminPackable]
    public sealed class FailureChild
    {
        public static Exception? Failure;
        public int Number;

        /// <summary>Throws the caller-provided exception at the ordinary formatter boundary.</summary>
        [LuminPackOnDeserializing]
        public static void BeforeRead() { if (Failure != null) throw Failure; }
    }

    /// <summary>Places a staged primitive before an explicitly selected formatter.</summary>
    [LuminPackable]
    public sealed class FailureRoot
    {
        public int Number;
        [LuminPackableObject] public FailureChild Child = new();
    }

    /// <summary>Retains the same commit boundary inside named version framing.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed class VersionedFailureRoot
    {
        public int Number;
        [LuminPackableObject] public FailureChild Child = new();
    }

    /// <summary>Provides immutable input segments for the sequence facade.</summary>
    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        /// <summary>Wraps one input range.</summary>
        /// <param name="memory">The input bytes.</param>
        public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

        /// <summary>Appends one input range.</summary>
        /// <param name="memory">The following bytes.</param>
        /// <returns>The new tail.</returns>
        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var tail = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = tail;
            return tail;
        }
    }
}
