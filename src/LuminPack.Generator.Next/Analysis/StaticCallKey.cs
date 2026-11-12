using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Spells the build-time mapping that replaces a facade call when compiler interception is unavailable.</summary>
/// <remarks>The installed IL rewriter (<c>LuminPack.Build.StaticCallRewriter</c>) matches each compiled call against this key: the
/// declaring type, metadata name, arity, definition parameters and return type, followed by the call's type arguments with the
/// forwarded profile as a wildcard. The spelling is the ECMA signature shape the rewriter derives from metadata, as the original
/// generator's <c>StaticSerializerBinding.Il</c> produced it.</remarks>
internal static class StaticCallKey
{
    /// <summary>Builds the mapping attribute of one bound facade call.</summary>
    /// <param name="method">The constructed facade method the call binds.</param>
    /// <param name="profileArgument">The forwarded profile's type-argument index, or minus one for a closed call.</param>
    /// <returns>The complete attribute text.</returns>
    internal static string Attribute(IMethodSymbol method, int profileArgument)
    {
        IMethodSymbol definition = method.OriginalDefinition;
        var text = new StringBuilder(256);
        IlType(text, method.ContainingType);
        text.Append("::").Append(method.MetadataName).Append('`').Append(method.Arity).Append('(');
        for (int index = 0; index < definition.Parameters.Length; index++)
        {
            if (index != 0)
                text.Append(',');
            IlType(text, definition.Parameters[index].Type);
            if (definition.Parameters[index].RefKind != RefKind.None)
                text.Append('&');
        }
        text.Append(")->");
        IlType(text, definition.ReturnType);
        text.Append('|');
        for (int index = 0; index < method.TypeArguments.Length; index++)
        {
            if (index != 0)
                text.Append(',');
            if (index == profileArgument)
                text.Append('*');
            else
                IlType(text, method.TypeArguments[index]);
        }
        return "[global::LuminPack.Generated.StaticCall(" + SymbolDisplay.FormatLiteral(text.ToString(), true) + ", " +
            profileArgument.ToString(System.Globalization.CultureInfo.InvariantCulture) + ")]";
    }

    /// <summary>Appends the metadata shape of a type without C# aliases or nullable annotations.</summary>
    /// <param name="text">The key being built.</param>
    /// <param name="type">A definition or constructed signature type.</param>
    private static void IlType(StringBuilder text, ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                IlType(text, array.ElementType);
                text.Append('[').Append(',', array.Rank - 1).Append(']');
                return;
            case IPointerTypeSymbol pointer:
                IlType(text, pointer.PointedAtType);
                text.Append('*');
                return;
            case ITypeParameterSymbol parameter:
            {
                int offset = 0;
                if (parameter.TypeParameterKind == TypeParameterKind.Type)
                    for (INamedTypeSymbol? owner = parameter.ContainingType?.ContainingType; owner != null; owner = owner.ContainingType)
                        offset += owner.Arity;
                text.Append(parameter.TypeParameterKind == TypeParameterKind.Method ? "!!" : "!").Append(parameter.Ordinal + offset);
                return;
            }
        }
        if (type.TypeKind == TypeKind.Dynamic)
        {
            text.Append("System.Object");
            return;
        }
        var named = (INamedTypeSymbol)type;
        if (named.IsTupleType && named.TupleUnderlyingType is { } underlying)
            named = underlying;
        // Type arguments of containing types precede the type's own, as metadata flattens them.
        var arguments = new List<ITypeSymbol>(named.TypeArguments);
        string name = named.MetadataName;
        for (INamedTypeSymbol? owner = named.ContainingType; owner != null; owner = owner.ContainingType)
        {
            name = owner.MetadataName + "/" + name;
            arguments.InsertRange(0, owner.TypeArguments);
        }
        if (!named.ContainingNamespace.IsGlobalNamespace)
            text.Append(named.ContainingNamespace.ToDisplayString()).Append('.');
        text.Append(name);
        if (arguments.Count == 0)
            return;
        text.Append('<');
        for (int index = 0; index < arguments.Count; index++)
        {
            if (index != 0)
                text.Append(',');
            IlType(text, arguments[index]);
        }
        text.Append('>');
    }
}
