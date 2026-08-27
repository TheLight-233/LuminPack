namespace LuminPack.Code;

/// <summary>
/// Method-pointer entry for one manually registered cross-assembly union member. Each field is the
/// raw address of a static method (converted to <see cref="nint"/>); 0 means the operation is not
/// supported. The generated union root slot casts the pointer back to the root-typed function
/// pointer and calls it directly - the value is never boxed.
/// </summary>
public struct LuminUnionFormatterEntry
{
    public int Tag;
    public nint WriteValue;
    public nint ReadValue;
    public nint WriteValueJson;
    public nint ReadValueJson;
}