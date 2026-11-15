using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Discovers case types of C# Union structs before compiler lowering.</summary>
internal static class CompilerUnion
{
    private const string AttributeName = "System.Runtime.CompilerServices.UnionAttribute";

    /// <summary>Recognizes keyword declarations and explicit compiler Union structs.</summary>
    /// <param name="type">The candidate type.</param>
    /// <param name="compilation">The compilation binding the compiler Union attribute.</param>
    /// <returns>Whether the type declares the compiler Union contract.</returns>
    internal static bool IsUnion(INamedTypeSymbol type, Compilation compilation) =>
        type.TypeKind == TypeKind.Struct &&
        compilation.GetTypeByMetadataName(AttributeName) is { } marker &&
        (type.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, marker)) ||
         Declaration(type) != null);

    /// <summary>Collects distinct cases in declaration order and substitutes closed type arguments.</summary>
    /// <param name="type">The Union struct whose case types are needed.</param>
    /// <param name="compilation">The compilation that binds case syntax.</param>
    /// <returns>The case types in binary and JSON tag order.</returns>
    internal static ITypeSymbol[] Cases(INamedTypeSymbol type, Compilation compilation)
    {
        var cases = new List<ITypeSymbol>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        if (Declaration(type) is { } declaration &&
            declaration.ChildNodes().OfType<ParameterListSyntax>().FirstOrDefault() is { } parameters)
        {
            SemanticModel model = BindingSemanticModel.For(compilation, declaration.SyntaxTree);
            foreach (ParameterSyntax parameter in parameters.Parameters)
                if (parameter.Type is { } syntax && model.GetTypeInfo(syntax).Type is { } caseType && seen.Add(caseType))
                    cases.Add(Substitute(caseType, type, compilation));
        }
        if (cases.Count == 0)
            foreach (IMethodSymbol constructor in type.InstanceConstructors)
                if (constructor.DeclaredAccessibility == Accessibility.Public && constructor.Parameters.Length == 1 &&
                    seen.Add(constructor.OriginalDefinition.Parameters[0].Type))
                    cases.Add(constructor.Parameters[0].Type);
        return cases.ToArray();
    }

    /// <summary>Finds keyword syntax using APIs shared with older Roslyn hosts.</summary>
    /// <param name="type">The Union type symbol.</param>
    /// <returns>The keyword declaration, or null for a manually declared Union.</returns>
    private static TypeDeclarationSyntax? Declaration(INamedTypeSymbol type)
    {
        foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
            if (reference.GetSyntax() is TypeDeclarationSyntax declaration &&
                (declaration.Keyword.Text == "union" || declaration.Modifiers.Any(modifier => modifier.Text == "union")))
                return declaration;
        return null;
    }

    /// <summary>Rebinds a case declared by an open Union to its closed type arguments.</summary>
    /// <param name="caseType">The type appearing in the case declaration.</param>
    /// <param name="owner">The constructed Union or containing type.</param>
    /// <param name="compilation">The compilation used to recreate array symbols.</param>
    /// <returns>The case type in the context of the constructed Union.</returns>
    private static ITypeSymbol Substitute(ITypeSymbol caseType, INamedTypeSymbol owner, Compilation compilation)
    {
        if (caseType is ITypeParameterSymbol parameter)
        {
            for (INamedTypeSymbol? current = owner; current != null; current = current.ContainingType)
                if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, parameter.ContainingSymbol))
                    return current.TypeArguments[parameter.Ordinal];
        }
        if (caseType is IArrayTypeSymbol array)
            return compilation.CreateArrayTypeSymbol(Substitute(array.ElementType, owner, compilation), array.Rank);
        if (caseType is INamedTypeSymbol named)
        {
            INamedTypeSymbol definition = named.ConstructedFrom;
            if (named.ContainingType is { } containing &&
                Substitute(containing, owner, compilation) is INamedTypeSymbol closed &&
                !SymbolEqualityComparer.Default.Equals(closed, containing))
                definition = closed.GetTypeMembers(named.Name, named.Arity).FirstOrDefault(candidate =>
                    SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, named.OriginalDefinition)) ?? definition;
            return named.Arity == 0 ? definition :
                definition.Construct(named.TypeArguments.Select(argument => Substitute(argument, owner, compilation)).ToArray());
        }
        return caseType;
    }
}
