#if LUMINPACK_NEXT_GENERATOR
using System.Globalization;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Covers framework indexing values through the generated serializer.</summary>
public sealed class NinoUnsupportedTypeRegressionTest
{
    /// <summary>Checks the fixed-width binary payload and signed JSON encoding across both index directions.</summary>
    /// <param name="number">The index's nonnegative value.</param>
    /// <param name="fromEnd">Whether the index starts from the end.</param>
    /// <param name="encoded">The signed representation.</param>
    [Theory]
    [InlineData(0, false, 0)]
    [InlineData(0, true, -1)]
    [InlineData(3, true, -4)]
    [InlineData(int.MaxValue, false, int.MaxValue)]
    [InlineData(int.MaxValue, true, int.MinValue)]
    public void IndexUsesOneSignedInteger(int number, bool fromEnd, int encoded)
    {
        var source = new Index(number, fromEnd);
        Index result = default;
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(LuminPackSerializer.Serialize(encoded), bytes);
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref result));
        Assert.Equal(source, result);

        string json = LuminPackSerializer.SerializeJson(source);
        Assert.Equal(encoded.ToString(CultureInfo.InvariantCulture), json);
        LuminPackSerializer.DeserializeJson(json, ref result);
        Assert.Equal(source, result);
    }

    /// <summary>Checks an Index and Range as members, along with a standalone Range's two endpoints.</summary>
    [Fact]
    public void IndexAndRangeRoundTripAsMembers()
    {
        var source = new NinoIndexAndRange { Index = ^3, Range = 1..^2 };
        NinoIndexAndRange? result = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref result);
        Assert.NotNull(result);
        Assert.Equal(source.Index, result.Index);
        Assert.Equal(source.Range, result.Range);

        result = null;
        LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(source), ref result);
        Assert.NotNull(result);
        Assert.Equal(source.Index, result.Index);
        Assert.Equal(source.Range, result.Range);
        Assert.Equal("[1,-3]", LuminPackSerializer.SerializeJson(source.Range));
    }

    /// <summary>Checks that an open generic reader observes the generated indexing registrations.</summary>
    [Fact]
    public void IndexAndRangeAreAvailableThroughRegisteredFallback()
    {
        Index index = ^3;
        Range range = 1..^2;
        Func<byte[], Index> readIndex = ReadRegistered<Index>;
        Func<byte[], Range> readRange = ReadRegistered<Range>;
        Assert.Equal(index, readIndex(LuminPackSerializer.Serialize(index)));
        Assert.Equal(range, readRange(LuminPackSerializer.Serialize(range)));
    }

    /// <summary>Uses an open generic payload whose state operations bind through registration.</summary>
    /// <typeparam name="T">The payload selected after compilation.</typeparam>
    /// <param name="bytes">The encoded value.</param>
    /// <returns>The parsed value.</returns>
    private static T ReadRegistered<T>(byte[] bytes) => LuminPackSerializer.Deserialize<T>(bytes);

    /// <summary>Checks that the opt-in appends the ordinary List bytes to the existing object record.</summary>
    [Fact]
    public void IncludedCollectionBaseKeepsDeclaredMemberFraming()
    {
        var original = new NinoOrdinaryList { Name = "numbers", Version = 4 };
        original.AddRange([1, 2, 3]);
        var source = new NinoListWithMembers { Name = original.Name, Version = original.Version };
        source.AddRange(original);

        byte[] memberBytes = LuminPackSerializer.Serialize(original);
        byte[] listBytes = LuminPackSerializer.Serialize(new List<int>(original));
        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(memberBytes.Concat(listBytes), bytes);

        NinoOrdinaryList? plain = null;
        LuminPackSerializer.Deserialize(memberBytes, ref plain);
        Assert.NotNull(plain);
        Assert.Empty(plain);
        Assert.Equal("numbers", plain.Name);
        Assert.Equal(4, plain.Version);

        NinoListWithMembers? result = null;
        Assert.Equal(bytes.Length, LuminPackSerializer.Deserialize(bytes, ref result));
        Assert.NotNull(result);
        Assert.Equal(new[] { 1, 2, 3 }, result);
        Assert.Equal("numbers", result.Name);
        Assert.Equal(4, result.Version);
    }

    /// <summary>Checks the identity of an opted-in list nested in another payload.</summary>
    /// <param name="json">Whether the operation uses JSON.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IncludedCollectionBaseRefillsTheExistingNestedObject(bool json)
    {
        var source = new NinoListHolder { Value = new NinoListWithMembers { 1, 2, 3 } };
        source.Value.Name = "numbers";
        source.Value.Version = 4;
        var existing = new NinoListWithMembers { 99 };
        NinoListHolder? target = new NinoListHolder { Value = existing };

        if (json)
            LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(source), ref target);
        else
            LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref target);

        Assert.NotNull(target);
        Assert.Same(existing, target.Value);
        Assert.Equal(new[] { 1, 2, 3 }, existing);
        Assert.Equal("numbers", existing.Name);
        Assert.Equal(4, existing.Version);
    }

    /// <summary>Checks that collection elements use the full record with its appended list.</summary>
    [Fact]
    public void IncludedCollectionBaseRoundTripsInAnArray()
    {
        NinoListWithMembers[] source = [new NinoListWithMembers { 7, 8 }];
        source[0].Name = "items";
        source[0].Version = 9;
        NinoListWithMembers[]? result = null;
        LuminPackSerializer.Deserialize(LuminPackSerializer.Serialize(source), ref result);

        NinoListWithMembers item = Assert.Single(result!);
        Assert.Equal(new[] { 7, 8 }, item);
        Assert.Equal("items", item.Name);
        Assert.Equal(9, item.Version);
    }

    /// <summary>Checks the literal wire format and the inherited collection's frozen limit.</summary>
    [Fact]
    public void IncludedCollectionBaseObeysTheRootLimit()
    {
        byte[] fixture = [1, 4, 0, 0, 0, 3, 0, 0, 0, 7, 0, 0, 0, 8, 0, 0, 0, 9, 0, 0, 0];
        var source = new NinoLimitedCollectionBase { 7, 8, 9 };
        source.Version = 4;
        Assert.Equal(fixture, LuminPackSerializer.Serialize(source));

        NinoLimitedCollectionBase? target = new() { 1 };
        Assert.Equal(fixture.Length, LuminPackSerializer.Deserialize(fixture, ref target));
        Assert.Equal(source, target);
        Assert.Equal(4, target.Version);

        source.Add(10);
        byte[] tooLong = LuminPackSerializer.Serialize(source);
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize(tooLong, ref target));
        Assert.Equal(new[] { 7, 8, 9 }, target);
    }

    /// <summary>Checks closed collection-only source subclasses using their framework base format and reuse.</summary>
    /// <param name="json">Whether the operation uses JSON.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CollectionOnlySubclassesKeepTheirRuntimeType(bool json)
    {
        var list = new NinoCollectionOnlyList<int> { 1, 2, 3 };
        var dictionary = new NinoCollectionOnlyMap<long, long> { [2] = [7, 8], [1] = [9] };
        var oldList = new NinoCollectionOnlyList<int> { 99 };
        var oldMap = new NinoCollectionOnlyMap<long, long> { [9] = [99] };
        NinoCollectionOnlyList<int>? readList = oldList;
        NinoCollectionOnlyMap<long, long>? readMap = oldMap;

        if (json)
        {
            LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(list), ref readList);
            LuminPackSerializer.DeserializeJson(LuminPackSerializer.SerializeJson(dictionary), ref readMap);
        }
        else
        {
            byte[] listBytes = LuminPackSerializer.Serialize(list);
            byte[] mapBytes = LuminPackSerializer.Serialize(dictionary);
            Assert.Equal(LuminPackSerializer.Serialize((List<int>)list), listBytes);
            Assert.Equal(LuminPackSerializer.Serialize((SortedDictionary<long, List<long>>)dictionary), mapBytes);
            LuminPackSerializer.Deserialize(listBytes, ref readList);
            LuminPackSerializer.Deserialize(mapBytes, ref readMap);
        }

        Assert.Same(oldList, readList);
        Assert.Same(oldMap, readMap);
        Assert.Equal(new[] { 1, 2, 3 }, oldList);
        Assert.Equal(new[] { 1L, 2L }, oldMap.Keys);
        Assert.Equal(new[] { 7L, 8L }, oldMap[2]);
        Assert.Equal(new[] { 9L }, oldMap[1]);
    }

    /// <summary>Checks that an explicit formatter installed after first use overrides collection forwarding.</summary>
    [Fact]
    public void ExplicitCollectionSubclassRegistrationTakesPrecedence()
    {
        var source = new NinoRegisteredCollection { 7 };
        Assert.Equal(LuminPackSerializer.Serialize((List<int>)source), LuminPackSerializer.Serialize(source));
        LuminPackSerializer.Register<NinoRegisteredCollection>(
            static (ref LuminPackWriter writer, in NinoRegisteredCollection value) => writer.Write(value[0] + 100),
            static (ref LuminPackReader reader, ref NinoRegisteredCollection value) => value = new() { reader.Read<int>() - 100 });

        byte[] bytes = LuminPackSerializer.Serialize(source);
        Assert.Equal(LuminPackSerializer.Serialize(107), bytes);
        NinoRegisteredCollection? target = null;
        LuminPackSerializer.Deserialize(bytes, ref target);
        Assert.Equal(new[] { 7 }, target);
    }

    /// <summary>Checks that the ordinary and opted-in subclass paths validate inherited Boolean elements.</summary>
    [Fact]
    public void CollectionSubclassesCheckInheritedBooleanStorage()
    {
        byte[] ordinary = LuminPackSerializer.Serialize(new NinoCollectionOnlyList<bool> { false, true });
        ordinary[sizeof(int) + 1] = 2;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<NinoCollectionOnlyList<bool>>(ordinary));

        var source = new NinoLimitedBooleanCollectionBase { false, true };
        byte[] optedIn = LuminPackSerializer.Serialize(source);
        optedIn[1 + 2 * sizeof(int) + 1] = 2;
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<NinoLimitedBooleanCollectionBase>(optedIn));

        source.Add(true);
        byte[] tooLong = LuminPackSerializer.Serialize(source);
        Assert.Throws<InvalidDataException>(() => LuminPackSerializer.Deserialize<NinoLimitedBooleanCollectionBase>(tooLong));
    }
}

/// <summary>A payload containing both framework indexing values.</summary>
[LuminPackable]
public sealed partial class NinoIndexAndRange
{
    public Index Index;
    public Range Range;
}

/// <summary>A list subclass using the original declared-member record.</summary>
[LuminPackable]
public sealed partial class NinoOrdinaryList : List<int>
{
    public string? Name;
    public int Version;
}

/// <summary>A list subclass that serializes both its record members and its base elements.</summary>
[LuminPackable, LuminPackOption(IncludeCollectionBase = true)]
public sealed partial class NinoListWithMembers : List<int>
{
    public string? Name;
    public int Version;
}

/// <summary>A parent with an opted-in list member.</summary>
[LuminPackable]
public sealed partial class NinoListHolder
{
    public NinoListWithMembers? Value;
}

/// <summary>A list-derived record with a fixed declared member and a bounded base collection.</summary>
[LuminPackable, LuminPackOption(IncludeCollectionBase = true, MaxCollectionLength = 3)]
public sealed partial class NinoLimitedCollectionBase : List<int>
{
    public int Version;
}

/// <summary>A list-derived record that enforces both its inherited count and Boolean domain.</summary>
[LuminPackable, LuminPackOption(IncludeCollectionBase = true, MaxCollectionLength = 2)]
public sealed partial class NinoLimitedBooleanCollectionBase : List<bool>
{
    public int Version;
}

/// <summary>A collection-only list subclass whose behavioral interface adds no storage.</summary>
public sealed class NinoCollectionOnlyList<T> : List<T>, IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => Clear();
}

/// <summary>A collection-only sorted map with list values.</summary>
public sealed class NinoCollectionOnlyMap<TKey, TValue> : SortedDictionary<TKey, List<TValue>> where TKey : notnull;

/// <summary>A collection-only subclass reserved for testing explicit registration precedence.</summary>
public sealed class NinoRegisteredCollection : List<int>;
#endif
