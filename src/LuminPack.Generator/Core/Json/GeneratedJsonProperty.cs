using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator;

/// <summary>Shares the generated property head spelling and runtime availability check.</summary>
internal static class GeneratedJsonProperty
{
    /// <summary>Checks whether the selected Core supplies the bounded property-head write.</summary>
    /// <param name="compilation">The consumer compilation.</param>
    /// <returns>Whether a compatible entry is visible to generated code.</returns>
    internal static bool Available(Compilation compilation)
    {
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter");
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        if (writer == null || span == null)
            return false;
        foreach (var member in writer.GetMembers("TryWriteGeneratedPropertyName"))
        {
            if (member is IMethodSymbol { IsStatic: false, Arity: 1, DeclaredAccessibility: Accessibility.Public } method &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, span.Construct(compilation.GetSpecialType(SpecialType.System_Byte))) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, span.Construct(compilation.GetSpecialType(SpecialType.System_Char))) &&
                method.Parameters[0].RefKind == RefKind.None && method.Parameters[1].RefKind == RefKind.None)
                return true;
        }
        return false;
    }

    /// <summary>Spells the two encodings of the complete generated property head.</summary>
    /// <param name="name">The property name emitted by the existing reference-tracked writer.</param>
    /// <returns>The arguments for the bounded write.</returns>
    internal static string Arguments(string name)
    {
        string literal = SymbolDisplay.FormatLiteral(",\"" + name + "\":", true);
        return literal + "u8, global::System.MemoryExtensions.AsSpan(" + literal + ")";
    }
}
