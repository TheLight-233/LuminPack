namespace LuminPack.Code;

public sealed class LuminLocalFieldData
{
    public string TypeName;
    public Microsoft.CodeAnalysis.ITypeSymbol TypeSymbol;
    public string Name;
    public string Identifier =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(Name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ||
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetContextualKeywordKind(Name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? "@" + Name
            : Name;
    public int filedOffset;
    public bool IsValue;

    /// <summary>
    /// True when the member is a private/protected storage location that cannot be named from the
    /// generated extension class (explicit private/protected field, or an auto-property backing field).
    /// A Local layout mirror is only emitted for types that have at least one such member.
    /// </summary>
    public bool IsPrivate;

    /// <summary>True when the member is an auto-property; its mirrored slot is the backing field.</summary>
    public bool IsProperty;
}
