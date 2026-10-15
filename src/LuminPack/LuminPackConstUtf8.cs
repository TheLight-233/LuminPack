namespace LuminPack;

/// <summary>Provides lumin pack const utf8 functionality.</summary>
public static class LuminPackConstUtf8
{
    // "$type" in UTF8 bytes: $ (36), t (116), y (121), p (112), e (101)
    /// <summary>Stores the type u8.</summary>
    public static readonly byte[] TypeU8 = new byte[] { 36, 116, 121, 112, 101 };

    // "$value" in UTF8 bytes: $ (36), v (118), a (97), l (108), u (117), e (101)
    /// <summary>Stores the value u8.</summary>
    public static readonly byte[] ValueU8 = new byte[] { 36, 118, 97, 108, 117, 101 };

    // "$type" in UTF16 bytes
    /// <summary>Stores the type u16.</summary>
    public static readonly char[] TypeU16 = new char[] { '$', 't', 'y', 'p', 'e' };

    // "$value" in UTF16 bytes
    /// <summary>Stores the value u16.</summary>
    public static readonly char[] ValueU16 = new char[] { '$', 'v', 'a', 'l', 'u', 'e' };
}
