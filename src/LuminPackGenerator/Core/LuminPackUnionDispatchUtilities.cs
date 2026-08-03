using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

internal static class LuminPackUnionDispatchUtilities
{
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

    public static bool IsCurrentCompilation(INamedTypeSymbol type, Compilation compilation) =>
        SymbolEqualityComparer.Default.Equals(type.OriginalDefinition.ContainingAssembly, compilation.Assembly);
}
