using System.Linq;
using Microsoft.CodeAnalysis;
namespace LuminPack.SourceGenerator.CodeEmitters;
/// <summary>Checks the complete optional JSON sizing API on the referenced runtime.</summary>
internal static class JsonCapacityEmitter
{
    /// <summary>Requires the ordinary and compiled-profile forms before emitting capacity hints.</summary>
    /// <param name="compilation">The current consumer compilation.</param><returns>Whether all signatures are compatible.</returns>
    internal static bool Available(Compilation compilation)
    {
        var reader = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader");
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        if (reader is null || options is null)
            return false;
        foreach (string name in new[] { "GetArrayLengthHint", "GetArrayCapacityHint", "GetEntryArrayCapacityHint" })
        for (int arity = 0; arity <= 1; arity++)
        {
            int parameterCount = name == "GetArrayCapacityHint" ? 1 : 0;
            if (!reader.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
                !method.IsStatic && method.DeclaredAccessibility == Accessibility.Public &&
                method.Arity == arity && method.RefKind == RefKind.None &&
                method.ReturnType.SpecialType == SpecialType.System_Int32 &&
                method.Parameters.Length == parameterCount &&
                method.Parameters.All(parameter => parameter.RefKind == RefKind.None && parameter.Type.SpecialType == SpecialType.System_Int32) &&
                (arity == 0 || method.TypeParameters[0].HasValueTypeConstraint &&
                    !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                    method.TypeParameters[0].ConstraintTypes.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options))))
                return false;
        }
        return true;
    }
}
