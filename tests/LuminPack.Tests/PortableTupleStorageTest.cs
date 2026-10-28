using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Checks physical tuple fields, alias-independent bytes and recursive Rest storage in portable records.</summary>
public class PortableTupleStorageTest
{
    /// <summary>Checks component names do not duplicate storage or change the portable record's wire.</summary>
    [Fact]
    public void NamedComponentsAreWrittenOnce()
    {
        var value = new PortableNamedTupleRecord { Tag = 3, Pair = (123456789, long.MinValue + 17), Tail = -1234 };
        var renamed = new PortableRenamedTupleRecord { Tag = value.Tag, Pair = value.Pair, Tail = value.Tail };
        var unnamed = new PortableUnnamedTupleRecord { Tag = value.Tag, Pair = value.Pair, Tail = value.Tail };
        byte[] expected = Expected(value);
        Assert.Equal(15, expected.Length);
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(expected, LuminPackSerializer.Serialize(renamed));
        Assert.Equal(expected, LuminPackSerializer.Serialize(unnamed));
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(value));
        Assert.Equal(value, LuminPackSerializer.Deserialize<PortableNamedTupleRecord>(expected));
        Assert.Equal(renamed, LuminPackSerializer.Deserialize<PortableRenamedTupleRecord>(expected));
        Assert.Equal(unnamed, LuminPackSerializer.Deserialize<PortableUnnamedTupleRecord>(expected));
    }

    /// <summary>Checks flattened long-tuple symbols are represented only through their actual Rest field.</summary>
    [Fact]
    public void LongTupleRestStorageIsExpandedOnce()
    {
        var value = new PortableLongTupleRecord
        {
            Tag = 17,
            Items = (1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15),
            Tail = 23
        };
        byte[] expected = new byte[] { value.Tag }
            .Concat(Enumerable.Range(1, 15).SelectMany(BitConverter.GetBytes))
            .Concat(new byte[] { value.Tail }).ToArray();
        Assert.Equal(62, expected.Length);
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(value));
        Assert.Equal(value, LuminPackSerializer.Deserialize<PortableLongTupleRecord>(expected));
        var values = new[] { value, value, value };
        var payload = BitConverter.GetBytes(values.Length).Concat(values.SelectMany(_ => expected)).ToArray();
        Assert.Equal(payload, LuminPackSerializer.Serialize(values));
        Assert.Equal(values, LuminPackSerializer.Deserialize<PortableLongTupleRecord[]>(payload));
    }

    /// <summary>Checks tuple byte offsets used by contiguous packing and the scalar tail share the same wire.</summary>
    [Fact]
    public void TupleCollectionsKeepThePackedWireAndReuse()
    {
        foreach (int length in new[] { 0, 1, 2, 3, 7, 8, 17, 65 })
        {
            var values = Enumerable.Range(0, length).Select(index => new PortableNamedTupleRecord
            {
                Tag = (byte)index, Pair = (index * 7919 - 42, long.MinValue + index), Tail = (short)(17 - index)
            }).ToArray();
            byte[] expected = BitConverter.GetBytes(length).Concat(values.SelectMany(Expected)).ToArray();
            Assert.Equal(expected, LuminPackSerializer.Serialize(values));
            Assert.Equal(expected, LuminPackSerializer.Serialize(values.ToList()));
            Assert.Equal(expected, LuminPackSerializer.Serialize(new Memory<PortableNamedTupleRecord>(values)));
            Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(values));

            var reused = new PortableNamedTupleRecord[length];
            var original = reused;
            LuminPackSerializer.Deserialize(expected, ref reused);
            Assert.Same(original, reused);
            Assert.Equal(values, reused);
            var list = values.Select(_ => default(PortableNamedTupleRecord)).ToList();
            var originalList = list;
            LuminPackSerializer.Deserialize(expected, ref list);
            Assert.Same(originalList, list);
            Assert.Equal(values, list);
        }
    }

    /// <summary>Checks nested tuples reached through a closed unmanaged parameter retain physical storage analysis.</summary>
    [Fact]
    public void ClosedGenericAndNestedTuplesUseComponentOrder()
    {
        var value = new PortableTupleEnvelope<(int Id, (long Value, short Code) Inner)>
        {
            Tag = 7, Value = (123456789, (long.MinValue + 23, -1234))
        };
        byte[] expected = new byte[] { value.Tag }.Concat(BitConverter.GetBytes(value.Value.Id))
            .Concat(BitConverter.GetBytes(value.Value.Inner.Value)).Concat(BitConverter.GetBytes(value.Value.Inner.Code)).ToArray();
        Assert.Equal(15, expected.Length);
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(value));
        Assert.Equal(value, LuminPackSerializer.Deserialize<PortableTupleEnvelope<(int Id, (long Value, short Code) Inner)>>(expected));
    }

    /// <summary>Checks tuple storage is expanded even when sequential size estimates happen to equal the wire size.</summary>
    [Fact]
    public void CompactTupleMembersKeepPositionOrder()
    {
        var value = new PortableCompactTupleRecord { Pair = (long.MinValue + 1, 123456789, -23) };
        byte[] expected = BitConverter.GetBytes(value.Pair.Value).Concat(BitConverter.GetBytes(value.Pair.Id))
            .Concat(BitConverter.GetBytes(value.Pair.Code)).ToArray();
        Assert.Equal(16, expected.Length);
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(value, LuminPackSerializer.Deserialize<PortableCompactTupleRecord>(expected));
    }

    /// <summary>Checks an unmarked outer record whose tuple member varies between runtimes is written without padding.</summary>
    [Fact]
    public void UnmarkedVariantRecordsUsePackedSingles()
    {
        var value = new RawNamedTupleRecord { Tag = 11, Pair = (17, 123456789012345) };
        byte[] expected = new byte[] { 11 }.Concat(BitConverter.GetBytes(17)).Concat(BitConverter.GetBytes(123456789012345L)).ToArray();
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(value, ConcreteReads.Read<RawNamedTupleRecord>(expected, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
    }

    /// <summary>Checks unmarked tuples whose automatic layout varies are written member by member as roots and tagged native storage in containers.</summary>
    [Fact]
    public void UnmarkedVariantTuplesUsePackedRootsAndTaggedCollections()
    {
        var value = (Tag: (byte)17, Value: long.MinValue + 123, Code: (short)42);
        byte[] packed = new byte[] { 17 }.Concat(BitConverter.GetBytes(value.Value)).Concat(BitConverter.GetBytes(value.Code)).ToArray();
        Assert.Equal(packed, LuminPackSerializer.Serialize(value));
        Assert.Equal(value, ConcreteReads.Read<(byte Tag, long Value, short Code)>(packed, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        var values = new[] { value, value, value };
        byte[] expected = LuminPackSerializer.Serialize(values);
        // The count, one layout tag, then the native elements exactly as this process stores them.
        Assert.Equal(values.Length, BitConverter.ToInt32(expected, 0));
        Assert.True(expected[4] != 0);
        Assert.Equal(MemoryMarshal.AsBytes(values.AsSpan()).ToArray(), expected.AsSpan(5).ToArray());
        Assert.Equal(expected, LuminPackSerializer.Serialize(values.ToList()));
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(values));
        Assert.Equal(values, ConcreteReads.Read<(byte Tag, long Value, short Code)[]>(expected, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        Assert.Equal(values, ConcreteReads.Read<List<(byte Tag, long Value, short Code)>>(expected, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
    }

    /// <summary>Builds the record from component values without relying on native tuple layout.</summary>
    /// <param name="value">The source record.</param>
    /// <returns>The declaration-ordered portable bytes.</returns>
    private static byte[] Expected(PortableNamedTupleRecord value) => new byte[] { value.Tag }
        .Concat(BitConverter.GetBytes(value.Pair.Id)).Concat(BitConverter.GetBytes(value.Pair.Count))
        .Concat(BitConverter.GetBytes(value.Tail)).ToArray();
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableNamedTupleRecord
{
    public byte Tag;
    public (int Id, long Count) Pair;
    public short Tail;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableRenamedTupleRecord
{
    public byte Tag;
    public (int RenamedId, long RenamedCount) Pair;
    public short Tail;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableUnnamedTupleRecord
{
    public byte Tag;
    public ValueTuple<int, long> Pair;
    public short Tail;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableLongTupleRecord
{
    public byte Tag;
    public (int A, int B, int C, int D, int E, int F, int G, int H, int I, int J, int K, int L, int M, int N, int O) Items;
    public byte Tail;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableTupleEnvelope<T> where T : unmanaged
{
    public byte Tag;
    public T Value;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableCompactTupleRecord
{
    public (long Value, int Id, int Code) Pair;
}

[LuminPackable]
public partial struct RawNamedTupleRecord
{
    public byte Tag;
    public (int Id, long Count) Pair;
}
