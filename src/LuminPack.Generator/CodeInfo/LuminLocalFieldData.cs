namespace LuminPack.Code;

/// <summary>Provides lumin local field data functionality.</summary>
public sealed class LuminLocalFieldData
{
    /// <summary>Stores the type name.</summary>
    public string TypeName;
    /// <summary>Stores the type symbol.</summary>
    public Microsoft.CodeAnalysis.ITypeSymbol TypeSymbol;
    /// <summary>Whether the referenced runtime exposes the existing priority-queue heap access helper.</summary>
    internal bool CanAccessPriorityQueueNodes;
    /// <summary>Whether the closed small raw element and referenced runtime permit grouped dense hash-set stores.</summary>
    internal bool CanGroupSmallHashSetWrites;
    /// <summary>Whether a closed framework comparison contract can use the runtime's fresh sorted-set builder.</summary>
    internal bool CanBuildSortedSet;
    /// <summary>Whether the referenced runtime can reconstruct an eligible mutable sorted set.</summary>
    internal bool CanRebuildSortedSet;
    /// <summary>Whether a string-keyed sorted list can use the referenced runtime's ordered append helper.</summary>
    internal bool CanAppendSortedListOrdered;
    /// <summary>Stores the canonical local Union cursor slot selected for a dictionary operation, if any.</summary>
    internal string UnionCursorSuffix;
    /// <summary>Stores the string encoding of the Union root's declared binary slot.</summary>
    internal int UnionCursorEncoding;
    /// <summary>Stores the string recording of the Union root's declared binary slot.</summary>
    internal int UnionCursorRecording;
    /// <summary>Stores the fixed Union cursor cases and their narrow tags in tag order, tested before the cursor slot.</summary>
    internal System.Collections.Generic.List<(int Id, string Type)> UnionCursorCases;
    /// <summary>Stores the canonical proof that an int-keyed dictionary can refill existing leaf values in place.</summary>
    internal bool CanReuseDictionarySlots;
    /// <summary>Whether an int-keyed dictionary with string or known scalar values may refill existing slots in place.</summary>
    internal bool CanReuseDictionaryScalarSlots;
    /// <summary>Whether a closed string-key dictionary can use the optional complete-staging runtime kernel.</summary>
    internal bool CanReadStableStringKeys;
    /// <summary>Stores the certified narrow tags and case types whose existing exact instances an int-keyed dictionary can refill in place.</summary>
    internal System.Collections.Generic.List<(int Id, string Type)> UnionSlotCases;
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
