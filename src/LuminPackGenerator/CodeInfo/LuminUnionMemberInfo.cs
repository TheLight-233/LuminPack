using Microsoft.CodeAnalysis;

namespace LuminPack.Code;

/// <summary>Provides lumin union member info functionality.</summary>
public readonly record struct LuminUnionMemberInfo
{
    /// <summary>Stores the id.</summary>
    public readonly ushort Id;
    /// <summary>Stores the type.</summary>
    public readonly INamedTypeSymbol Type;
    /// <summary>Stores the can generate dispatch.</summary>
    public readonly bool CanGenerateDispatch;

    /// <summary>Initializes a new instance of <c>LuminUnionMemberInfo</c>.</summary>
    /// <param name="id">The id used by this operation.</param>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="canGenerateDispatch">The can generate dispatch used by this operation.</param>
    public LuminUnionMemberInfo(ushort id, INamedTypeSymbol type, bool canGenerateDispatch = false)
    {
        Id = id;
        Type = type;
        CanGenerateDispatch = canGenerateDispatch;
    }
}
