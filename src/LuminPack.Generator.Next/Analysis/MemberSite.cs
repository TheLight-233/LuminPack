using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>The facts member classification reads from a serialized storage location.</summary>
/// <remarks>A declared field or property supplies its own facts. A closed serializer call root has no member symbol, so it is
/// classified as an unnamed, non-volatile storage location of its closed type without a declaring payload.</remarks>
internal readonly struct MemberSite
{
    /// <summary>Captures the storage facts.</summary>
    /// <param name="name">The member name, or empty for a call root.</param>
    /// <param name="type">The declared storage type.</param>
    /// <param name="isVolatile">Whether the storage is a volatile field.</param>
    /// <param name="containing">The declaring type, or null for a call root.</param>
    internal MemberSite(string name, ITypeSymbol type, bool isVolatile, INamedTypeSymbol? containing)
    {
        Name = name;
        Type = type;
        Volatile = isVolatile;
        Containing = containing;
    }

    /// <summary>The member name.</summary>
    internal string Name { get; }

    /// <summary>The declared storage type.</summary>
    internal ITypeSymbol Type { get; }

    /// <summary>Whether the storage is a volatile field.</summary>
    internal bool Volatile { get; }

    /// <summary>The declaring type, or null for a call root.</summary>
    internal INamedTypeSymbol? Containing { get; }

    /// <summary>Reads the facts of a declared field or property.</summary>
    /// <param name="member">The field or property.</param>
    /// <returns>The storage facts.</returns>
    internal static MemberSite Of(ISymbol member) => new MemberSite(member.Name,
        member is IFieldSymbol field ? field.Type : ((IPropertySymbol)member).Type, member is IFieldSymbol { IsVolatile: true },
        member.ContainingType);
}
