namespace LuminPack.Internal;

// VarInt, first sbyte is value or typeCode

// 0~127 = unsigned byte value
// -1~-120 = signed byte value
// -121 = byte
// -122 = sbyte
// -123 = ushort
// -124 = short
// -125 = uint
// -126 = int
// -127 = ulong
// -128 = long

/// <summary>Provides var int codes functionality.</summary>
internal static class VarIntCodes
{
    /// <summary>Stores the max single value.</summary>
    public const byte MaxSingleValue = 127;
    /// <summary>Stores the min single value.</summary>
    public const sbyte MinSingleValue = -120;

    /// <summary>Stores the byte.</summary>
    public const sbyte Byte = -121;
    /// <summary>Stores the s byte.</summary>
    public const sbyte SByte = -122;
    /// <summary>Stores the u int16.</summary>
    public const sbyte UInt16 = -123;
    /// <summary>Stores the int16.</summary>
    public const sbyte Int16 = -124;
    /// <summary>Stores the u int32.</summary>
    public const sbyte UInt32 = -125;
    /// <summary>Stores the int32.</summary>
    public const sbyte Int32 = -126;
    /// <summary>Stores the u int64.</summary>
    public const sbyte UInt64 = -127;
    /// <summary>Stores the int64.</summary>
    public const sbyte Int64 = -128;
}
