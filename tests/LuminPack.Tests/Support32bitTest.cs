using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Generated;
using Xunit;

namespace LuminPackUnitTest;

/// <summary>Verifies architecture-independent opt-in unmanaged records and unchanged raw storage.</summary>
public class Support32bitTest
{
    /// <summary>Verifies declaration order and eight-byte integers without internal or trailing padding.</summary>
    [Fact]
    public void PaddedStorageUsesDeclarationOrder()
    {
        var value = Create(7);
        byte[] expected = Expected(value);
        Assert.Equal(11, expected.Length);
        Assert.True(Unsafe.SizeOf<PortablePadded>() > expected.Length);
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(value));
        Assert.Equal(value, LuminPackSerializer.Deserialize<PortablePadded>(expected));
    }

    /// <summary>Verifies that an unmarked nested struct is expanded inside a portable record.</summary>
    [Fact]
    public void NestedStorageIsRecursivelyExpanded()
    {
        var value = new PortableNested { Prefix = 3, Inner = new RawPadded { Tag = 5, Value = 0x102030405060708 }, Tail = 9 };
        byte[] expected = new byte[] { 3, 5 }.Concat(BitConverter.GetBytes(value.Inner.Value)).Concat(new byte[] { 9 }).ToArray();
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(value, LuminPackSerializer.Deserialize<PortableNested>(expected));
    }

    /// <summary>Verifies packed ABI declarations can retain raw copies when no padding exists.</summary>
    [Fact]
    public void PaddingFreeStorageKeepsRawBytes()
    {
        var compact = new PortableCompact { Tag = 17, Value = long.MinValue + 3 };
        Assert.Equal(RawBytes(compact), LuminPackSerializer.Serialize(compact));
    }

    /// <summary>Verifies an unmarked struct whose layout differs between 32-bit and 64-bit processes is written without padding.</summary>
    [Fact]
    public void UnmarkedVariantStorageIsPackedAndTaggedInCollections()
    {
        var raw = new RawPadded { Tag = 29, Value = 0x1122334455667788 };
        byte[] packed = new[] { raw.Tag }.Concat(BitConverter.GetBytes(raw.Value)).ToArray();
        Assert.Equal(packed, LuminPackSerializer.Serialize(raw));
        Assert.Equal(raw, ConcreteReads.Read<RawPadded>(packed, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        var parent = new RawParent { Value = Create(4), Tail = 8 };
        byte[] parentBytes = Expected(parent.Value).Concat(BitConverter.GetBytes(parent.Tail)).ToArray();
        Assert.Equal(parentBytes, LuminPackSerializer.Serialize(parent));
        Assert.Equal(parent, ConcreteReads.Read<RawParent>(parentBytes, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));

        // A contiguous payload names the native model of its elements with one tag byte: 1 is 64-bit natural, 2 is 32-bit natural.
        var values = new[] { raw, new RawPadded { Tag = 3, Value = -5 }, new RawPadded { Tag = 255, Value = long.MinValue } };
        byte[] written = LuminPackSerializer.Serialize(values);
        Assert.Equal(values.Length, BitConverter.ToInt32(written, 0));
        Assert.Contains(written[4], new byte[] { 1, 2 });
        Assert.Equal(4 + 1 + values.Length * (written[4] == 1 ? 16 : 12), written.Length);
        Assert.Equal(written.Length, LuminPackSerializer.Sizeof(values));
        Assert.Equal(written, LuminPackSerializer.Serialize(values.ToList()));
        Assert.Empty(ConcreteReads.Read<RawPadded[]>(LuminPackSerializer.Serialize(Array.Empty<RawPadded>()), static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        Assert.Equal(4, LuminPackSerializer.Serialize(Array.Empty<RawPadded>()).Length);

        // Payloads written by either kind of process decode in this one.
        foreach (byte tag in new byte[] { 1, 2 })
        {
            int width = tag == 1 ? 16 : 12, wide = tag == 1 ? 8 : 4;
            byte[] foreign = BitConverter.GetBytes(values.Length).Append(tag).Concat(new byte[values.Length * width]).ToArray();
            for (int index = 0; index < values.Length; index++)
            {
                foreign[5 + index * width] = values[index].Tag;
                BitConverter.GetBytes(values[index].Value).CopyTo(foreign, 5 + index * width + wide);
            }
            Assert.Equal(values, ConcreteReads.Read<RawPadded[]>(foreign, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
            Assert.Equal(values, ConcreteReads.Read<List<RawPadded>>(foreign, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
        }
        byte[] unknown = BitConverter.GetBytes(1).Append((byte)200).Concat(new byte[16]).ToArray();
        Assert.Throws<InvalidDataException>(() => ConcreteReads.Read<RawPadded[]>(unknown, static (b, ref v) => LuminPackSerializer.Deserialize(b, ref v)));
    }

    /// <summary>Verifies private fields, readonly fields, and auto-property backing slots survive packing.</summary>
    [Fact]
    public void PrivateStorageAndExplicitSizeArePreserved()
    {
        var value = new PortablePrivate(19, 0x123456789abcdef);
        var expected = new byte[] { 19 }.Concat(BitConverter.GetBytes(value.Value)).ToArray();
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(value.Value, LuminPackSerializer.Deserialize<PortablePrivate>(expected).Value);
        var sized = new PortableSized { Value = 41 };
        Assert.Equal(BitConverter.GetBytes(41), LuminPackSerializer.Serialize(sized));
        Assert.Equal(41, LuminPackSerializer.Deserialize<PortableSized>(BitConverter.GetBytes(41)).Value);
        var explicitValue = new PortableExplicit { First = 5, Second = 123456789012345 };
        var explicitBytes = new byte[] { 5 }.Concat(BitConverter.GetBytes(explicitValue.Second)).ToArray();
        Assert.Equal(explicitBytes, LuminPackSerializer.Serialize(explicitValue));
        Assert.Equal(explicitValue, LuminPackSerializer.Deserialize<PortableExplicit>(explicitBytes));
    }

    /// <summary>Verifies large arrays and lists share one packed payload and reuse existing storage.</summary>
    [Fact]
    public void ArraysAndListsUsePackedPayloadsAndReuseStorage()
    {
        var values = Enumerable.Range(0, 20000).Select(Create).ToArray();
        byte[] expected = BitConverter.GetBytes(values.Length).Concat(values.SelectMany(Expected)).ToArray();
        Assert.Equal(expected, LuminPackSerializer.Serialize(values));
        Assert.Equal(expected, LuminPackSerializer.Serialize(values.ToList()));
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(values));
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(values.ToList()));
        var copy = LuminPackSerializer.Deserialize<PortablePadded[]>(expected);
        Assert.Equal(values, copy);
        var original = copy;
        LuminPackSerializer.Deserialize(expected.AsSpan(), ref copy);
        Assert.Same(original, copy);
        var list = LuminPackSerializer.Deserialize<List<PortablePadded>>(expected);
        Assert.Equal(values, list);
        var originalList = list;
        LuminPackSerializer.Deserialize(expected.AsSpan(), ref list);
        Assert.Same(originalList, list);
        Assert.Equal(values, list);
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<PortablePadded[]>(expected.AsSpan(0, expected.Length - 1)));
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize<List<PortablePadded>>(expected.AsSpan(0, expected.Length - 1)));
    }

    /// <summary>Verifies packed root/member contracts and collection boundaries compose with following fields.</summary>
    [Fact]
    public void NestedCollectionsAndFollowingFieldsRoundTrip()
    {
        var value = new PortableContainer { First = 13, Value = Create(3), Array = new[] { Create(5) }, List = new() { Create(7) }, Last = 29 };
        var payload = LuminPackSerializer.Serialize(value);
        var copy = LuminPackSerializer.Deserialize<PortableContainer>(payload);
        Assert.Equal(value.First, copy.First);
        Assert.Equal(value.Value, copy.Value);
        Assert.Equal(value.Array, copy.Array);
        Assert.Equal(value.List, copy.List);
        Assert.Equal(value.Last, copy.Last);
        Assert.Equal(payload, LuminPackSerializer.Serialize(copy));
        var nested = new[] { new[] { Create(11) }, Array.Empty<PortablePadded>() };
        var nestedCopy = LuminPackSerializer.Deserialize<PortablePadded[][]>(LuminPackSerializer.Serialize(nested));
        Assert.Equal(nested[0], nestedCopy[0]);
        Assert.Empty(nestedCopy[1]);
        var dictionary = new Dictionary<int, PortablePadded> { [2] = Create(9) };
        Assert.Equal(dictionary[2], LuminPackSerializer.Deserialize<Dictionary<int, PortablePadded>>(LuminPackSerializer.Serialize(dictionary))[2]);
    }

    /// <summary>Verifies null and empty collection framing remains unchanged.</summary>
    [Fact]
    public void NullAndEmptyCollectionsKeepTheirHeaders()
    {
        PortablePadded[]? array = null;
        List<PortablePadded>? list = null;
        Assert.Null(LuminPackSerializer.Deserialize<PortablePadded[]>(LuminPackSerializer.Serialize(array)));
        Assert.Null(LuminPackSerializer.Deserialize<List<PortablePadded>>(LuminPackSerializer.Serialize(list)));
        Assert.Equal(BitConverter.GetBytes(0), LuminPackSerializer.Serialize(Array.Empty<PortablePadded>()));
        Assert.Equal(BitConverter.GetBytes(0), LuminPackSerializer.Serialize(new List<PortablePadded>()));
    }

    /// <summary>Verifies closed generic storage uses the actual scalar widths without raw nested copies.</summary>
    [Fact]
    public void GenericStorageUsesItsClosedFieldLayout()
    {
        var value = new PortableGeneric<long> { Tag = 11, Value = long.MaxValue - 17 };
        byte[] expected = new byte[] { 11 }.Concat(BitConverter.GetBytes(value.Value)).ToArray();
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(value, LuminPackSerializer.Deserialize<PortableGeneric<long>>(expected));
        var array = new[] { value };
        var payload = LuminPackSerializer.Serialize(array);
        Assert.Equal(BitConverter.GetBytes(1).Concat(expected).ToArray(), payload);
        Assert.Equal(array, LuminPackSerializer.Deserialize<PortableGeneric<long>[]>(payload));
        Span<byte> span = new byte[64];
        var writer = new LuminPackWriter(ref span);
        WriteGeneric(ref writer, in value);
        Assert.Equal(expected, writer.GetSpan().ToArray());
        ReadOnlySpan<byte> input = expected;
        var reader = new LuminPackReader(ref input);
        PortableGeneric<long> copy = default;
        ReadGeneric(ref reader, ref copy);
        Assert.Equal(value, copy);
        Assert.Equal(expected.Length, LuminPackSerializer.Sizeof(value));
    }

    /// <summary>Exercises the open state overload through its closed formatter dispatch.</summary>
    /// <typeparam name="T">The unmanaged field type.</typeparam>
    /// <param name="writer">The caller-owned output state.</param>
    /// <param name="value">The portable generic value.</param>
    private static void WriteGeneric<T>(ref LuminPackWriter writer, in PortableGeneric<T> value) where T : unmanaged
        => LuminPackSerializer.Serialize(ref writer, in value);

    /// <summary>Exercises an open reader entry without changing the closed portable representation.</summary>
    /// <typeparam name="T">The unmanaged field type.</typeparam>
    /// <param name="reader">The caller-owned input state.</param>
    /// <param name="value">The destination storage.</param>
    private static void ReadGeneric<T>(ref LuminPackReader reader, ref PortableGeneric<T> value) where T : unmanaged
        => LuminPackSerializer.Deserialize(ref reader, ref value);

    /// <summary>Verifies repeated portable array/List reads allocate no objects after warmup.</summary>
    [Fact]
    public void ReusedCollectionsAllocateNothing()
    {
        var array = Enumerable.Range(0, 16).Select(Create).ToArray();
        var list = array.ToList();
        byte[] payload = LuminPackSerializer.Serialize(array);
        for (int index = 0; index < 128; index++)
        {
            LuminPackSerializer.Deserialize(payload.AsSpan(), ref array);
            LuminPackSerializer.Deserialize(payload.AsSpan(), ref list);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int index = 0; index < 128; index++)
        {
            LuminPackSerializer.Deserialize(payload.AsSpan(), ref array);
            LuminPackSerializer.Deserialize(payload.AsSpan(), ref list);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    /// <summary>Verifies contiguous memory slices and rectangular arrays share the portable element contract.</summary>
    [Fact]
    public void OtherContiguousCollectionsUsePackedFields()
    {
        var values = new[] { Create(1), Create(2), Create(3) };
        byte[] expected = LuminPackSerializer.Serialize(values[1..]);
        var memory = new Memory<PortablePadded>(values, 1, 2);
        var readOnly = new ReadOnlyMemory<PortablePadded>(values, 1, 2);
        var segment = new ArraySegment<PortablePadded>(values, 1, 2);
        Assert.Equal(expected, LuminPackSerializer.Serialize(memory));
        Assert.Equal(expected, LuminPackSerializer.Serialize(readOnly));
        Assert.Equal(expected, LuminPackSerializer.Serialize(segment));
        Assert.Equal(values[1..], LuminPackSerializer.Deserialize<Memory<PortablePadded>>(expected).ToArray());
        Assert.Equal(values[1..], LuminPackSerializer.Deserialize<ReadOnlyMemory<PortablePadded>>(expected).ToArray());
        Assert.Equal(values[1..], LuminPackSerializer.Deserialize<ArraySegment<PortablePadded>>(expected).ToArray());
        var matrix = new PortablePadded[,] { { Create(4), Create(5) } };
        var payload = LuminPackSerializer.Serialize(matrix);
        Assert.Equal(matrix[0, 1], LuminPackSerializer.Deserialize<PortablePadded[,]>(payload)[0, 1]);
    }

    /// <summary>Creates a reproducible record with a nontrivial 64-bit field.</summary>
    /// <param name="seed">The record discriminator.</param>
    /// <returns>The test record.</returns>
    private static PortablePadded Create(int seed) => new() { Tag = (byte)seed, Value = 0x102030405060708 + seed, Tail = (short)(seed * 3) };

    /// <summary>Builds expected packed bytes independently of the generator.</summary>
    /// <param name="value">The test record.</param>
    /// <returns>Declaration-ordered scalar bytes.</returns>
    private static byte[] Expected(PortablePadded value) => new[] { value.Tag }.Concat(BitConverter.GetBytes(value.Value)).Concat(BitConverter.GetBytes(value.Tail)).ToArray();

    /// <summary>Captures the complete native storage representation, including padding.</summary>
    /// <typeparam name="T">The unmanaged storage type.</typeparam>
    /// <param name="value">The initialized value.</param>
    /// <returns>Its original raw wire bytes.</returns>
    private static byte[] RawBytes<T>(T value) where T : unmanaged => MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref value, 1)).ToArray();
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortablePadded
{
    [LuminPackOrder(9)] public byte Tag;
    [LuminPackOrder(1)] public long Value;
    public short Tail;
}

[LuminPackable]
public partial struct RawPadded
{
    public byte Tag;
    public long Value;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableNested
{
    public byte Prefix;
    public RawPadded Inner;
    public byte Tail;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed), StructLayout(LayoutKind.Sequential, Pack = 1)]
public partial struct PortableCompact
{
    public byte Tag;
    public long Value;
}

[LuminPackable]
public partial struct RawParent
{
    public PortablePadded Value;
    public int Tail;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortablePrivate
{
    [LuminPackInclude] private readonly byte _tag;
    public long Value { get; }

    /// <summary>Initializes private storage and a readonly auto-property.</summary>
    /// <param name="_tag">The private prefix.</param>
    /// <param name="value">The property storage.</param>
    public PortablePrivate(byte _tag, long value) { this._tag = _tag; Value = value; }
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed), StructLayout(LayoutKind.Sequential, Size = 32)]
public partial struct PortableSized
{
    public int Value;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed), StructLayout(LayoutKind.Explicit, Size = 32)]
public partial struct PortableExplicit
{
    [FieldOffset(12)] public byte First;
    [FieldOffset(0)] public long Second;
}

[LuminPackable, LuminPackOption(StructLayout = global::LuminPack.Attribute.LuminPackStructLayout.Packed)]
public partial struct PortableGeneric<T> where T : unmanaged
{
    public byte Tag;
    public T Value;
}

[LuminPackable]
public partial class PortableContainer
{
    public int First;
    public PortablePadded Value;
    public PortablePadded[] Array = null!;
    public List<PortablePadded> List = null!;
    public int Last;
}
