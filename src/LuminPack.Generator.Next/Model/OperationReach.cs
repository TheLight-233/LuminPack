using System;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>The payload identities a declaration or a call payload reaches, keyed by <see cref="TypeIdentity.MetadataName"/>.</summary>
/// <remarks>Retained operations follow these edges: every identity a member type, a generic argument, a base type, an interface
/// or an attribute type names keeps the operations of the code that reaches it. A base type or interface also reaches back to
/// the declaration, since a Union root dispatches to the derived cases it discovers.</remarks>
internal sealed class OperationReach : IEquatable<OperationReach>
{
    /// <summary>No reached identity.</summary>
    internal static readonly OperationReach Empty = new(Array.Empty<string>(), Array.Empty<string>());

    /// <summary>The reached identities in discovery order.</summary>
    internal readonly string[] Keys;

    /// <summary>The source base types and interfaces of a declaration, which reach the declaration in turn.</summary>
    internal readonly string[] Bases;

    /// <summary>Captures the reached identities.</summary>
    /// <param name="keys">The reached identities in discovery order.</param>
    /// <param name="bases">The source base types and interfaces of a declaration.</param>
    internal OperationReach(string[] keys, string[] bases)
    {
        Keys = keys;
        Bases = bases;
    }

    /// <inheritdoc/>
    public bool Equals(OperationReach? other)
    {
        if (ReferenceEquals(this, other))
            return true;
        return other != null && Same(Keys, other.Keys) && Same(Bases, other.Bases);
    }

    private static bool Same(string[] left, string[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (!string.Equals(left[index], right[index], StringComparison.Ordinal))
                return false;
        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as OperationReach);

    /// <inheritdoc/>
    public override int GetHashCode() => Keys.Length == 0 ? 0 : Keys.Length * 31 + Keys[0].GetHashCode();
}
