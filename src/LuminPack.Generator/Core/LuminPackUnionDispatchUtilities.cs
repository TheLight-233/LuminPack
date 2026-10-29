using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Provides lumin pack union dispatch utilities functionality.</summary>
internal static class LuminPackUnionDispatchUtilities
{
    /// <summary>Checks whether a referenced union exposes the generated virtual dispatch contract.</summary>
    /// <param name="type">The referenced union root.</param>
    /// <param name="compilation">The consuming compilation.</param>
    /// <returns>Whether both generated slots can be called from this assembly.</returns>
    internal static bool CanUseReferencedDispatch(INamedTypeSymbol type, Compilation compilation)
    {
        if (IsCurrentCompilation(type, compilation)) return false;
        var suffix = LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type.OriginalDefinition);
        foreach (var operation in new[] { "Serialize", "SerializeJson" })
        {
            string state = operation == "Serialize" ? "Writer" : "JsonWriter";
            if (!type.GetMembers("__LuminPackUnion" + operation + "_" + suffix).OfType<IMethodSymbol>()
                .Any(method => !method.IsStatic && method.ReturnsVoid && method.Parameters.Length == 1 &&
                    (method.IsVirtual || method.IsAbstract || method.IsOverride) &&
                    method.Parameters[0].RefKind == RefKind.Ref &&
                    method.Parameters[0].Type.ToDisplayString() == "LuminPack.Core.LuminPack" + state &&
                    compilation.IsSymbolAccessibleWithin(method, compilation.Assembly)))
                return false;
        }
        return true;
    }

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
