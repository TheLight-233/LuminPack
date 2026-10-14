using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;

public class GeneratedFixedFieldWriteTest
{
    [Fact]
    public void DirectFieldReadsPreserveAnExistingRootInstance()
    {
        var source = CreateValue();
        byte[] payload = LuminPackSerializer.Serialize(source);
        var destination = new FixedPrimitiveWriteModel { Id = 17, Prefix = "old", Suffix = "old" };
        var original = destination;
        int consumed = LuminPackSerializer.Deserialize(payload.AsSpan(), ref destination);
        Assert.Same(original, destination);
        Assert.Equal(payload.Length, consumed);
        Assert.Equal(payload, LuminPackSerializer.Serialize(destination));
    }

#if DEBUG
    [Fact]
    public void TruncatedFixedGroupDoesNotPublishPartialRootFields()
    {
        var source = new FixedPrimitiveGuardModel { First = 123, Second = ulong.MaxValue, Third = 1.25m, Fourth = true };
        byte[] payload = LuminPackSerializer.Serialize(source);
        var destination = new FixedPrimitiveGuardModel { First = -17, Second = 51, Third = -3.5m };
        var original = destination;
        Assert.ThrowsAny<Exception>(() => LuminPackSerializer.Deserialize(payload.AsSpan(0, payload.Length - 1), ref destination));
        Assert.Same(original, destination);
        Assert.Equal(-17, destination.First);
        Assert.Equal(51UL, destination.Second);
        Assert.Equal(-3.5m, destination.Third);
    }
#endif

    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void MixedWidthsKeepExistingWireBytesAndOffsetBounds(LuminPackStringEncoding encoding)
    {
        var value = CreateValue();
        var option = new LuminPackSerializerOption { StringEncoding = encoding };
        byte[] expected = ReferenceWrite(value, option);
        Assert.Equal(expected, LuminPackSerializer.Serialize(value, option));
        var copy = LuminPackSerializer.Deserialize<FixedPrimitiveWriteModel>(expected, option);
        Assert.Equal(BitConverter.SingleToInt32Bits(value.F32), BitConverter.SingleToInt32Bits(copy.F32));
        Assert.Equal(BitConverter.DoubleToInt64Bits(value.F64), BitConverter.DoubleToInt64Bits(copy.F64));
        Assert.Equal(expected, LuminPackSerializer.Serialize(copy, option));

        // The fixed-span writer can exercise primitive blocks directly.
        // String-bearing formatters also use the pooled writer's flush path.
        var fixedValue = new FixedPrimitiveGuardModel { First = int.MinValue, Second = ulong.MaxValue, Third = value.Decimal, Fourth = true };
        byte[] fixedExpected = LuminPackSerializer.Serialize(fixedValue, option);
        foreach (int prefix in new[] { 1, 7, 31 })
        {
            byte[] guarded = Enumerable.Repeat((byte)0xCC, prefix + fixedExpected.Length + 9).ToArray();
            Span<byte> destination = guarded.AsSpan(prefix, fixedExpected.Length);
            using var state = new LuminPackWriterOptionalState(option);
            var writer = new LuminPackWriter(ref destination, state);
            writer.WriteValue(in fixedValue);
            Assert.Equal(fixedExpected.Length, writer.CurrentIndex);
            Assert.Equal(fixedExpected, destination.ToArray());
            Assert.All(guarded.Take(prefix).Concat(guarded.Skip(prefix + fixedExpected.Length)),
                b => Assert.Equal((byte)0xCC, b));
        }
    }

    [Fact]
    public void NestedArraysAndListsKeepHeadersNullsAndFieldOffsets()
    {
        var item = CreateValue();
        var value = new FixedPrimitiveWriteContainer
        {
            Array = new[] { item, null!, item },
            List = new List<FixedPrimitiveWriteModel> { null!, item }
        };
        byte[] child = ReferenceWrite(item, LuminPackSerializerOption.Default);
        using var expected = new MemoryStream();
        expected.WriteByte(2);
        expected.Write(BitConverter.GetBytes(3));
        expected.Write(child);
        expected.WriteByte(255);
        expected.Write(child);
        expected.Write(BitConverter.GetBytes(2));
        expected.WriteByte(255);
        expected.Write(child);
        byte[] payload = expected.ToArray();
        Assert.Equal(payload, LuminPackSerializer.Serialize(value));
        Assert.Equal(payload, LuminPackSerializer.Serialize(
            LuminPackSerializer.Deserialize<FixedPrimitiveWriteContainer>(payload)));
    }

    [Fact]
    public void LargeStructsAndPropertiesKeepExistingInParameterBehavior()
    {
        var value = new FixedPrimitiveWriteFallback { Id = -17, Large = new() { Value = long.MinValue }, Value = long.MaxValue };
        Span<byte> span = new byte[1024];
        var writer = new LuminPackWriter(ref span);
        writer.Advance(writer.WriteUnmanaged((byte)3, value.Id, value.Large, value.Value));
        byte[] expected = writer.GetSpan().ToArray();
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(expected, LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<FixedPrimitiveWriteFallback>(expected)));
    }

    [Fact]
    public void NestedFallbackBlocksAdvanceTheCollectionOffset()
    {
        var child = new FixedPrimitiveWriteFallback { Id = 71, Large = new() { Value = -1234567 }, Value = long.MaxValue };
        var item = new FixedPrimitiveFallbackElement { Child = child, Trailer = 0x123456789ABCDEF0 };
        var value = new FixedPrimitiveFallbackCollection { Items = new() { item, null!, item } };
        // The existing inline nested-class schema places auto-properties first.
        // Use the original writer API to construct that wire representation.
        Span<byte> childSpan = new byte[1024];
        var childWriter = new LuminPackWriter(ref childSpan);
        childWriter.Advance(childWriter.WriteUnmanaged((byte)3, child.Value, child.Id, child.Large));
        byte[] childPayload = childWriter.GetSpan().ToArray();
        using var expected = new MemoryStream();
        expected.WriteByte(1);
        expected.Write(BitConverter.GetBytes(3));
        for (int i = 0; i < 3; i++)
        {
            if (i == 1) { expected.WriteByte(255); continue; }
            expected.WriteByte(2);
            expected.Write(childPayload);
            expected.Write(BitConverter.GetBytes(item.Trailer));
        }
        byte[] payload = expected.ToArray();
        Assert.Equal(payload, LuminPackSerializer.Serialize(value));
        Assert.Equal(payload, LuminPackSerializer.Serialize(
            LuminPackSerializer.Deserialize<FixedPrimitiveFallbackCollection>(payload)));
    }

    private static byte[] ReferenceWrite(FixedPrimitiveWriteModel value, LuminPackSerializerOption option)
    {
        Span<byte> span = new byte[1024];
        using var state = new LuminPackWriterOptionalState(option);
        var writer = new LuminPackWriter(ref span, state);
        writer.Advance(writer.WriteUnmanaged((byte)21, value.Id));
        writer.Advance(writer.WriteString(value.Prefix) + writer.StringRecordLength());
        writer.Advance(writer.WriteUnmanaged(value.U8, value.I8, value.Flag, value.I16, value.U16, value.Character,
            value.I32, value.U32, value.I64, value.U64, value.F32, value.F64, value.Decimal, value.Small, value.Medium));
        writer.Advance(writer.WriteUnmanaged(value.Wide));
        writer.Advance(writer.WriteString(value.Suffix) + writer.StringRecordLength());
        writer.Advance(writer.WriteUnmanaged(value.Marker, value.End));
        return writer.GetSpan().ToArray();
    }

    private static FixedPrimitiveWriteModel CreateValue() => new()
    {
        Id = -123, Prefix = "prefix_中文", U8 = byte.MaxValue, I8 = sbyte.MinValue, Flag = true,
        I16 = short.MinValue, U16 = ushort.MaxValue, Character = '\ud800', I32 = int.MinValue,
        U32 = uint.MaxValue, I64 = long.MinValue, U64 = ulong.MaxValue,
        F32 = BitConverter.Int32BitsToSingle(unchecked((int)0xFFC01234)), F64 = -0.0,
        Decimal = new decimal(12345, 0, 0, true, 4), Small = (FixedByteEnum)255,
        Medium = (FixedUShortEnum)65535, Wide = (FixedULongEnum)ulong.MaxValue,
        Suffix = "tail_😀", Marker = 12345, End = long.MaxValue
    };
}

public enum FixedByteEnum : byte { Zero }
public enum FixedUShortEnum : ushort { Zero }
public enum FixedULongEnum : ulong { Zero }

[LuminPackable]
public sealed partial class FixedPrimitiveWriteModel
{
    public int Id;
    public string Prefix = null!;
    public byte U8;
    public sbyte I8;
    public bool Flag;
    public short I16;
    public ushort U16;
    public char Character;
    public int I32;
    public uint U32;
    public long I64;
    public ulong U64;
    public float F32;
    public double F64;
    public decimal Decimal;
    public FixedByteEnum Small;
    public FixedUShortEnum Medium;
    public FixedULongEnum Wide;
    public string Suffix = null!;
    public int Marker;
    public long End;
}

[LuminPackable]
public sealed partial class FixedPrimitiveWriteContainer
{
    public FixedPrimitiveWriteModel[] Array = null!;
    public List<FixedPrimitiveWriteModel> List = null!;
}

[LuminPackable]
public sealed partial class FixedPrimitiveGuardModel
{
    public int First;
    public ulong Second;
    public decimal Third;
    public bool Fourth;
}

[StructLayout(LayoutKind.Sequential, Size = 256)]
public struct FixedPrimitiveWriteLargeValue
{
    public long Value;
}

[LuminPackable]
public sealed partial class FixedPrimitiveWriteFallback
{
    public int Id;
    public FixedPrimitiveWriteLargeValue Large;
    public long Value { get; set; }
}

[LuminPackable]
public sealed partial class FixedPrimitiveFallbackElement
{
    public FixedPrimitiveWriteFallback Child = null!;
    public long Trailer;
}

[LuminPackable]
public sealed partial class FixedPrimitiveFallbackCollection
{
    public List<FixedPrimitiveFallbackElement> Items = null!;
}
