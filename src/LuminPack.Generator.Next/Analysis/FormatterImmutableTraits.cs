using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Shares optional exclusive builder rentals without changing insertion or publication order.</summary>
internal static class FormatterImmutableTraits
{
    /// <summary>Checks both exact methods needed to rent and return the existing result family's builder.</summary>
    /// <param name = "compilation">The consumer's actual runtime and framework metadata.</param>
    /// <param name = "family">The concrete factory name without its Immutable prefix.</param>
    /// <param name = "arity">The number of element contracts.</param>
    /// <returns>Whether both matching ownership operations can be emitted.</returns>
    internal static bool Available(Compilation compilation, string family, int arity)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminImmutableBuilders");
        var collection = compilation.GetTypeByMetadataName("System.Collections.Immutable.Immutable" + family + "`" + arity);
        if (access is null || access.DeclaredAccessibility != Accessibility.Public || collection is null)
            return false;
        bool Matches(IMethodSymbol method, bool rent)
        {
            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Arity != arity || method.ReturnsByRef || method.ReturnsByRefReadonly || method.TypeParameters.Any(parameter => !parameter.ConstraintTypes.IsEmpty || parameter.HasReferenceTypeConstraint || parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint || parameter.HasConstructorConstraint || (parameter.HasNotNullConstraint && !(arity == 2 && parameter.Ordinal == 0))))
                return false;
            var builder = collection.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray()).GetTypeMembers("Builder").SingleOrDefault();
            return builder is not null && (rent ? method.Parameters.IsEmpty && SymbolEqualityComparer.Default.Equals(method.ReturnType, builder) : method.ReturnsVoid && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, builder));
        }

        return access.GetMembers("Rent" + family).OfType<IMethodSymbol>().Any(method => Matches(method, true)) && access.GetMembers("Return" + family).OfType<IMethodSymbol>().Any(method => Matches(method, false));
    }
}
