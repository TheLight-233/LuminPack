using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
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
        // A type whose analysis produced an error emits no formatter extension, so its JSON
        // operation cannot reference the absent per-type helper.  Such types keep the original
        // direct formatter call boundary instead of a JSON expansion.
        bool jsonExpandable = true;
        if (type is INamedTypeSymbol named && HasPackableAttribute(named))
        {
            // Preserve the original generic definition's wire decisions. Analyzing a closed
            // construction can classify a formerly generic field as unmanaged and change framing.
            var data = LuminPackSourceGenerator.GetSerializationMetadata(named.OriginalDefinition, compilation, metadata);
            name = FormatterTypeName.Get(named.OriginalDefinition);
            jsonExpandable = !data.Diagnostics.Any(diagnostic => diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
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
            GenerateFormatterExtensions(body, new LuminLocalFieldData
            {
                TypeName = name, TypeSymbol = type, Name = "value", IsValue = type.IsValueType
            }, metadata, new HashSet<string>(StringComparer.Ordinal));
        }
        string source = GenerateExtension(body, GetExtensionClassName(compilation));
        return jsonExpandable ? JsonOperationTemplates.Prepare(source, metadata) : source;
    }
}
