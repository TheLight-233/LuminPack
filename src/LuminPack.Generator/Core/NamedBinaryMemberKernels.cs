using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Shares ordinary member writes without replacing their framing or fixed-storage contract.</summary>
internal static class NamedBinaryMemberKernels
{
    /// <summary>Identifies a source-owned positional class with a complete public leaf-storage contract.</summary>
    /// <param name="type">The declared payload type.</param>
    /// <param name="fields">The fields selected by the existing object analysis.</param>
    /// <returns>Whether one named member writer can serve its existing inline occurrences.</returns>
    private static bool Supports(INamedTypeSymbol type, IReadOnlyCollection<LuminDataField> fields)
    {
        if (type == null || type.TypeKind != TypeKind.Class || type.IsAbstract || type.IsGenericType ||
            LuminPackExtensionGenerator.ContainsTypeParameter(type) || type.DeclaringSyntaxReferences.Length == 0 ||
            type.BaseType?.SpecialType != SpecialType.System_Object || !TypeMetaChecker.TryCheckPackableAttribute(type) ||
            TypeMetaChecker.TryCheckUnionAttribute(type) || TypeMetaChecker.CheckGeneratorType(type) != GeneratorType.Object ||
            type.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "LuminPackBoundedAttribute") ||
            type.GetMembers().OfType<IMethodSymbol>().Any(method => method.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.ContainingNamespace.ToDisplayString() == "LuminPack.Attribute" &&
                attribute.AttributeClass.Name is "LuminPackOnSerializingAttribute" or "LuminPackOnSerializedAttribute" or
                    "LuminPackOnDeserializingAttribute" or "LuminPackOnDeserializedAttribute")))
            return false;
        return fields.Any(field => field.Type == LuminFiledType.String) && fields.All(field => !field.IsPrivate && !field.isProperty && !field.IsVolatile &&
            !field.IsCompress && (field.Type == LuminFiledType.String || LuminPackCodeGenerator.IsMergeableField(field)));
    }

    /// <summary>Selects the canonical declaration that owns a named member writer.</summary>
    /// <param name="data">The existing complete type contract.</param>
    /// <returns>Whether the declaration publishes the additional static operation.</returns>
    internal static bool CanGenerate(LuminDataInfo data)
        => !data.isUnion && !data.isValueType && !data.isGeneric && data.generatorType == GeneratorType.Object &&
           data.callBackMethods.Count == 0 && !data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) &&
           Supports(data.TypeSymbol, data.fields);

    /// <summary>Selects only member occurrences whose original inline storage matches the shared operation.</summary>
    /// <param name="field">The member's complete inline object contract.</param>
    /// <returns>Whether its concrete member writer is available in this source compilation.</returns>
    internal static bool CanCall(LuminDataField field)
        => field.Type == LuminFiledType.Class && Supports(field.TypeSymbol as INamedTypeSymbol, field.ClassFields);

    /// <summary>Accepts complete root writes already expressed as fixed runtime kernels and named member operations.</summary>
    /// <param name="type">The concrete source payload.</param>
    /// <param name="compilation">The consumer whose existing type facts own the wire contract.</param>
    /// <param name="metadata">The target's language and runtime capabilities.</param>
    /// <returns>Whether the original emitted write can bypass semantic operation expansion.</returns>
    internal static bool CanLowerDirectly(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        if (type is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class || named.IsAbstract || named.IsGenericType ||
            LuminPackExtensionGenerator.ContainsTypeParameter(named) || named.DeclaringSyntaxReferences.Length == 0 ||
            named.BaseType?.SpecialType != SpecialType.System_Object || !TypeMetaChecker.TryCheckPackableAttribute(named))
            return false;
        var data = LuminPackSourceGenerator.GetSerializationMetadata(named, compilation, metadata);
        return !data.isUnion && !data.isValueType && !data.isGeneric && data.generatorType == GeneratorType.Object &&
            data.callBackMethods.Count == 0 && !data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) &&
            !named.GetAttributes().Any(attribute => attribute.AttributeClass?.Name == "LuminPackBoundedAttribute") &&
            data.fields.All(field => !field.IsPrivate && !field.isProperty && !field.IsVolatile && !field.IsCompress &&
                (LuminPackCodeGenerator.CanLowerReservedWrite(field) || CanCall(field) && field.Options?.IsDeclared != true));
    }
}
