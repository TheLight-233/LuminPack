using System;
using System.Linq;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>Identity of a payload type, independent of any analysis layer.</summary>
internal sealed class TypeIdentity : IEquatable<TypeIdentity>
{
    /// <summary>The declaring namespace, or null in the global namespace.</summary>
    internal readonly string? Namespace;

    /// <summary>The containing type names from the outermost declaration inward.</summary>
    internal readonly string[] ContainingTypes;

    /// <summary>The type name without its arity suffix.</summary>
    internal readonly string Name;

    /// <summary>The declared type parameter names, which are empty for a closed definition.</summary>
    internal readonly string[] TypeParameters;

    /// <summary>Captures a type identity.</summary>
    internal TypeIdentity(string? @namespace, string[] containingTypes, string name, string[] typeParameters)
    {
        Namespace = @namespace;
        ContainingTypes = containingTypes;
        Name = name;
        TypeParameters = typeParameters;
    }

    /// <summary>Whether the definition declares type parameters.</summary>
    internal bool IsGeneric => TypeParameters.Length != 0;

    /// <summary>The CLR metadata name, including the arity suffix and nested-type separators.</summary>
    internal string MetadataName
    {
        get
        {
            string name = TypeParameters.Length == 0 ? Name : Name + "`" + TypeParameters.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string nested = string.Join("+", ContainingTypes.Concat(new[] { name }));
            return Namespace == null ? nested : Namespace + "." + nested;
        }
    }

    /// <summary>The C# display name, such as <c>N.Outer.Inner&lt;T&gt;</c>.</summary>
    internal string DisplayName
    {
        get
        {
            string name = TypeParameters.Length == 0 ? Name : Name + "<" + string.Join(", ", TypeParameters) + ">";
            string nested = string.Join(".", ContainingTypes.Concat(new[] { name }));
            return Namespace == null ? nested : Namespace + "." + nested;
        }
    }

    /// <summary>The fully qualified C# spelling.</summary>
    internal string GlobalName => "global::" + DisplayName;

    /// <inheritdoc/>
    public bool Equals(TypeIdentity? other) => other != null && Namespace == other.Namespace && Name == other.Name &&
        ContainingTypes.SequenceEqual(other.ContainingTypes) && TypeParameters.SequenceEqual(other.TypeParameters);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => MetadataName.GetHashCode();
}
