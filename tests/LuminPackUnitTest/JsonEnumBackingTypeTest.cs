using System.Text.Json;
using LuminPack;
using LuminPack.Attribute;
using Xunit;

namespace LuminPackUnitTest;

// JSON writes enums as their raw integer token, so every declared backing width
// must survive the trip. Narrowing to int truncated 64-bit-backed enums and
// reinterpreting a narrow enum as int read past the value.
public enum BackingByte : byte { Min = byte.MinValue, Max = byte.MaxValue }
public enum BackingSByte : sbyte { Min = sbyte.MinValue, Max = sbyte.MaxValue }
public enum BackingShort : short { Min = short.MinValue, Max = short.MaxValue }
public enum BackingUShort : ushort { Min = ushort.MinValue, Max = ushort.MaxValue }
public enum BackingInt : int { Min = int.MinValue, Max = int.MaxValue }
public enum BackingUInt : uint { Min = uint.MinValue, Max = uint.MaxValue }
public enum BackingLong : long { Min = long.MinValue, Max = long.MaxValue }
public enum BackingULong : ulong { Min = ulong.MinValue, Max = ulong.MaxValue }

[LuminPackable]
public sealed partial class EnumHolder
{
    public BackingByte ByteValue;
    public BackingSByte SByteValue;
    public BackingShort ShortValue;
    public BackingUShort UShortValue;
    public BackingInt IntValue;
    public BackingUInt UIntValue;
    public BackingLong LongValue;
    public BackingULong ULongValue;
}

public class JsonEnumBackingTypeTest
{
    [Fact]
    public void ObjectFieldsRoundTripMinimumValues()
    {
        var value = new EnumHolder
        {
            ByteValue = BackingByte.Min,
            SByteValue = BackingSByte.Min,
            ShortValue = BackingShort.Min,
            UShortValue = BackingUShort.Min,
            IntValue = BackingInt.Min,
            UIntValue = BackingUInt.Min,
            LongValue = BackingLong.Min,
            ULongValue = BackingULong.Min,
        };

        var roundTrip = RoundTrip(value, out var root);

        Assert.Equal(byte.MinValue, root.GetProperty(nameof(EnumHolder.ByteValue)).GetByte());
        Assert.Equal(sbyte.MinValue, root.GetProperty(nameof(EnumHolder.SByteValue)).GetSByte());
        Assert.Equal(short.MinValue, root.GetProperty(nameof(EnumHolder.ShortValue)).GetInt16());
        Assert.Equal(ushort.MinValue, root.GetProperty(nameof(EnumHolder.UShortValue)).GetUInt16());
        Assert.Equal(int.MinValue, root.GetProperty(nameof(EnumHolder.IntValue)).GetInt32());
        Assert.Equal(uint.MinValue, root.GetProperty(nameof(EnumHolder.UIntValue)).GetUInt32());
        Assert.Equal(long.MinValue, root.GetProperty(nameof(EnumHolder.LongValue)).GetInt64());
        Assert.Equal(ulong.MinValue, root.GetProperty(nameof(EnumHolder.ULongValue)).GetUInt64());

        Assert.Equal(value.ByteValue, roundTrip.ByteValue);
        Assert.Equal(value.SByteValue, roundTrip.SByteValue);
        Assert.Equal(value.ShortValue, roundTrip.ShortValue);
        Assert.Equal(value.UShortValue, roundTrip.UShortValue);
        Assert.Equal(value.IntValue, roundTrip.IntValue);
        Assert.Equal(value.UIntValue, roundTrip.UIntValue);
        Assert.Equal(value.LongValue, roundTrip.LongValue);
        Assert.Equal(value.ULongValue, roundTrip.ULongValue);
    }

    [Fact]
    public void ObjectFieldsRoundTripMaximumValues()
    {
        var value = new EnumHolder
        {
            ByteValue = BackingByte.Max,
            SByteValue = BackingSByte.Max,
            ShortValue = BackingShort.Max,
            UShortValue = BackingUShort.Max,
            IntValue = BackingInt.Max,
            UIntValue = BackingUInt.Max,
            LongValue = BackingLong.Max,
            ULongValue = BackingULong.Max,
        };

        var roundTrip = RoundTrip(value, out var root);

        Assert.Equal(byte.MaxValue, root.GetProperty(nameof(EnumHolder.ByteValue)).GetByte());
        Assert.Equal(sbyte.MaxValue, root.GetProperty(nameof(EnumHolder.SByteValue)).GetSByte());
        Assert.Equal(short.MaxValue, root.GetProperty(nameof(EnumHolder.ShortValue)).GetInt16());
        Assert.Equal(ushort.MaxValue, root.GetProperty(nameof(EnumHolder.UShortValue)).GetUInt16());
        Assert.Equal(int.MaxValue, root.GetProperty(nameof(EnumHolder.IntValue)).GetInt32());
        Assert.Equal(uint.MaxValue, root.GetProperty(nameof(EnumHolder.UIntValue)).GetUInt32());
        Assert.Equal(long.MaxValue, root.GetProperty(nameof(EnumHolder.LongValue)).GetInt64());
        Assert.Equal(ulong.MaxValue, root.GetProperty(nameof(EnumHolder.ULongValue)).GetUInt64());

        Assert.Equal(value.LongValue, roundTrip.LongValue);
        Assert.Equal(value.ULongValue, roundTrip.ULongValue);
    }

    [Fact]
    public void StandaloneEnumFormattersPreserveEveryBackingWidth()
    {
        Assert.Equal("255", LuminPackSerializer.SerializeJson(BackingByte.Max));
        Assert.Equal("-128", LuminPackSerializer.SerializeJson(BackingSByte.Min));
        Assert.Equal("-32768", LuminPackSerializer.SerializeJson(BackingShort.Min));
        Assert.Equal("65535", LuminPackSerializer.SerializeJson(BackingUShort.Max));
        Assert.Equal("-2147483648", LuminPackSerializer.SerializeJson(BackingInt.Min));
        Assert.Equal("4294967295", LuminPackSerializer.SerializeJson(BackingUInt.Max));
        Assert.Equal("9223372036854775807", LuminPackSerializer.SerializeJson(BackingLong.Max));
        Assert.Equal("18446744073709551615", LuminPackSerializer.SerializeJson(BackingULong.Max));

        Assert.Equal(BackingByte.Max, LuminPackSerializer.DeserializeJson<BackingByte>("255"));
        Assert.Equal(BackingSByte.Min, LuminPackSerializer.DeserializeJson<BackingSByte>("-128"));
        Assert.Equal(BackingShort.Min, LuminPackSerializer.DeserializeJson<BackingShort>("-32768"));
        Assert.Equal(BackingUShort.Max, LuminPackSerializer.DeserializeJson<BackingUShort>("65535"));
        Assert.Equal(BackingInt.Min, LuminPackSerializer.DeserializeJson<BackingInt>("-2147483648"));
        Assert.Equal(BackingUInt.Max, LuminPackSerializer.DeserializeJson<BackingUInt>("4294967295"));
        Assert.Equal(BackingLong.Max, LuminPackSerializer.DeserializeJson<BackingLong>("9223372036854775807"));
        Assert.Equal(BackingULong.Max, LuminPackSerializer.DeserializeJson<BackingULong>("18446744073709551615"));
    }

    [Fact]
    public void EnumCollectionsPreserve64BitBackingWidths()
    {
        var longs = new List<BackingLong> { BackingLong.Min, 0, BackingLong.Max };
        var ulongs = new BackingULong[] { BackingULong.Min, (BackingULong)1, BackingULong.Max };

        var longJson = LuminPackSerializer.SerializeJson(longs);
        var ulongJson = LuminPackSerializer.SerializeJson(ulongs);

        Assert.Equal("[-9223372036854775808,0,9223372036854775807]", longJson);
        Assert.Equal("[0,1,18446744073709551615]", ulongJson);
        Assert.Equal(longs, LuminPackSerializer.DeserializeJson<List<BackingLong>>(longJson));
        Assert.Equal(ulongs, LuminPackSerializer.DeserializeJson<BackingULong[]>(ulongJson));
    }

    private static EnumHolder RoundTrip(EnumHolder value, out JsonElement root)
    {
        string json = LuminPackSerializer.SerializeJson(value);
        var document = JsonDocument.Parse(json);
        root = document.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(8, root.EnumerateObject().Count());
        return LuminPackSerializer.DeserializeJson<EnumHolder>(json);
    }
}