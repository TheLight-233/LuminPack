using System.Buffers.Binary;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public partial class DeserializeReuseTest
{
    /// <summary>Checks ordinary and named leaf maps reuse nodes and strings without warmed allocations.</summary>
    [Fact]
    public void DictionarySlotsReuseOrdinaryAndVersionedLeaves()
    {
        CheckDictionarySlots<LuminPackDefaultOptions>();
        CheckDictionarySlots<LuminPackUtf8TokenOptions>();
        CheckDictionarySlots<LuminPackUtf16LengthOptions>();
        CheckDictionarySlots<LuminPackUtf16TokenOptions>();
    }

    /// <summary>Checks identities, complete consumption and warmed allocation under one string profile.</summary>
    /// <typeparam name="TOptions">The compiled string representation.</typeparam>
    private static void CheckDictionarySlots<TOptions>() where TOptions : struct, ILuminPackOptions
    {
        var node = new ReuseNode { Text = new("leaf".ToCharArray()) };
        var map = new Dictionary<int, ReuseNode> { [1] = node };
        var versionedNode = new SlotVersionedLeaf { Text = new("named".ToCharArray()) };
        var versionedMap = new Dictionary<int, SlotVersionedLeaf> { [1] = versionedNode };
        var text = node.Text;
        var versionedText = versionedNode.Text;
        byte[] bytes = LuminPackSerializer.Serialize<Dictionary<int, ReuseNode>, TOptions>(
            new() { [1] = new() { Number = 17, Text = "leaf" } });
        byte[] versionedBytes = LuminPackSerializer.Serialize<Dictionary<int, SlotVersionedLeaf>, TOptions>(
            new() { [1] = new() { Number = 19, Text = "named" } });
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>, TOptions>(bytes, ref map));
        Assert.Equal(versionedBytes.Length, LuminPackSerializer.Deserialize<Dictionary<int, SlotVersionedLeaf>, TOptions>(versionedBytes, ref versionedMap));
        Assert.Same(node, map[1]);
        Assert.Same(versionedNode, versionedMap[1]);
        Assert.Same(text, node.Text);
        Assert.Same(versionedText, versionedNode.Text);
        Assert.Equal(17, node.Number);
        Assert.Equal(19, versionedNode.Number);
        for (int i = 0; i < 128; i++)
        {
            LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>, TOptions>(bytes, ref map);
            LuminPackSerializer.Deserialize<Dictionary<int, SlotVersionedLeaf>, TOptions>(versionedBytes, ref versionedMap);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++)
        {
            LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>, TOptions>(bytes, ref map);
            LuminPackSerializer.Deserialize<Dictionary<int, SlotVersionedLeaf>, TOptions>(versionedBytes, ref versionedMap);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Checks the approved retained ordering and stale-key removal for populated slots.</summary>
    [Fact]
    public void DictionarySlotsRetainOrderAndRemoveStaleKeys()
    {
        var first = new ReuseNode();
        var second = new ReuseNode();
        var map = new Dictionary<int, ReuseNode> { [1] = first, [2] = second, [9] = new() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, ReuseNode>
        {
            [2] = new() { Number = 22 }, [1] = new() { Number = 11 }
        });
        LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>>(bytes, ref map);
        Assert.Equal(new[] { 1, 2 }, map.Keys);
        Assert.Same(first, map[1]);
        Assert.Same(second, map[2]);
        Assert.Equal(11, first.Number);
        Assert.Equal(22, second.Number);
        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>>(bytes, ref map);
        Assert.True(enumerator.MoveNext());
    }

    /// <summary>Checks duplicate input still fails and a subsequent valid refill recovers.</summary>
    [Fact]
    public void DictionarySlotsRejectDuplicateKeysAndRecover()
    {
        var first = new ReuseNode { Number = 11 };
        var source = new Dictionary<int, ReuseNode> { [1] = first, [2] = new() { Number = 22 } };
        byte[] valid = LuminPackSerializer.Serialize(source);
        byte[] duplicate = (byte[])valid.Clone();
        int secondKey = sizeof(int) * 2 + LuminPackSerializer.Serialize(first).Length;
        BinaryPrimitives.WriteInt32LittleEndian(duplicate.AsSpan(secondKey), 1);
        var map = new Dictionary<int, ReuseNode> { [1] = new(), [9] = new() };
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>>(duplicate, ref map));
        Assert.Equal(2, map.Count);
        LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>>(valid, ref map);
        Assert.Equal(new[] { 1, 2 }, map.Keys);
        Assert.Equal(11, map[1].Number);
        Assert.Equal(22, map[2].Number);
    }

    /// <summary>Checks a cold constructor can clear and resize the dictionary without losing its parsed prefix.</summary>
    [Fact]
    public void DictionaryColdConstructionStagesTheParsedPrefix()
    {
        var first = new SlotConstructorLeaf();
        var map = new Dictionary<int, SlotConstructorLeaf?> { [1] = first, [2] = null, [9] = new() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotConstructorLeaf?>
        {
            [1] = new() { Number = 11 }, [2] = new() { Number = 22 }, [3] = null
        });
        SlotConstructorLeaf.Constructed = () =>
        {
            SlotConstructorLeaf.Constructed = null;
            map.Clear();
            for (int i = 100; i < 2048; i++) map.Add(i, first);
        };
        try { LuminPackSerializer.Deserialize<Dictionary<int, SlotConstructorLeaf?>>(bytes, ref map); }
        finally { SlotConstructorLeaf.Constructed = null; }
        Assert.Equal(new[] { 1, 2, 3 }, map.Keys);
        Assert.Same(first, map[1]);
        Assert.Equal(11, first.Number);
        Assert.Equal(22, map[2]!.Number);
        Assert.Null(map[3]);
    }

    /// <summary>Checks a missing key takes the staged cold branch and null records clear existing slots.</summary>
    [Fact]
    public void DictionarySlotsHandleMissingKeysAndIncomingNulls()
    {
        var map = new Dictionary<int, ReuseNode?> { [1] = new(), [9] = new() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, ReuseNode?>
        {
            [1] = null, [2] = new() { Number = 12 }
        });
        LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode?>>(bytes, ref map);
        Assert.Equal(new[] { 1, 2 }, map.Keys);
        Assert.Null(map[1]);
        Assert.Equal(12, map[2]!.Number);
    }

    /// <summary>Checks a pool rent uses cold staging and is skipped when every leaf is already present.</summary>
    [Fact]
    public void DictionarySlotsPreservePoolRentBoundaries()
    {
        var first = new SlotPooledLeaf();
        var map = new Dictionary<int, SlotPooledLeaf> { [1] = first };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotPooledLeaf>
        {
            [1] = new() { Number = 11 }, [2] = new() { Number = 22 }
        });
        SlotPooledLeaf.Rents = 0;
        SlotPooledLeaf.Renting = () => { map.Clear(); map.EnsureCapacity(2048); };
        try { LuminPackSerializer.Deserialize<Dictionary<int, SlotPooledLeaf>>(bytes, ref map); }
        finally { SlotPooledLeaf.Renting = null; }
        Assert.Equal(1, SlotPooledLeaf.Rents);
        Assert.Same(first, map[1]);
        Assert.Equal(11, first.Number);
        Assert.Equal(22, map[2].Number);
        LuminPackSerializer.Deserialize<Dictionary<int, SlotPooledLeaf>>(bytes, ref map);
        Assert.Equal(1, SlotPooledLeaf.Rents);
    }

    /// <summary>Checks a custom int comparer keeps the original public Clear/Add reconstruction.</summary>
    [Fact]
    public void DictionaryCustomIntComparerRetainsStagedOrdering()
    {
        var comparer = new SlotIntComparer();
        var map = new Dictionary<int, ReuseNode>(comparer) { [1] = new(), [2] = new() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, ReuseNode> { [2] = new(), [1] = new() });
        var enumerator = map.GetEnumerator();
        LuminPackSerializer.Deserialize<Dictionary<int, ReuseNode>>(bytes, ref map);
        Assert.Same(comparer, map.Comparer);
        Assert.Equal(new[] { 2, 1 }, map.Keys);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    /// <summary>Checks an observable value callback retains staging and may resize its dictionary safely.</summary>
    [Fact]
    public void DictionaryCallbacksKeepTheOriginalStagedPath()
    {
        var first = new SlotCallbackLeaf();
        var map = new Dictionary<int, SlotCallbackLeaf> { [1] = first, [2] = new() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotCallbackLeaf>
        {
            [2] = new() { Number = 22 }, [1] = new() { Number = 11 }
        });
        int callbacks = 0;
        SlotCallbackLeaf.Reading = () => { callbacks++; map.EnsureCapacity(2048); };
        try { LuminPackSerializer.Deserialize<Dictionary<int, SlotCallbackLeaf>>(bytes, ref map); }
        finally { SlotCallbackLeaf.Reading = null; }
        Assert.Equal(2, callbacks);
        Assert.Equal(new[] { 2, 1 }, map.Keys);
        Assert.Same(first, map[1]);
        Assert.Equal(11, first.Number);
    }

    /// <summary>Provides a flat named contract for the approved slot path.</summary>
    [LuminPackable(GeneratorType.VersionTolerant)]
    public sealed class SlotVersionedLeaf
    {
        public int Number;
        public string Text = "";
    }

    /// <summary>Provides user construction without read callbacks on an existing instance.</summary>
    [LuminPackable]
    public sealed class SlotConstructorLeaf
    {
        public static Action? Constructed;
        public int Number;

        /// <summary>Invokes the test's deliberately dictionary-mutating constructor hook.</summary>
        public SlotConstructorLeaf() => Constructed?.Invoke();
    }

    /// <summary>Provides an observable callback that excludes the slot path.</summary>
    [LuminPackable]
    public sealed class SlotCallbackLeaf
    {
        public static Action? Reading;
        public int Number;

        /// <summary>Runs user code at the original formatter callback boundary.</summary>
        [LuminPackOnDeserializing]
        public void BeforeRead() => Reading?.Invoke();
    }

    /// <summary>Provides a user-owned rent operation that can mutate its containing dictionary.</summary>
    [LuminPackable]
    public sealed class SlotPooledLeaf
    {
        public static int Rents;
        public static Action? Renting;
        public int Number;

        /// <summary>Runs the test's pool hook before returning the requested new leaf.</summary>
        /// <returns>A leaf supplied by the test pool.</returns>
        [LuminPackPoolRent]
        public static SlotPooledLeaf Rent()
        {
            Rents++;
            Renting?.Invoke();
            return new();
        }
    }

    /// <summary>Provides a non-default comparer with ordinary integer equality.</summary>
    private sealed class SlotIntComparer : IEqualityComparer<int>
    {
        /// <summary>Compares the original integer keys.</summary>
        /// <param name="x">The first key.</param>
        /// <param name="y">The second key.</param>
        /// <returns>Whether the keys are equal.</returns>
        public bool Equals(int x, int y) => x == y;

        /// <summary>Returns the original integer hash.</summary>
        /// <param name="obj">The key being hashed.</param>
        /// <returns>The integer hash.</returns>
        public int GetHashCode(int obj) => obj;
    }
}
