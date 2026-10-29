using System.Text;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Core;
using LuminPack.Option;
using LuminPack.Utility;
using Xunit;

namespace LuminPackUnitTest;

public partial class DeserializeReuseTest
{
    /// <summary>Checks JSON graph identity and warmed allocations in both encodings and syntax policies.</summary>
    [Fact]
    public void JsonExistingGraphIsReusedWithoutAllocations()
    {
        CheckJsonGraph<TestUtf8Length>();
        CheckJsonGraph<TestUtf16Length>();
        CheckJsonGraph<TestStrictUtf8Length>();
        CheckJsonGraph<TestStrictUtf16Length>();
    }

    /// <summary>Checks JSON byte entry reuse for one compiled document profile.</summary>
    /// <typeparam name="TOptions">The encoding and validation policy.</typeparam>
    private static void CheckJsonGraph<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var source = new ReuseGraph
        {
            Child = new() { Number = 42, Text = "\u4E2D\uD83D\uDE00" },
            Items = [new() { Number = 7, Text = "item" }],
            Array = [new() { Number = 8, Text = "array" }],
            Names = ["ascii", "quote\"\u00E9"], Numbers = [1, 2, 3], Tail = 99
        };
        string json = LuminPackSerializer.SerializeJson<ReuseGraph, TOptions>(source);
        byte[] bytes = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(json);
        var target = LuminPackSerializer.DeserializeJson<ReuseGraph, TOptions>(bytes);
        var root = target;
        var child = target.Child;
        var text = child.Text;
        var items = target.Items;
        var item = items[0];
        var array = target.Array;
        var element = array[0];
        var names = target.Names;
        var name = names[1];
        var numbers = target.Numbers;
        target.Tail = 0;
        Assert.Equal(bytes.Length, LuminPackSerializer.DeserializeJson<ReuseGraph, TOptions>(bytes, ref target));
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Same(text, child.Text);
        Assert.Same(items, target.Items);
        Assert.Same(item, items[0]);
        Assert.Same(array, target.Array);
        Assert.Same(element, array[0]);
        Assert.Same(names, target.Names);
        Assert.Same(name, names[1]);
        Assert.Same(numbers, target.Numbers);
        Assert.Equal(99, target.Tail);
        for (int i = 0; i < 128; i++) LuminPackSerializer.DeserializeJson<ReuseGraph, TOptions>(bytes, ref target);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) LuminPackSerializer.DeserializeJson<ReuseGraph, TOptions>(bytes, ref target);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Checks concrete text and buffer entries retain constructor-bound storage and avoid new pool rentals.</summary>
    [Fact]
    public void JsonEntriesReuseConstructorAndPoolStorage()
    {
        var target = new ConstructorNode(new ReuseNode(), 1);
        var root = target;
        var child = target.Child;
        int constructions = ConstructorNode.Constructions;
        LuminPackSerializer.DeserializeJson("{\"Child\":{\"Number\":12},\"Id\":9}", ref target);
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Equal(12, child.Number);
        Assert.Equal(9, target.Id);
        Assert.Equal(constructions, ConstructorNode.Constructions);
        LuminPackSerializer.DeserializeJson("{\"Child\":{\"Number\":13},\"Id\":10}".AsSpan(), ref target);
        Assert.Same(child, target.Child);
        Assert.Equal(13, child.Number);
        using var buffer = new LuminBufferWriter(true);
        LuminPackSerializer.SerializeJson(new ConstructorNode(new ReuseNode { Number = 14 }, 11), buffer);
        Assert.Equal(buffer.GetSpan().Length, LuminPackSerializer.DeserializeJson(buffer, ref target));
        Assert.Same(root, target);
        Assert.Same(child, target.Child);
        Assert.Equal(14, child.Number);

        var pooled = new PooledNode();
        int rents = PooledNode.Rents;
        LuminPackSerializer.DeserializeJson<PooledNode>("{\"Child\":{\"Number\":15}}", ref pooled);
        Assert.Equal(rents, PooledNode.Rents);
        Assert.Equal(15, pooled.Child.Number);
        PooledNode fresh = null!;
        LuminPackSerializer.DeserializeJson<PooledNode>("{\"Child\":{\"Number\":16}}", ref fresh);
        Assert.Equal(rents + 1, PooledNode.Rents);
        Assert.Equal(16, fresh.Child.Number);
        var shared = PoolBackedNode.Shared;
        var sharedChild = shared.Child;
        rents = PoolBackedNode.Rents;
        var rented = LuminPackSerializer.DeserializeJson<PoolBackedNode>("{\"Child\":{\"Number\":17}}");
        Assert.Same(shared, rented);
        Assert.Same(sharedChild, rented.Child);
        Assert.Equal(17, sharedChild.Number);
        Assert.Equal(rents + 1, PoolBackedNode.Rents);
    }

    /// <summary>Checks missing members reset, duplicate properties update and explicit null clears JSON targets.</summary>
    [Fact]
    public void JsonMissingAndNullMembersKeepTheirSemantics()
    {
        var target = new ReuseGraph { Tail = 1, Items = [new()] };
        var root = target;
        LuminPackSerializer.DeserializeJson<ReuseGraph>("{\"Tail\":2,\"unknown\":[{}],\"Tail\":3}", ref target);
        Assert.Same(root, target);
        Assert.Equal(3, target.Tail);
        Assert.Null(target.Child);
        Assert.Null(target.Items);
        LuminPackSerializer.DeserializeJson<ReuseGraph>("null", ref target);
        Assert.Null(target);
        var managed = new ManagedNode { Child = new() };
        var child = managed.Child;
        LuminPackSerializer.DeserializeJson<ManagedNode>("{\"Child\":{\"Number\":17}}", ref managed);
        Assert.Same(child, managed.Child);
        Assert.Equal(17, child.Number);
        var generic = new GenericGraph<ReuseNode> { Child = new(), Items = [new()] };
        child = generic.Child;
        var item = generic.Items[0];
        LuminPackSerializer.DeserializeJson<GenericGraph<ReuseNode>>("{\"Child\":{\"Number\":18},\"Items\":[{\"Number\":19}]}", ref generic);
        Assert.Same(child, generic.Child);
        Assert.Same(item, generic.Items[0]);
        Assert.Equal(18, child.Number);
        Assert.Equal(19, item.Number);
    }

    /// <summary>Checks sequence storage reuse, array replacement, list shrink and unknown implementation replacement.</summary>
    [Fact]
    public void JsonCollectionsReuseOnlyCompatibleStorage()
    {
        var child = new ReuseNode();
        var list = new List<ReuseNode>(8) { child, new() };
        var original = list;
        LuminPackSerializer.DeserializeJson<List<ReuseNode>>("[{\"Number\":21}]", ref list);
        Assert.Same(original, list);
        Assert.Same(child, Assert.Single(list));
        Assert.Equal(8, list.Capacity);
        IEnumerable<ReuseNode> sequence = list;
        LuminPackSerializer.DeserializeJson<IEnumerable<ReuseNode>>("[{\"Number\":22}]", ref sequence);
        Assert.Same(list, sequence);
        Assert.Same(child, list[0]);
        var array = new[] { child };
        sequence = array;
        LuminPackSerializer.DeserializeJson<IEnumerable<ReuseNode>>("[{\"Number\":23}]", ref sequence);
        Assert.Same(array, sequence);
        Assert.Same(child, array[0]);
        LuminPackSerializer.DeserializeJson<ReuseNode[]>("[{\"Number\":24},null]", ref array);
        Assert.Equal(2, array.Length);
        Assert.NotSame(child, array[0]);
        Assert.Null(array[1]);
        sequence = new HashSet<ReuseNode> { child };
        LuminPackSerializer.DeserializeJson<IEnumerable<ReuseNode>>("[]", ref sequence);
        Assert.IsType<List<ReuseNode>>(sequence);
        Assert.Empty(sequence);
    }

    /// <summary>Checks key matching retains comparers and values while removed and missing entries follow JSON rules.</summary>
    [Fact]
    public void JsonDictionariesReuseMatchingValues()
    {
        var child = new ReuseNode();
        var dictionary = new Dictionary<string, ReuseNode>(StringComparer.OrdinalIgnoreCase) { ["key"] = child, ["removed"] = new() };
        var original = dictionary;
        LuminPackSerializer.DeserializeJson<Dictionary<string, ReuseNode>>("[[\"KEY\",{\"Number\":31}],[\"new\",null]]", ref dictionary);
        Assert.Same(original, dictionary);
        Assert.Same(StringComparer.OrdinalIgnoreCase, dictionary.Comparer);
        Assert.Same(child, dictionary["key"]);
        Assert.Equal(31, child.Number);
        Assert.False(dictionary.ContainsKey("removed"));
        Assert.Null(dictionary["new"]);
        IDictionary<string, ReuseNode> view = dictionary;
        LuminPackSerializer.DeserializeJson<IDictionary<string, ReuseNode>>("[[\"key\",{\"Number\":32}]]", ref view);
        Assert.Same(dictionary, view);
        Assert.Same(child, view["key"]);
        var numbers = new Dictionary<string, int> { ["key"] = 99 };
        LuminPackSerializer.DeserializeJson<Dictionary<string, int>>("[[\"key\"]]", ref numbers);
        Assert.Equal(0, numbers["key"]);
    }

    /// <summary>Checks mutable slices retain their offsets and unchanged dictionary strings allocate nothing after warm-up.</summary>
    [Fact]
    public void JsonMutableSlicesAndDictionaryStringsReuseStorage()
    {
        var array = new[] { new ReuseNode(), new ReuseNode(), new ReuseNode() };
        var child = array[1];
        Memory<ReuseNode> memory = array.AsMemory(1, 1);
        LuminPackSerializer.DeserializeJson<Memory<ReuseNode>>("[{\"Number\":35}]", ref memory);
        Assert.True(MemoryMarshal.TryGetArray((ReadOnlyMemory<ReuseNode>)memory, out var backing));
        Assert.Same(array, backing.Array);
        Assert.Equal(1, backing.Offset);
        Assert.Same(child, memory.Span[0]);
        var segment = new ArraySegment<ReuseNode>(array, 1, 1);
        LuminPackSerializer.DeserializeJson<ArraySegment<ReuseNode>>("[{\"Number\":36}]", ref segment);
        Assert.Same(array, segment.Array);
        Assert.Equal(1, segment.Offset);
        Assert.Same(child, segment[0]);
        Assert.Equal(36, child.Number);
        var map = new Dictionary<string, string> { ["key"] = "quote\"\u00E9" };
        var text = map["key"];
        byte[] bytes = Encoding.UTF8.GetBytes("[[\"key\",\"quote\\\"\\u00e9\"]]");
        for (int i = 0; i < 128; i++) LuminPackSerializer.DeserializeJson<Dictionary<string, string>, TestUtf8Length>(bytes, ref map);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) LuminPackSerializer.DeserializeJson<Dictionary<string, string>, TestUtf8Length>(bytes, ref map);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.Same(text, map["key"]);
    }

    /// <summary>Checks exact Union case reuse, case replacement and incoming shared cycles.</summary>
    [Fact]
    public void JsonUnionAndReferenceTrackingReuseGraphs()
    {
        ReuseUnion target = new FirstCase();
        var original = (FirstCase)target;
        var child = original.Child;
        string json = LuminPackSerializer.SerializeJson<ReuseUnion>(new FirstCase { Child = new() { Number = 41 } });
        LuminPackSerializer.DeserializeJson<ReuseUnion>(json, ref target);
        Assert.Same(original, target);
        Assert.Same(child, original.Child);
        Assert.Equal(41, child.Number);
        json = LuminPackSerializer.SerializeJson<ReuseUnion>(new SecondCase { Number = 42 });
        LuminPackSerializer.DeserializeJson<ReuseUnion>(json, ref target);
        Assert.Equal(42, Assert.IsType<SecondCase>(target).Number);
        var source = new TrackedNode { Number = 43 };
        source.Next = source;
        var tracked = new TrackedNode();
        var trackedRoot = tracked;
        json = LuminPackSerializer.SerializeJson(source);
        LuminPackSerializer.DeserializeJson<TrackedNode>(json, ref tracked);
        Assert.Same(trackedRoot, tracked);
        Assert.Same(tracked, tracked.Next);
        Assert.Equal(43, tracked.Number);
        tracked.Next = tracked;
        LuminPackSerializer.DeserializeJson<TrackedNode>("{\"$id\":0,\"Number\":44,\"Next\":{\"$id\":1,\"Number\":45,\"Next\":null}}", ref tracked);
        Assert.Same(trackedRoot, tracked);
        Assert.NotSame(tracked, tracked.Next);
        Assert.Equal(44, tracked.Number);
        Assert.Equal(45, tracked.Next!.Number);
    }

    /// <summary>Checks array lookahead preserves mutable deep grammar state and the original cursor in both encodings.</summary>
    [Fact]
    public void JsonArrayLookaheadPreservesDeepReaderState()
    {
        CheckJsonLookahead<TestStrictUtf8Length>();
        CheckJsonLookahead<TestStrictUtf16Length>();
    }

    /// <summary>Counts a nested array without publishing the lookahead's grammar or cursor changes.</summary>
    /// <typeparam name="TOptions">The strict JSON document encoding.</typeparam>
    private static void CheckJsonLookahead<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        string json = new string('[', 70) + "\"\\u4e2d\"" + new string(']', 70);
        ReadOnlySpan<byte> bytes = (default(TOptions).StringEncoding == LuminPackStringEncoding.UTF8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(json);
        var reader = new LuminPackJsonReader(ref bytes, new LuminPackReaderOptionalState());
        for (int i = 0; i < 70; i++) Assert.True(reader.Read<TOptions>());
        int position = reader.CurrentIndex;
        byte grammar = reader.GrammarState;
        byte[] kinds = reader.DeepContainerKinds!.ToArray();
        Assert.Equal(1, reader.CountArrayElements<TOptions>());
        Assert.Equal(position, reader.CurrentIndex);
        Assert.Equal(70, reader.Depth);
        Assert.Equal(grammar, reader.GrammarState);
        Assert.Equal(kinds, reader.DeepContainerKinds);
        reader.Skip<TOptions>();
        while (reader.Read<TOptions>()) { }
        reader.EnsureEndOfDocument<TOptions>();
    }

    /// <summary>Checks lookahead rejects truncated nested payloads without invalidating subsequent reader state.</summary>
    /// <param name="json">The malformed array document.</param>
    [Theory]
    [InlineData("[{\"Number\":1}")]
    [InlineData("[\"unterminated]")]
    [InlineData("[{\"Number\":1},]")]
    public void JsonReuseRejectsMalformedArrays(string json)
    {
        var array = new[] { new ReuseNode() };
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.DeserializeJson<ReuseNode[], TestStrictUtf8Length>(json, ref array));
        LuminPackSerializer.DeserializeJson<ReuseNode[], TestStrictUtf8Length>("[{\"Number\":51}]", ref array);
        Assert.Equal(51, array[0].Number);
    }
}
