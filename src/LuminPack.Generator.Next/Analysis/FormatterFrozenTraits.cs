using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Selects compatible mutable construction rentals and scalar pair output.</summary>
internal static class FormatterFrozenTraits
{
    /// <summary>Checks both exact methods needed to rent and return the existing result family's builder.</summary>
    /// <param name = "compilation">The consumer's actual runtime and framework metadata.</param>
    /// <param name = "family">The mutable Dictionary or Set construction family.</param>
    /// <param name = "arity">The number of element contracts.</param>
    /// <returns>Whether both matching ownership operations can be emitted.</returns>
    internal static bool Available(Compilation compilation, string family, int arity)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminFrozenConstruction");
        var collection = compilation.GetTypeByMetadataName("System.Collections.Generic." + (family == "Set" ? "HashSet" : family) + "`" + arity);
        if (access is null || access.DeclaredAccessibility != Accessibility.Public || !access.IsStatic || collection is null)
            return false;
        bool Matches(IMethodSymbol method, bool rent)
        {
            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Arity != arity || method.ReturnsByRef || method.ReturnsByRefReadonly || method.TypeParameters.Any(parameter => !parameter.ConstraintTypes.IsEmpty || parameter.HasReferenceTypeConstraint || parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint || parameter.HasConstructorConstraint || (parameter.HasNotNullConstraint && !(arity == 2 && parameter.Ordinal == 0))))
                return false;
            var builder = collection.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray());
            return builder is not null && (rent ? method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32 && SymbolEqualityComparer.Default.Equals(method.ReturnType, builder) : method.ReturnsVoid && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, builder));
        }

        return access.GetMembers("Rent" + family).OfType<IMethodSymbol>().Any(method => Matches(method, true)) && access.GetMembers("Return" + family).OfType<IMethodSymbol>().Any(method => Matches(method, false));
    }

    /// <summary>Checks the complete raw pair helper against the actual consumer metadata.</summary>
    /// <param name = "compilation">The consumer compilation.</param>
    /// <returns>Whether two scalar spans can use the complete fixed writer contract.</returns>
    internal static bool CanWritePairs(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminFrozenConstruction");
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        if (owner is null || owner.DeclaredAccessibility != Accessibility.Public || !owner.IsStatic || span is null || writer is null)
            return false;
        return owner.GetMembers("WritePairs").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 2 && method.ReturnsVoid && method.TypeParameters.All(parameter => parameter.HasUnmanagedTypeConstraint && parameter.ConstraintTypes.IsEmpty) && method.Parameters.Length == 3 && method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) && method.Parameters[1].RefKind == RefKind.None && method.Parameters[2].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, span.Construct(method.TypeParameters[0])) && SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, span.Construct(method.TypeParameters[1])));
    }
}
