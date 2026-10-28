using System.Buffers.Binary;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

public partial class DeserializeReuseTest
{
    /// <summary>Checks exact fixed Union cases are refilled in place without warmed allocations or enumerator invalidation.</summary>
    [Fact]
    public void UnionSlotsRefillExactCasesWithoutAllocations()
    {
        var first = new SlotUnionPoint();
        var second = new SlotUnionVector();
        var map = new Dictionary<int, SlotUnion> { [1] = first, [2] = second };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotUnion>
        {
            [1] = new SlotUnionPoint { Id = 7, X = 1.5f }, [2] = new SlotUnionVector { Id = 8, X = 2.5f, Y = 3.5f }
        });
        var enumerator = map.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(bytes, ref map));
        Assert.True(enumerator.MoveNext());
        Assert.Same(first, map[1]);
        Assert.Same(second, map[2]);
        Assert.Equal((7, 1.5f), (first.Id, first.X));
        Assert.Equal((8, 2.5f, 3.5f), (second.Id, second.X, second.Y));
        Assert.Equal(bytes, LuminPackSerializer.Serialize(map));
        for (int i = 0; i < 128; i++) LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(bytes, ref map);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1024; i++) LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(bytes, ref map);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Checks the approved retained ordering and stale-key removal for refilled Union slots.</summary>
    [Fact]
    public void UnionSlotsRetainOrderAndRemoveStaleKeys()
    {
        var first = new SlotUnionPoint();
        var second = new SlotUnionVector();
        var map = new Dictionary<int, SlotUnion> { [1] = first, [2] = second, [9] = new SlotUnionPoint() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotUnion>
        {
            [2] = new SlotUnionVector { Id = 22 }, [1] = new SlotUnionPoint { Id = 11 }
        });
        LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(bytes, ref map);
        Assert.Equal(new[] { 1, 2 }, map.Keys);
        Assert.Same(first, map[1]);
        Assert.Same(second, map[2]);
        Assert.Equal(11, first.Id);
        Assert.Equal(22, second.Id);
    }

    /// <summary>Checks a derived or different case is constructed by the staged continuation, even when construction resizes the map.</summary>
    [Fact]
    public void UnionSlotsStageMismatchedCasesBeforeConstruction()
    {
        var first = new SlotUnionPoint();
        var map = new Dictionary<int, SlotUnion> { [1] = first, [2] = new SlotUnionPoint(), [3] = new SlotUnionVector() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotUnion>
        {
            [1] = new SlotUnionPoint { Id = 11 }, [2] = new SlotUnionVector { Id = 22, Y = 2 }, [3] = new SlotUnionPoint { Id = 33 }
        });
        int constructed = 0;
        SlotUnionVector.Constructed = () =>
        {
            constructed++;
            map.Clear();
            for (int i = 100; i < 2048; i++) map.Add(i, first);
        };
        try { LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(bytes, ref map); }
        finally { SlotUnionVector.Constructed = null; }
        Assert.Equal(1, constructed);
        Assert.Equal(new[] { 1, 2, 3 }, map.Keys);
        Assert.Same(first, map[1]);
        Assert.Equal(11, first.Id);
        Assert.Equal((22, 2f), (Assert.IsType<SlotUnionVector>(map[2]).Id, ((SlotUnionVector)map[2]).Y));
        Assert.Equal(33, Assert.IsType<SlotUnionPoint>(map[3]).Id);
    }

    /// <summary>Checks missing keys, null records, string and callback cases keep their original staged semantics.</summary>
    [Fact]
    public void UnionSlotsKeepUncertifiedCasesOnTheStagedPath()
    {
        var text = new SlotUnionText();
        var callback = new SlotUnionCallback();
        var map = new Dictionary<int, SlotUnion?> { [1] = new SlotUnionPoint(), [2] = text, [3] = callback, [9] = null };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotUnion?>
        {
            [1] = null, [2] = new SlotUnionText { Id = 2, Text = "two" }, [3] = new SlotUnionCallback { Id = 3 }, [4] = new SlotUnionPoint { Id = 4 }
        });
        int callbacks = 0;
        SlotUnionCallback.Reading = () => callbacks++;
        try { LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion?>>(bytes, ref map); }
        finally { SlotUnionCallback.Reading = null; }
        Assert.Equal(1, callbacks);
        Assert.Equal(new[] { 1, 2, 3, 4 }, map.Keys);
        Assert.Null(map[1]);
        Assert.Same(text, map[2]);
        Assert.Equal("two", text.Text);
        Assert.Same(callback, map[3]);
        Assert.Equal(3, callback.Id);
        Assert.Equal(4, Assert.IsType<SlotUnionPoint>(map[4]).Id);
    }

    /// <summary>Checks duplicate keys still fail after in-place refills and a valid read recovers.</summary>
    [Fact]
    public void UnionSlotsRejectDuplicateKeysAndRecover()
    {
        var source = new Dictionary<int, SlotUnion> { [1] = new SlotUnionPoint { Id = 11 }, [2] = new SlotUnionPoint { Id = 22 } };
        byte[] valid = LuminPackSerializer.Serialize(source);
        byte[] duplicate = (byte[])valid.Clone();
        int secondKey = sizeof(int) * 2 + LuminPackSerializer.Serialize<SlotUnion>(source[1]).Length;
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(duplicate.AsSpan(secondKey)));
        BinaryPrimitives.WriteInt32LittleEndian(duplicate.AsSpan(secondKey), 1);
        var map = new Dictionary<int, SlotUnion> { [1] = new SlotUnionPoint(), [2] = new SlotUnionPoint() };
        Assert.Throws<ArgumentException>(() => LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(duplicate, ref map));
        LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(valid, ref map);
        Assert.Equal(new[] { 1, 2 }, map.Keys);
        Assert.Equal(11, map[1].Id);
        Assert.Equal(22, map[2].Id);
    }

    /// <summary>Checks a custom int comparer keeps the original public Clear/Add reconstruction for Union values.</summary>
    [Fact]
    public void UnionSlotsCustomComparerRetainsStagedOrdering()
    {
        var comparer = new SlotIntComparer();
        var first = new SlotUnionPoint();
        var map = new Dictionary<int, SlotUnion>(comparer) { [1] = first, [2] = new SlotUnionPoint() };
        byte[] bytes = LuminPackSerializer.Serialize(new Dictionary<int, SlotUnion> { [2] = new SlotUnionPoint(), [1] = new SlotUnionPoint { Id = 5 } });
        var enumerator = map.GetEnumerator();
        LuminPackSerializer.Deserialize<Dictionary<int, SlotUnion>>(bytes, ref map);
        Assert.Same(comparer, map.Comparer);
        Assert.Equal(new[] { 2, 1 }, map.Keys);
        Assert.Same(first, map[1]);
        Assert.Equal(5, first.Id);
        Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
    }

    /// <summary>Provides a local narrow class Union with fixed, derived, string and callback cases.</summary>
    [LuminPackable, LuminPackUnion(0, typeof(SlotUnionPoint)), LuminPackUnion(1, typeof(SlotUnionVector)),
     LuminPackUnion(2, typeof(SlotUnionText)), LuminPackUnion(3, typeof(SlotUnionCallback))]
    public abstract partial class SlotUnion
    {
        public int Id;
    }

    /// <summary>Provides a fixed case with a derived fixed case.</summary>
    [LuminPackable]
    public partial class SlotUnionPoint : SlotUnion
    {
        public float X;
    }

    /// <summary>Provides a derived fixed case whose parameterless constructor runs only for fresh cases.</summary>
    [LuminPackable]
    public sealed partial class SlotUnionVector : SlotUnionPoint
    {
        public static Action? Constructed;
        public float Y;

        /// <summary>Invokes the test's deliberately dictionary-mutating construction hook.</summary>
        public SlotUnionVector() => Constructed?.Invoke();
    }

    /// <summary>Provides a variable-length case outside the fixed slot certification.</summary>
    [LuminPackable]
    public sealed partial class SlotUnionText : SlotUnion
    {
        public string Text = "";
    }

    /// <summary>Provides a callback case outside the slot certification.</summary>
    [LuminPackable]
    public sealed partial class SlotUnionCallback : SlotUnion
    {
        public static Action? Reading;

        /// <summary>Runs user code at the original formatter callback boundary.</summary>
        [LuminPackOnDeserialized]
        public void AfterRead() => Reading?.Invoke();
    }
}
