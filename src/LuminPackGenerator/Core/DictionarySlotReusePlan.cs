using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Certifies callback-free leaf reads before a dictionary emitter retains an entry reference.</summary>
internal static class DictionarySlotReusePlan
{
    /// <summary>Checks the canonical field contract without changing its framing or storage views.</summary>
    /// <param name="type">The exact dictionary value type.</param>
    /// <param name="compilation">The compilation owning all analyzed symbols.</param>
    /// <param name="metadata">The current language and emission settings.</param>
    /// <returns>Whether reading an existing nonnull value cannot execute user code or resize its dictionary.</returns>
    internal static bool CanReadExistingValue(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        if (!metadata.IsNet8 || metadata.IsForUnity ||
            metadata.ParseOptions.PreprocessorSymbolNames.Any(symbol => symbol.StartsWith("UNITY", System.StringComparison.Ordinal)) ||
            type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: true, IsGenericType: false } named ||
            named.BaseType?.SpecialType != SpecialType.System_Object ||
            !SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly) ||
            !CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named)) return false;
        var data = LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata);
        return !data.isUnion && data.generatorType is GeneratorType.Object or GeneratorType.VersionTolerant &&
            data.fields.Count != 0 && data.callBackMethods.Count == 0 &&
            !data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) &&
            data.fields.All(field => !field.isProperty && !field.IsVolatile && !field.IsCompress &&
                field.TypeSymbol.SpecialType != SpecialType.None &&
                (field.TypeSymbol.SpecialType == SpecialType.System_String ||
                 LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type)));
    }
}
