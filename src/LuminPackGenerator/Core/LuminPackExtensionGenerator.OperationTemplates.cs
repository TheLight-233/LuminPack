using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Builds one type's operation templates before concrete entry-point emission.</summary>
    /// <param name="type">The closed formatter type.</param>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <returns>Only this type's operations and the layout declarations required to bind them.</returns>
    internal static string GenerateOperationTemplates(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        var body = new StringBuilder();
        string name = FormatterTypeName.Get(type);
        // Only valid non-Union packable roots declare a JSON helper. Built-in and Union
        // operations already have their final container shape and need no helper projection.
        bool projectJsonHelper = false;
        if (type is INamedTypeSymbol named && CompilationTypeAnalysisCache.GetOrCreate(compilation).IsPackable(named))
        {
            // Preserve the original generic definition's wire decisions. Analyzing a closed
            // construction can classify a formerly generic field as unmanaged and change framing.
            var data = LuminPackSourceGenerator.GetSerializationMetadata(named.OriginalDefinition, compilation, metadata);
            using var unmanagedContract = named.IsGenericType && !SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition)
                ? new UnmanagedContractScope(named, compilation) : null;
            name = FormatterTypeName.Get(named.OriginalDefinition);
            // Invalid models keep their original formatter boundary because their helper is absent.
            projectJsonHelper = !data.isUnion && !data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            if (data.isUnion && data.isValueType) LuminPackUnionCodeGenerator.GenerateCaseMapStruct(data, body);
            GenerateRootBinaryExtensions(body, data, name, metadata);
            GenerateRootJsonExtensions(body, data, name, metadata);
            var registry = new LuminPackLocalLayoutGenerator.LocalLayoutRegistry();
            LuminPackLocalLayoutGenerator.AppendRootLocalClass(body, registry, data);
            LuminPackLocalLayoutGenerator.AppendNestedLocalClasses(body, registry, data.fields);
            LuminPackLocalLayoutGenerator.AppendLocalMemberAccessors(body, registry, data.fields);
        }
        else
        {
            GenerateFormatterExtensions(body, CreateOperationField(type, compilation, metadata), metadata, new HashSet<string>(StringComparer.Ordinal));
        }
        PortableStructEmitter.AppendViews(body, type);
        string source = GenerateExtension(body, GetExtensionClassName(compilation));
        bool ownsExternalHelper = type is INamedTypeSymbol external &&
            CompilationTypeAnalysisCache.GetOrCreate(compilation).IsAssemblyPackable(external) &&
            !SymbolEqualityComparer.Default.Equals(external.ContainingAssembly, compilation.Assembly);
        return projectJsonHelper ? JsonOperationTemplates.Prepare(source, metadata, ownsExternalHelper) : source;
    }

    /// <summary>Attaches canonical cursor dispatch and dictionary reuse facts to a built-in formatter operation.</summary>
    /// <param name="type">The exact operation type.</param>
    /// <param name="compilation">The current consuming compilation.</param>
    /// <param name="metadata">Complete current emission settings.</param>
    /// <returns>Field metadata with no new serialization contract analysis in the collection emitter.</returns>
    private static LuminLocalFieldData CreateOperationField(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        var field = new LuminLocalFieldData { TypeName = FormatterTypeName.Get(type), TypeSymbol = type, Name = "value", IsValue = type.IsValueType };
        if (type is INamedTypeSymbol { MetadataName: "Dictionary`2", TypeArguments.Length: 2 } dictionary &&
            dictionary.ContainingNamespace.ToDisplayString() == "System.Collections.Generic")
        {
            field.UnionCursorSuffix = FixedUnionCursorPlan.GetRoot(dictionary.TypeArguments[1], compilation, metadata)?.Suffix;
            field.CanReuseDictionarySlots = dictionary.TypeArguments[0].SpecialType == SpecialType.System_Int32 &&
                DictionarySlotReusePlan.CanReadExistingValue(dictionary.TypeArguments[1], compilation, metadata);
        }
        return field;
    }
}
