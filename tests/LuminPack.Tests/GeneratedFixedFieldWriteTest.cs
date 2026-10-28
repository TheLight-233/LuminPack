using System.Runtime.InteropServices;
using LuminPack;
using LuminPack.Attribute;
using LuminPack.Core;
using LuminPack.Generated;
using LuminPack.Option;
using Xunit;

namespace LuminPackUnitTest;
/// <summary>Provides generated fixed field write test functionality.</summary>
public class GeneratedFixedFieldWriteTest
{
    /// <summary>Verifies direct field reads preserve an existing root instance.</summary>
    [Fact]
    public void DirectFieldReadsPreserveAnExistingRootInstance()
    {
        var source = CreateValue();
        byte[] payload = LuminPackSerializer.Serialize(source);
        var destination = new FixedPrimitiveWriteModel
        {
            Id = 17,
            Prefix = "old",
            Suffix = "old"
        };
        var original = destination;
        int consumed = LuminPackSerializer.Deserialize(payload.AsSpan(), ref destination);
        Assert.Same(original, destination);
        Assert.Equal(payload.Length, consumed);
        Assert.Equal(payload, LuminPackSerializer.Serialize(destination));
    }

#if DEBUG
    /// <summary>Verifies truncated fixed group does not publish partial root fields.</summary>
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
    /// <summary>Verifies mixed widths keep existing wire bytes and offset bounds.</summary>
    /// <param name = "encoding">The encoding used by this operation.</param>
    [Theory]
    [InlineData(LuminPackStringEncoding.UTF8)]
    [InlineData(LuminPackStringEncoding.UTF16)]
    public void MixedWidthsKeepExistingWireBytesAndOffsetBounds(LuminPackStringEncoding encoding)
    {
        var value = CreateValue();
        var option = new TestSerializationProfile
        {
            StringEncoding = encoding
        };
        byte[] expected = ReferenceWrite(value, option);
        Assert.Equal(expected, LuminPackSerializer.Serialize(value, option));
        var copy = LuminPackSerializer.Deserialize<FixedPrimitiveWriteModel>(expected, option);
        Assert.Equal(BitConverter.SingleToInt32Bits(value.F32), BitConverter.SingleToInt32Bits(copy.F32));
        Assert.Equal(BitConverter.DoubleToInt64Bits(value.F64), BitConverter.DoubleToInt64Bits(copy.F64));
        Assert.Equal(expected, LuminPackSerializer.Serialize(copy, option));
        // The fixed-span writer can exercise primitive blocks directly.
        // String-bearing formatters also use the pooled writer's flush path.
        var fixedValue = new FixedPrimitiveGuardModel
        {
            First = int.MinValue,
            Second = ulong.MaxValue,
            Third = value.Decimal,
            Fourth = true
        };
        byte[] fixedExpected = LuminPackSerializer.Serialize(fixedValue, option);
        foreach (int prefix in new[]
        {
            1,
            7,
            31
        }

        )
        {
            byte[] guarded = Enumerable.Repeat((byte)0xCC, prefix + fixedExpected.Length + 9).ToArray();
            Span<byte> destination = guarded.AsSpan(prefix, fixedExpected.Length);
            using var state = new LuminPackWriterOptionalState();
            var writer = new LuminPackWriter(ref destination, state);
            writer.Write(in fixedValue);
            Assert.Equal(fixedExpected.Length, writer.CurrentIndex);
            Assert.Equal(fixedExpected, destination.ToArray());
            Assert.All(guarded.Take(prefix).Concat(guarded.Skip(prefix + fixedExpected.Length)), b => Assert.Equal((byte)0xCC, b));
        }
    }

    /// <summary>Verifies nested arrays and lists keep headers nulls and field offsets.</summary>
    [Fact]
    public void NestedArraysAndListsKeepHeadersNullsAndFieldOffsets()
    {
        var item = CreateValue();
        var value = new FixedPrimitiveWriteContainer
        {
            Array = new[]
            {
                item,
                null!,
                item
            },
            List = new List<FixedPrimitiveWriteModel>
            {
                null!,
                item
            }
        };
        // The library default is the UTF-16 length profile.
        byte[] child = ReferenceWrite(item, TestSerializationProfile.Utf16WithLength);
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
        Assert.Equal(payload, LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<FixedPrimitiveWriteContainer>(payload)));
    }

    /// <summary>Verifies large structs and properties keep existing in parameter behavior.</summary>
    [Fact]
    public void LargeStructsAndPropertiesKeepExistingInParameterBehavior()
    {
        var value = new FixedPrimitiveWriteFallback
        {
            Id = -17,
            Large = new()
            {
                Value = long.MinValue
            },
            Value = long.MaxValue
        };
        Span<byte> span = new byte[1024];
        var writer = new LuminPackWriter(ref span);
        ref byte __fixed0Reference = ref writer.Cursor;
        int __fixed0Length = 0;
        __fixed0Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, __fixed0Length), (byte)3);
        __fixed0Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, __fixed0Length), value.Id);
        __fixed0Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, __fixed0Length), value.Large);
        __fixed0Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed0Reference, __fixed0Length), value.Value);
        writer.Advance(__fixed0Length);
        byte[] expected = writer.GetSpan().ToArray();
        Assert.Equal(expected, LuminPackSerializer.Serialize(value));
        Assert.Equal(expected, LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<FixedPrimitiveWriteFallback>(expected)));
    }

    /// <summary>Verifies nested fallback blocks advance the collection offset.</summary>
    [Fact]
    public void NestedFallbackBlocksAdvanceTheCollectionOffset()
    {
        var child = new FixedPrimitiveWriteFallback
        {
            Id = 71,
            Large = new()
            {
                Value = -1234567
            },
            Value = long.MaxValue
        };
        var item = new FixedPrimitiveFallbackElement
        {
            Child = child,
            Trailer = 0x123456789ABCDEF0
        };
        var value = new FixedPrimitiveFallbackCollection
        {
            Items = new()
            {
                item,
                null!,
                item
            }
        };
        // The existing inline nested-class schema places auto-properties first.
        // Use the original writer API to construct that wire representation.
        Span<byte> childSpan = new byte[1024];
        var childWriter = new LuminPackWriter(ref childSpan);
        ref byte __fixed1Reference = ref childWriter.Cursor;
        int __fixed1Length = 0;
        __fixed1Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed1Reference, __fixed1Length), (byte)3);
        __fixed1Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed1Reference, __fixed1Length), child.Value);
        __fixed1Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed1Reference, __fixed1Length), child.Id);
        __fixed1Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed1Reference, __fixed1Length), child.Large);
        childWriter.Advance(__fixed1Length);
        byte[] childPayload = childWriter.GetSpan().ToArray();
        using var expected = new MemoryStream();
        expected.WriteByte(1);
        expected.Write(BitConverter.GetBytes(3));
        for (int i = 0; i < 3; i++)
        {
            if (i == 1)
            {
                expected.WriteByte(255);
                continue;
            }

            expected.WriteByte(2);
            expected.Write(childPayload);
            expected.Write(BitConverter.GetBytes(item.Trailer));
        }

        byte[] payload = expected.ToArray();
        Assert.Equal(payload, LuminPackSerializer.Serialize(value));
        Assert.Equal(payload, LuminPackSerializer.Serialize(LuminPackSerializer.Deserialize<FixedPrimitiveFallbackCollection>(payload)));
    }

    /// <summary>Verifies reference write.</summary>
    /// <param name = "value">The value consumed or produced by this operation.</param>
    /// <param name = "option">The configuration to apply to this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static byte[] ReferenceWrite(FixedPrimitiveWriteModel value, TestSerializationProfile option)
    {
        Span<byte> span = new byte[1024];
        using var state = new LuminPackWriterOptionalState();
        var writer = new LuminPackWriter(ref span, state);
        ref byte __fixed2Reference = ref writer.Cursor;
        int __fixed2Length = 0;
        __fixed2Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed2Reference, __fixed2Length), (byte)21);
        __fixed2Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed2Reference, __fixed2Length), value.Id);
        writer.Advance(__fixed2Length);
        writer.WriteFor(option, value.Prefix);
        ref byte __fixed3Reference = ref writer.Cursor;
        int __fixed3Length = 0;
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.U8);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.I8);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.Flag);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.I16);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.U16);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.Character);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.I32);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.U32);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.I64);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.U64);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.F32);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.F64);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.Decimal);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.Small);
        __fixed3Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed3Reference, __fixed3Length), value.Medium);
        writer.Advance(__fixed3Length);
        writer.Advance(global::LuminPack.Core.LuminPackWriter.Write(ref writer.Cursor, value.Wide));
        writer.WriteFor(option, value.Suffix);
        ref byte __fixed4Reference = ref writer.Cursor;
        int __fixed4Length = 0;
        __fixed4Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed4Reference, __fixed4Length), value.Marker);
        __fixed4Length += global::LuminPack.Core.LuminPackWriter.Write(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref __fixed4Reference, __fixed4Length), value.End);
        writer.Advance(__fixed4Length);
        return writer.GetSpan().ToArray();
    }

    /// <summary>Creates value from the supplied input.</summary>
    /// <returns>The result of the operation.</returns>
    private static FixedPrimitiveWriteModel CreateValue() => new()
    {
        Id = -123,
        Prefix = "prefix_\u4E2D\u6587",
        U8 = byte.MaxValue,
        I8 = sbyte.MinValue,
        Flag = true,
        I16 = short.MinValue,
        U16 = ushort.MaxValue,
        Character = '\ud800',
        I32 = int.MinValue,
        U32 = uint.MaxValue,
        I64 = long.MinValue,
        U64 = ulong.MaxValue,
        F32 = BitConverter.Int32BitsToSingle(unchecked((int)0xFFC01234)),
        F64 = -0.0,
        Decimal = new decimal (12345, 0, 0, true, 4),
        Small = (FixedByteEnum)255,
        Medium = (FixedUShortEnum)65535,
        Wide = (FixedULongEnum)ulong.MaxValue,
        Suffix = "tail_😀",
        Marker = 12345,
        End = long.MaxValue
    };
}

/// <summary>Provides fixed byte enum functionality.</summary>
public enum FixedByteEnum : byte
{
    /// <summary>Specifies zero.</summary>
    Zero
}

/// <summary>Provides fixed u short enum functionality.</summary>
public enum FixedUShortEnum : ushort
{
    /// <summary>Specifies zero.</summary>
    Zero
}

/// <summary>Provides fixed u long enum functionality.</summary>
public enum FixedULongEnum : ulong
{
    /// <summary>Specifies zero.</summary>
    Zero
}

/// <summary>Provides fixed primitive write model functionality.</summary>
[LuminPackable]
public sealed partial class FixedPrimitiveWriteModel
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the prefix.</summary>
    public string Prefix = null!;
    /// <summary>Stores the u8.</summary>
    public byte U8;
    /// <summary>Stores the i8.</summary>
    public sbyte I8;
    /// <summary>Stores the flag.</summary>
    public bool Flag;
    /// <summary>Stores the i16.</summary>
    public short I16;
    /// <summary>Stores the u16.</summary>
    public ushort U16;
    /// <summary>Stores the character.</summary>
    public char Character;
    /// <summary>Stores the i32.</summary>
    public int I32;
    /// <summary>Stores the u32.</summary>
    public uint U32;
    /// <summary>Stores the i64.</summary>
    public long I64;
    /// <summary>Stores the u64.</summary>
    public ulong U64;
    /// <summary>Stores the f32.</summary>
    public float F32;
    /// <summary>Stores the f64.</summary>
    public double F64;
    /// <summary>Stores the decimal.</summary>
    public decimal Decimal;
    /// <summary>Stores the small.</summary>
    public FixedByteEnum Small;
    /// <summary>Stores the medium.</summary>
    public FixedUShortEnum Medium;
    /// <summary>Stores the wide.</summary>
    public FixedULongEnum Wide;
    /// <summary>Stores the suffix.</summary>
    public string Suffix = null!;
    /// <summary>Stores the marker.</summary>
    public int Marker;
    /// <summary>Stores the end.</summary>
    public long End;
}

/// <summary>Provides fixed primitive write container functionality.</summary>
[LuminPackable]
public sealed partial class FixedPrimitiveWriteContainer
{
    /// <summary>Stores the array.</summary>
    public FixedPrimitiveWriteModel[] Array = null!;
    /// <summary>Stores the list.</summary>
    public List<FixedPrimitiveWriteModel> List = null!;
}

/// <summary>Provides fixed primitive guard model functionality.</summary>
[LuminPackable]
public sealed partial class FixedPrimitiveGuardModel
{
    /// <summary>Stores the first.</summary>
    public int First;
    /// <summary>Stores the second.</summary>
    public ulong Second;
    /// <summary>Stores the third.</summary>
    public decimal Third;
    /// <summary>Stores the fourth.</summary>
    public bool Fourth;
}

/// <summary>Provides fixed primitive write large value functionality.</summary>
[StructLayout(LayoutKind.Sequential, Size = 256)]
public struct FixedPrimitiveWriteLargeValue
{
    /// <summary>Stores the value.</summary>
    public long Value;
}

/// <summary>Provides fixed primitive write fallback functionality.</summary>
[LuminPackable]
public sealed partial class FixedPrimitiveWriteFallback
{
    /// <summary>Stores the id.</summary>
    public int Id;
    /// <summary>Stores the large.</summary>
    public FixedPrimitiveWriteLargeValue Large;
    /// <summary>Gets or sets the value.</summary>
    public long Value { get; set; }
}

/// <summary>Provides fixed primitive fallback element functionality.</summary>
[LuminPackable]
public sealed partial class FixedPrimitiveFallbackElement
{
    /// <summary>Stores the child.</summary>
    public FixedPrimitiveWriteFallback Child = null!;
    /// <summary>Stores the trailer.</summary>
    public long Trailer;
}

/// <summary>Provides fixed primitive fallback collection functionality.</summary>
[LuminPackable]
public sealed partial class FixedPrimitiveFallbackCollection
{
    /// <summary>Stores the items.</summary>
    public List<FixedPrimitiveFallbackElement> Items = null!;
}
