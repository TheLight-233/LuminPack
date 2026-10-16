namespace LuminPack.Code;

/// <summary>Provides lumin local field data functionality.</summary>
public sealed class LuminLocalFieldData
{
    /// <summary>Stores the type name.</summary>
    public string TypeName;
    /// <summary>Stores the type symbol.</summary>
    public Microsoft.CodeAnalysis.ITypeSymbol TypeSymbol;
    /// <summary>Stores the name.</summary>
    public string Name;
    /// <summary>Stores the exact declaring storage level, including constructed generic arguments.</summary>
    public string DeclaringTypeName;
    /// <summary>Gets the identifier.</summary>
    public string Identifier =>
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetKeywordKind(Name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None ||
        Microsoft.CodeAnalysis.CSharp.SyntaxFacts.GetContextualKeywordKind(Name) != Microsoft.CodeAnalysis.CSharp.SyntaxKind.None
            ? "@" + Name
            : Name;
    /// <summary>Stores the filed offset.</summary>
    public int filedOffset;
    /// <summary>Stores the is value.</summary>
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
