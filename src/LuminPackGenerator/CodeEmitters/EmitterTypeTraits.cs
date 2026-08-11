using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

internal static class EmitterTypeTraits
{
    /// <summary>
    /// Resolves the unmanaged decision when Roslyn can prove it at generation time.
    /// An unmanaged-constrained type parameter is already definitive; other open
    /// types must keep the RuntimeHelpers fallback for each closed instantiation.
    /// </summary>
    public static bool TryGetIsUnmanaged(ITypeSymbol type, out bool isUnmanaged)
    {
        if (type?.IsUnmanagedType == true)
        {
            isUnmanaged = true;
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

    public static ITypeSymbol GetFirstTypeArgument(LuminLocalFieldData fieldData)
    {
        return GetCollectionElementType(fieldData.TypeSymbol);
    }

    public static ITypeSymbol GetCollectionElementType(ITypeSymbol collectionType)
    {
        return collectionType switch
        {
            IArrayTypeSymbol array => array.ElementType,
            INamedTypeSymbol { TypeArguments.Length: > 0 } named => named.TypeArguments[0],
            _ => null
        };
    }

    public static string GetArrayPoolClearExpression(LuminLocalFieldData fieldData, string elementType)
    {
        if (TryGetIsUnmanaged(GetFirstTypeArgument(fieldData), out bool isUnmanaged))
        {
            return isUnmanaged ? "false" : "true";
        }

        return "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" +
               elementType + ">()";
    }

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
}
