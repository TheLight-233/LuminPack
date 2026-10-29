using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks interface-set materialization through the existing HashSet storage view.</summary>
public class SetInterfaceBulkReadTest
{
    /// <summary>Checks framing, bucket links, iteration and mutations across empty and nonempty sets.</summary>
    /// <param name="count">The element count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    [InlineData(200)]
    public void InterfaceSetsRetainTheHashSetWire(int count)
    {
        var expected = Enumerable.Range(0, count).Select(i => i * 7919 % 100003 - 50000).ToHashSet();
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(bytes, LuminPackSerializer.Serialize<ISet<int>>(expected));
        Assert.Equal(bytes, LuminPackSerializer.Serialize<IReadOnlySet<int>>(expected));
        var mutable = LuminPackSerializer.Deserialize<ISet<int>>(bytes);
        var readOnly = LuminPackSerializer.Deserialize<IReadOnlySet<int>>(bytes);
        Assert.True(expected.SetEquals(mutable));
        Assert.True(expected.SetEquals(readOnly));
        Assert.True(mutable.Add(int.MaxValue));
        Assert.True(mutable.Remove(int.MaxValue));
        Assert.True(((HashSet<int>)readOnly).Add(int.MaxValue));
        Assert.True(((HashSet<int>)readOnly).Remove(int.MaxValue));
        Assert.True(expected.SetEquals(mutable));
        Assert.True(expected.SetEquals(readOnly));
    }

    /// <summary>Checks null markers and replacement of caller-supplied interface implementations.</summary>
    [Fact]
    public void NullAndReplacementRulesRemainUnchanged()
    {
        ISet<int> source = null!;
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Null(LuminPackSerializer.Deserialize<ISet<int>>(bytes));
        Assert.Null(LuminPackSerializer.Deserialize<IReadOnlySet<int>>(bytes));
        source = new SortedSet<int> { 3, 7 };
        bytes = LuminPackSerializer.Serialize(source);
        ISet<int> result = new SortedSet<int> { 99 };
        var previous = result;
        LuminPackSerializer.Deserialize(bytes.AsSpan(), ref result);
        Assert.NotSame(previous, result);
        Assert.IsType<HashSet<int>>(result);
        Assert.True(source.SetEquals(result));
        Assert.Equal(new[] { 99 }, previous);
    }

    /// <summary>Checks null strings, hash collisions and the approved absence of duplicate normalization.</summary>
    [Fact]
    public void BucketConstructionDoesNotCompareElements()
    {
        var input = new[] { new SetCollisionValue { Value = 1 }, new SetCollisionValue { Value = 2 } };
        byte[] bytes = LuminPackSerializer.Serialize(input);
        SetCollisionValue.Comparisons = 0;
        var values = LuminPackSerializer.Deserialize<ISet<SetCollisionValue>>(bytes);
        Assert.Equal(0, SetCollisionValue.Comparisons);
        Assert.Equal(new[] { 1, 2 }, values.Select(value => value.Value));
        SetCollisionValue.Comparisons = 0;
        var readOnly = LuminPackSerializer.Deserialize<IReadOnlySet<SetCollisionValue>>(bytes);
        Assert.Equal(0, SetCollisionValue.Comparisons);
        Assert.Equal(new[] { 1, 2 }, readOnly.Select(value => value.Value));

        bytes = LuminPackSerializer.Serialize(new[] { 7, 7 });
        Assert.Equal(2, LuminPackSerializer.Deserialize<ISet<int>>(bytes).Count);
        Assert.Equal(2, LuminPackSerializer.Deserialize<IReadOnlySet<int>>(bytes).Count);

        ISet<string?> strings = new HashSet<string?> { null, "ascii", "\u00e9", "\u4e2d" };
        bytes = LuminPackSerializer.Serialize(strings);
        Assert.True(strings.SetEquals(LuminPackSerializer.Deserialize<ISet<string?>>(bytes)));
        Assert.True(strings.SetEquals(LuminPackSerializer.Deserialize<IReadOnlySet<string?>>(bytes)));
    }

    /// <summary>Checks portable elements retain their field-wise formatter and fixed-width boundary validation.</summary>
    [Fact]
    public void PortableElementsAndTruncatedPayloadsRetainTheirBoundaries()
    {
        ISet<SetPortableValue> expected = new HashSet<SetPortableValue>
        {
            new() { Tag = 1, Number = long.MinValue },
            new() { Tag = 2, Number = long.MaxValue }
        };
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(22, bytes.Length);
        Assert.True(expected.SetEquals(LuminPackSerializer.Deserialize<ISet<SetPortableValue>>(bytes)));
        Assert.True(expected.SetEquals(LuminPackSerializer.Deserialize<IReadOnlySet<SetPortableValue>>(bytes)));

        ISet<int> result = new HashSet<int> { 99 };
        var previous = result;
        byte[] truncated = [2, 0, 0, 0, 1, 0, 0, 0];
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(truncated.AsSpan(), ref result));
        Assert.Same(previous, result);
        Assert.Equal(new[] { 99 }, result);
    }

    /// <summary>Checks unmanaged versioned elements retain their explicit field evaluator.</summary>
    [Fact]
    public void VersionedElementSizeDoesNotUseNativeStorage()
    {
        var expected = new HashSet<SetVersionedValue> { new() { Value = 17 }, new() { Value = 31 } };
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.True(expected.SetEquals(LuminPackSerializer.Deserialize<ISet<SetVersionedValue>>(bytes)));
        Assert.True(expected.SetEquals(LuminPackSerializer.Deserialize<IReadOnlySet<SetVersionedValue>>(bytes)));
    }

    /// <summary>Checks exact allocation uses live Count rather than the occupied entry range.</summary>
    [Fact]
    public void SparseHashSetRetainsItsExactRecordWidth()
    {
        var expected = Enumerable.Range(0, 256).ToHashSet();
        expected.RemoveWhere(value => (value & 1) == 0);
        byte[] bytes = LuminPackSerializer.Serialize(expected);
        Assert.Equal(4 + expected.Count * 4, bytes.Length);
        Assert.True(expected.SetEquals(LuminPackSerializer.Deserialize<HashSet<int>>(bytes)));
        Assert.True(expected.SetEquals(LuminPackSerializer.Deserialize<ISet<int>>(bytes)));
    }
}

/// <summary>Observes equality calls while forcing the same hash for every element.</summary>
[LuminPackable]
public partial struct SetCollisionValue : IEquatable<SetCollisionValue>
{
    public int Value;
    public static int Comparisons;

    /// <summary>Counts each equality comparison.</summary>
    /// <param name="other">The comparison value.</param>
    /// <returns>Whether the element values match.</returns>
    public bool Equals(SetCollisionValue other) { Comparisons++; return Value == other.Value; }

    /// <summary>Compares a boxed element.</summary>
    /// <param name="other">The comparison value.</param>
    /// <returns>Whether the boxed element matches.</returns>
    public override bool Equals(object? other) => other is SetCollisionValue value && Equals(value);

    /// <summary>Forces all elements into one bucket.</summary>
    /// <returns>A constant hash code.</returns>
    public override int GetHashCode() => 0;
}

/// <summary>Exercises field-wise portable storage inside a set interface.</summary>
[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct SetPortableValue
{
    public byte Tag;
    public long Number;
}

/// <summary>Retains version-tolerant framing despite unmanaged CLR storage.</summary>
[LuminPackable(GeneratorType.VersionTolerant)]
public partial struct SetVersionedValue
{
    [LuminPackOrder(2)]
    public int Value;
}
