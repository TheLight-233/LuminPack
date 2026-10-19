using System.Linq;
using System.Collections.Generic;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides emitter type traits functionality.</summary>
internal static class EmitterTypeTraits
{
    /// <summary>Identifies ordinary reference-object framing whose root null rule must not retain a nested value.</summary>
    /// <param name="type">The canonical formatter type.</param>
    /// <returns>Whether this is the ordinary object formatter, without Custom, Union or built-in framing.</returns>
    internal static bool UsesOrdinaryObjectHeader(ITypeSymbol type)
        => type is INamedTypeSymbol { IsReferenceType: true, TypeKind: TypeKind.Class } named &&
           TypeMetaChecker.TryCheckPackableAttribute(named) &&
           TypeMetaChecker.CheckGeneratorType(named) == GeneratorType.Object &&
           !TypeMetaChecker.TryCheckUnionAttribute(named) &&
           !LuminPackExtensionGenerator.IsCustomGeneratorType(named) &&
           CodeEmitterRegistry.GetEmitter(FormatterTypeName.Get(named)).Read == null;

    /// <summary>Reads a reusable collection value while keeping null elements distinct from the root merge rule.</summary>
    /// <param name="sb">The generated statements.</param>
    /// <param name="type">The canonical element type.</param>
    /// <param name="target">The existing writable element storage.</param>
    /// <param name="indent">Statement indentation.</param>
    /// <param name="compressed">Whether the original call uses the compressed operation.</param>
    internal static void AppendReusableElementRead(System.Text.StringBuilder sb, ITypeSymbol type, string target, string indent, bool compressed = false)
    {
        if (UsesOrdinaryObjectHeader(type))
        {
            sb.AppendLine(indent + "if (reader.PeekIsNullObject(ref reader.GetCurrentSpanOffset())) " + target + " = default!;");
        }
        else if (type is ITypeParameterSymbol { IsValueType: false })
            sb.AppendLine(indent + "if (global::LuminPack.LuminPackSerializer.FormatterDispatch.__LuminPackShouldResetMember<" + type.ToDisplayString() + ">(ref reader)) " + target + " = default!;");
        sb.AppendLine(indent + "reader." + (compressed ? "ReadValueWithCompress" : "ReadValue") + "(ref " + target + "!);");
    }

    /// <summary>
    /// Resolves whether raw serialization can preserve the formatter contract.
    /// Open types retain per-value dispatch because an unmanaged constraint does not exclude Custom.
    /// </summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="isUnmanaged">Receives the is unmanaged produced by the operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool TryGetIsUnmanaged(ITypeSymbol type, out bool isUnmanaged)
    {
        if (type is not null && ContainsUndeterminedTypeParameter(type))
        {
            // Keep a proven unmanaged constraint's original raw path when its actual argument
            // is available and does not carry a Custom contract. Unknown arguments stay conservative.
            isUnmanaged = type.IsUnmanagedType && CanUseUnmanagedSerialization(type);
            return true;
        }
        if (type?.IsUnmanagedType == true)
        {
            isUnmanaged = CanUseUnmanagedSerialization(type);
            return true;
        }

        if (type?.IsReferenceType == true)
        {
            isUnmanaged = false;
            return true;
        }

        if (type is null || ContainsUndeterminedTypeParameter(type))
        {
            isUnmanaged = false;
            return false;
        }

        isUnmanaged = false;
        return true;
    }

    /// <summary>Checks whether a closed unmanaged value has no custom formatter in its storage graph.</summary>
    /// <param name="type">The value type considered for raw copying.</param>
    /// <returns>Whether raw copying preserves every nested formatter boundary.</returns>
    internal static bool CanUseUnmanagedSerialization(ITypeSymbol type)
    {
        if (type?.IsUnmanagedType != true) return false;
        var storage = UnmanagedContractScope.Resolve(type);
        return !ContainsUndeterminedTypeParameter(storage) &&
            PortableStructPlan.TryCreate(storage) == null &&
            !ContainsCustomFormatter(storage, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default));
    }

    /// <summary>Finds custom contracts inside finite unmanaged storage without following reference graphs.</summary>
    /// <param name="type">The current storage type.</param>
    /// <param name="visited">Storage types already inspected.</param>
    /// <returns>Whether the storage contains a custom formatter contract.</returns>
    private static bool ContainsCustomFormatter(ITypeSymbol type, HashSet<ITypeSymbol> visited)
    {
        if (!visited.Add(type)) return false;
        if (LuminPackExtensionGenerator.IsCustomGeneratorType(type)) return true;
        return type is INamedTypeSymbol { TypeKind: TypeKind.Struct } named &&
            (named.TypeArguments.Any(argument => ContainsCustomFormatter(argument, visited)) ||
             named.GetMembers().OfType<IFieldSymbol>().Any(field => !field.IsStatic && ContainsCustomFormatter(field.Type, visited)));
    }

    /// <summary>Obtains the first type argument for the supplied input.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <returns>The result of the operation.</returns>
    public static ITypeSymbol GetFirstTypeArgument(LuminLocalFieldData fieldData)
    {
        return GetCollectionElementType(fieldData.TypeSymbol);
    }

    /// <summary>Obtains the collection element type for the supplied input.</summary>
    /// <param name="collectionType">The collection type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static ITypeSymbol GetCollectionElementType(ITypeSymbol collectionType)
    {
        return collectionType switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: > 0 } named => named.TypeArguments[0],
            _ => null
        };
    }

    /// <summary>Obtains the array pool clear expression for the supplied input.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="elementType">The element type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    public static string GetArrayPoolClearExpression(LuminLocalFieldData fieldData, string elementType)
    {
        var element = GetFirstTypeArgument(fieldData);
        if (element?.IsUnmanagedType == true) return "false";
        if (TryGetIsUnmanaged(GetFirstTypeArgument(fieldData), out bool isUnmanaged))
        {
            return isUnmanaged ? "false" : "true";
        }

        return "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" +
               elementType + ">()";
    }

    /// <summary>Determines whether the metadata contains undetermined type parameter.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool ContainsUndeterminedTypeParameter(ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsUndeterminedTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsUndeterminedTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol named =>
                (named.ContainingType is not null && ContainsUndeterminedTypeParameter(named.ContainingType)) ||
                named.TypeArguments.Any(ContainsUndeterminedTypeParameter),
            _ => false
        };
    }

    /// <summary>Fully qualified name of the bounded-size guarantee attribute.</summary>
    private const string BoundedAttributeName = "LuminPack.Attribute.LuminPackBoundedAttribute";

    /// <summary>Reads the guaranteed maximum serialized length declared by the bounded-size attribute.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="maxBytes">Receives the guaranteed maximum complete serialized length in bytes.</param>
    /// <returns>Whether the type declares a usable bounded-size guarantee.</returns>
    internal static bool TryGetBoundedMaxBytes(ITypeSymbol type, out int maxBytes)
    {
        maxBytes = 0;
        if (type is null) return false;

        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != BoundedAttributeName) continue;
            if (attribute.ConstructorArguments.Length != 1) return false;
            if (attribute.ConstructorArguments[0].Value is not int declared || declared <= 0) return false;
            maxBytes = declared;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Tries to express the serialized upper bound of one value of the supplied type as C# source, using
    /// <c>Unsafe.SizeOf</c> for existing raw values or an explicit complete-record guarantee. Containers,
    /// nested object graphs, Union slots, Custom formatters and tolerant/circle-reference records are not
    /// expressible and therefore fall back to the per-element check.
    /// </summary>
    /// <param name="type">The type whose value is being written.</param>
    /// <param name="boundExpression">Receives a C# expression for the element bound.</param>
    /// <returns>Whether a statically expressible upper bound exists.</returns>
    internal static bool TryGetElementUpperBound(ITypeSymbol type, out string boundExpression)
    {
        boundExpression = null;
        if (type is null) return false;

        var portable = PortableStructPlan.TryCreate(type);
        if (portable != null)
        {
            boundExpression = portable.WireSize.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (TryGetBoundedMaxBytes(type, out int boundedMaxBytes))
        {
            // The guarantee covers the complete record, so a container can reserve count * MaxBytes once.
            boundExpression = boundedMaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }

        if (type.SpecialType == SpecialType.System_String)
        {
            // String bounds require a container-local Length prescan rather than a constant element size.
            return false;
        }

        if (type is INamedTypeSymbol named && named.GetAttributes().Any(attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE) &&
            TypeMetaChecker.CheckGeneratorType(named) != GeneratorType.Object) return false;
        if (type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.UnionAttribute")) return false;
        if (TryGetIsUnmanaged(type, out bool isUnmanaged) && isUnmanaged)
        {
            boundExpression = "global::System.Runtime.CompilerServices.Unsafe.SizeOf<" +
                              FormatterTypeName.Get(type) + ">()";
            return true;
        }

        return false;
    }

}
