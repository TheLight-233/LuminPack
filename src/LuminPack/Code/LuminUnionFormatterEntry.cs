namespace LuminPack.Code;

/// <summary>
/// Method-pointer entry for one manually registered cross-assembly union member. Each field is the
/// raw address of a static method (converted to <see cref="nint"/>); 0 means the operation is not
/// supported. The generated union root slot casts the pointer back to the root-typed function
/// pointer and calls it directly - the value is never boxed.
/// </summary>
public struct LuminUnionFormatterEntry
{
    /// <summary>Stores the tag.</summary>
    public int Tag;
    /// <summary>Stores the write value.</summary>
    public nint WriteValue;
    /// <summary>Stores the read value.</summary>
    public nint ReadValue;
    /// <summary>Stores the write value json.</summary>
    public nint WriteValueJson;
    /// <summary>Stores the read value json.</summary>
    public nint ReadValueJson;
}
