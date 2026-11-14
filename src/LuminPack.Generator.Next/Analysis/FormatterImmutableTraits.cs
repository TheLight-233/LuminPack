using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Checks optional immutable storage operations in the consumer's runtime.</summary>
internal static class FormatterImmutableTraits
{
    /// <summary>Checks the runtime's complete immutable-list raw writer.</summary>
    /// <param name="compilation">The consumer's referenced runtime and framework metadata.</param>
    /// <returns>Whether the generated list root can call the direct output operation.</returns>
    internal static bool HasRawListWriter(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        var list = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList`1");
        return writer != null && list != null && access?.DeclaredAccessibility == Accessibility.Public &&
            access.GetMembers("WriteRawList").OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.ReturnsVoid && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 1 &&
            method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
            method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.Ref && method.Parameters[1].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, list.Construct(method.TypeParameters[0])));
    }

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
