using System;
using System.Linq;
using System.Text;

namespace LuminPack.SourceGenerator.Next.Model;

/// <summary>Identity of a payload type, independent of any analysis layer.</summary>
internal sealed class TypeIdentity : IEquatable<TypeIdentity>
{
    /// <summary>The declaring namespace, or null in the global namespace.</summary>
    internal readonly string? Namespace;

    /// <summary>The containing type names from the outermost declaration inward. A closed containing type spells its type
    /// arguments in its name, such as <c>Outer&lt;int&gt;</c>.</summary>
    internal readonly string[] ContainingTypes;

    /// <summary>The number of type parameters each containing type of a definition declares, parallel to
    /// <see cref="ContainingTypes"/>.</summary>
    internal readonly int[] ContainingArities;

    /// <summary>The type name without its arity suffix.</summary>
    internal readonly string Name;

    /// <summary>Every type parameter name the definition is generic over, the containing types' parameters first, which is
    /// empty for a closed definition.</summary>
    internal readonly string[] TypeParameters;

    private readonly string[] _declaredTypeParameters;
    private readonly string? _escapedGlobalName;

    /// <summary>Captures a type identity.</summary>
    /// <param name="namespace">The namespace supplied to <c>TypeIdentity</c>.</param>
    /// <param name="containingTypes">The containing types supplied to <c>TypeIdentity</c>.</param>
    /// <param name="name">The name to look up or emit.</param>
    /// <param name="typeParameters">The type parameters supplied to <c>TypeIdentity</c>.</param>
    /// <param name="containingArities">The containing arities supplied to <c>TypeIdentity</c>.</param>
    /// <param name="escapedGlobalName">The source spelling when any component needs a C# escape.</param>
    /// <param name="escapedTypeParameters">The parameter names to emit, when they differ from the declared names.</param>
    internal TypeIdentity(string? @namespace, string[] containingTypes, string name, string[] typeParameters,
        int[]? containingArities = null, string? escapedGlobalName = null, string[]? escapedTypeParameters = null)
    {
        Namespace = @namespace;
        ContainingTypes = containingTypes;
        ContainingArities = containingArities ?? new int[containingTypes.Length];
        Name = name;
        _declaredTypeParameters = typeParameters;
        _escapedGlobalName = escapedGlobalName;
        TypeParameters = escapedTypeParameters ?? typeParameters;
    }

    /// <summary>Whether the definition declares type parameters, including those of a generic containing type.</summary>
    internal bool IsGeneric => TypeParameters.Length != 0;

    /// <summary>The number of type parameters the type itself declares.</summary>
    internal int OwnArity
    {
        get
        {
            int arity = TypeParameters.Length;
            foreach (int containing in ContainingArities)
                arity -= containing;
            return arity;
        }
    }

    /// <summary>The CLR metadata name, including the arity suffixes and nested-type separators.</summary>
    internal string MetadataName
    {
        get
        {
            var text = new StringBuilder(64);
            if (Namespace != null)
                text.Append(Namespace).Append('.');
            for (int index = 0; index < ContainingTypes.Length; index++)
            {
                text.Append(ContainingTypes[index]);
                if (ContainingArities[index] != 0)
                    text.Append('`').Append(ContainingArities[index].ToString(System.Globalization.CultureInfo.InvariantCulture));
                text.Append('+');
            }
            text.Append(Name);
            int own = OwnArity;
            if (own != 0)
                text.Append('`').Append(own.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return text.ToString();
        }
    }

    /// <summary>The original identity spelling, such as <c>N.Outer&lt;T&gt;.Inner&lt;U&gt;</c>, used by the Union slot contract.</summary>
    internal string DisplayName
    {
        get
        {
            var text = new StringBuilder(64);
            if (Namespace != null)
                text.Append(Namespace).Append('.');
            int parameter = 0;
            for (int index = 0; index < ContainingTypes.Length; index++)
            {
                text.Append(ContainingTypes[index]);
                AppendParameters(text, ref parameter, ContainingArities[index]);
                text.Append('.');
            }
            text.Append(Name);
            AppendParameters(text, ref parameter, TypeParameters.Length - parameter);
            return text.ToString();
        }
    }

    /// <summary>Appends the next <paramref name="count"/> parameter names as a type argument list.</summary>
    /// <param name="text">The text supplied to <c>AppendParameters</c>.</param>
    /// <param name="parameter">The parameter supplied to <c>AppendParameters</c>.</param>
    /// <param name="count">The number of items to process.</param>
    private void AppendParameters(StringBuilder text, ref int parameter, int count)
    {
        if (count == 0)
            return;
        text.Append('<');
        for (int index = 0; index < count; index++)
        {
            if (index != 0)
                text.Append(", ");
            text.Append(_declaredTypeParameters[parameter++]);
        }
        text.Append('>');
    }

    /// <summary>The fully qualified C# spelling.</summary>
    internal string GlobalName => _escapedGlobalName ?? "global::" + DisplayName;

    /// <inheritdoc/>
    public bool Equals(TypeIdentity? other) => ReferenceEquals(this, other) || other != null && Namespace == other.Namespace &&
        Name == other.Name && _escapedGlobalName == other._escapedGlobalName &&
        ContainingTypes.SequenceEqual(other.ContainingTypes) && ContainingArities.SequenceEqual(other.ContainingArities) &&
        TypeParameters.SequenceEqual(other.TypeParameters);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as TypeIdentity);

    /// <inheritdoc/>
    public override int GetHashCode() => MetadataName.GetHashCode();
}
