namespace LuminPack.SourceGenerator.Next.Rendering.Direct;

/// <summary>The analyzer's scalar classification of a serialized member.</summary>
internal enum LuminFiledType : byte
{
    /// <summary>Specifies int.</summary>
    Int,

    /// <summary>Specifies uint.</summary>
    UInt,

    /// <summary>Specifies byte.</summary>
    Byte,

    /// <summary>Specifies sbyte.</summary>
    SByte,

    /// <summary>Specifies short.</summary>
    Short,

    /// <summary>Specifies ushort.</summary>
    UShort,

    /// <summary>Specifies long.</summary>
    Long,

    /// <summary>Specifies ulong.</summary>
    ULong,

    /// <summary>Specifies float.</summary>
    Float,

    /// <summary>Specifies double.</summary>
    Double,

    /// <summary>Specifies decimal.</summary>
    Decimal,

    /// <summary>Specifies char.</summary>
    Char,

    /// <summary>Specifies string.</summary>
    String,

    /// <summary>Specifies bool.</summary>
    Bool,

    /// <summary>Specifies enum.</summary>
    Enum,

    /// <summary>Specifies struct.</summary>
    Struct,

    /// <summary>Specifies class.</summary>
    Class,

    /// <summary>Specifies list.</summary>
    List,

    /// <summary>Specifies array.</summary>
    Array,

    /// <summary>Specifies other.</summary>
    Other,
}
