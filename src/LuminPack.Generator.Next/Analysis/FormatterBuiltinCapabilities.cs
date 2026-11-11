using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Preserves the runtime capability checks of specialized framework formatters.</summary>
internal static class FormatterBuiltinCapabilities
{
    /// <summary>Captures optional built-in operations from the exact consumer metadata.</summary>
    /// <param name = "compilation">The consuming compilation and its runtime references.</param>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <param name = "result">The result used by this operation.</param>
    internal static void Capture(Compilation compilation, ITypeSymbol type, List<string> result)
    {
        string display = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (display == "global::System.TimeZoneInfo")
        {
            var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
            var methods = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?.GetMembers("ReadTimeZoneInfo").OfType<IMethodSymbol>().Where(method => !method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly && SymbolEqualityComparer.Default.Equals(method.ReturnType, type) && (method.Arity == 0 || method.Arity == 1 && method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.Length == 1 && SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options))).ToArray();
            if (methods != null && methods.Any(method => method.Arity == 0) && methods.Any(method => method.Arity == 1))
                result.Add("CanReadJsonTimeZoneInfo");
        }

        if (display == "global::System.Numerics.BigInteger")
        {
            var bytes = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")?.Construct(compilation.GetSpecialType(SpecialType.System_Byte));
            if (compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal")?.GetMembers("ReadBigInteger").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.ReturnsVoid && method.Arity == 0 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, bytes) && method.Parameters[1].RefKind == RefKind.Out && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, type)) == true)
                result.Add("CanReadBinaryBigInteger");
            if (compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?.GetMembers("WriteBigInteger").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true)
                result.Add("CanWriteJsonBigInteger");
            if (compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?.GetMembers("ReadBigInteger").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 0 && SymbolEqualityComparer.Default.Equals(method.ReturnType, type)) == true)
                result.Add("CanReadJsonBigInteger");
        }

        if (display == "global::System.Globalization.CultureInfo")
        {
            var cache = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCultureInfoCache");
            if (cache?.DeclaredAccessibility == Accessibility.Public && cache.GetMembers("Name").OfType<IPropertySymbol>().Any(property => property.IsStatic && property.GetMethod?.DeclaredAccessibility == Accessibility.Public && property.Type.SpecialType == SpecialType.System_String) && cache.GetMembers("ResolveCulture").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.Arity == 0 && method.DeclaredAccessibility == Accessibility.Public && SymbolEqualityComparer.Default.Equals(method.ReturnType, type) && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && method.Parameters[0].Type.SpecialType == SpecialType.System_String))
                result.Add("CanReuseCultureInfo");
        }

        if (display == "global::System.Version")
        {
            if (compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?.GetMembers("WriteVersion").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true)
                result.Add("CanWriteJsonVersion");
            if (compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?.GetMembers("ReadVersion").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 0 && SymbolEqualityComparer.Default.Equals(method.ReturnType, type)) == true)
                result.Add("CanReadJsonVersion");
        }

        if (display == "global::System.Text.StringBuilder" && type is INamedTypeSymbol builder)
        {
            if (compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonReader")?.GetMembers("ReadStringBuilder").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true)
                result.Add("CanReadJsonStringBuilder");
            if (compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter")?.GetMembers("WriteStringBuilder").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.ReturnsVoid && method.Arity == 1 && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, type)) == true)
                result.Add("CanWriteJsonStringBuilder");
            if (builder.GetMembers("CopyTo").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.ReturnsVoid && method.Arity == 0 && method.Parameters.Length == 3 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[1].Type is INamedTypeSymbol { MetadataName: "Span`1", TypeArguments.Length: 1 } span && span.ContainingNamespace.ToDisplayString() == "System" && span.TypeArguments[0].SpecialType == SpecialType.System_Char))
                result.Add("CanCopyStringBuilder");
        }

        if (display == "global::System.Collections.BitArray")
        {
            if (FormatterRuntimeTraits.CanWriteBitArrayPayload(compilation))
                result.Add("CanWriteBitArrayPayload");
            if (FormatterRuntimeTraits.CanCreateBitArrayPayload(compilation))
                result.Add("CanCreateBitArrayPayload");
        }
    }
}
