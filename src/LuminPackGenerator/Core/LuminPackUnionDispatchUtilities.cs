using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Provides lumin pack union dispatch utilities functionality.</summary>
internal static class LuminPackUnionDispatchUtilities
{
    /// <summary>Performs the can generate partial step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool CanGeneratePartial(INamedTypeSymbol type, Compilation compilation)
    {
        var definition = type.OriginalDefinition;
        if (!IsCurrentCompilation(definition, compilation))
            return false;

        for (var current = definition; current != null; current = current.ContainingType)
        {
            if (current.DeclaringSyntaxReferences.Length == 0)
                return false;

            foreach (var syntaxReference in current.DeclaringSyntaxReferences)
            {
                if (syntaxReference.GetSyntax() is not TypeDeclarationSyntax declaration ||
                    !declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Determines whether the supplied value has the current compilation classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsCurrentCompilation(INamedTypeSymbol type, Compilation compilation) =>
        SymbolEqualityComparer.Default.Equals(type.OriginalDefinition.ContainingAssembly, compilation.Assembly);
}
