using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Emits binary field reads, writes, and size evaluation from analyzed serialization contracts.</summary>
public static partial class LuminPackCodeGenerator
{

    /// <summary>Converts the supplied input to the generics to field type representation.</summary>
    /// <param name="genericType">The generic type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static LuminFiledType ConvertGenericsToFieldType(LuminGenericsType genericType)
    {
        switch (genericType)
        {
            case LuminGenericsType.Byte:
                return LuminFiledType.Byte;
            case LuminGenericsType.SByte:
                return LuminFiledType.SByte;
            case LuminGenericsType.Short:
                return LuminFiledType.Short;
            case LuminGenericsType.UShort:
                return LuminFiledType.UShort;
            case LuminGenericsType.Int:
                return LuminFiledType.Int;
            case LuminGenericsType.UInt:
                return LuminFiledType.UInt;
            case LuminGenericsType.Long:
                return LuminFiledType.Long;
            case LuminGenericsType.ULong:
                return LuminFiledType.ULong;
            case LuminGenericsType.Float:
                return LuminFiledType.Float;
            case LuminGenericsType.Double:
                return LuminFiledType.Double;
            case LuminGenericsType.Decimal:
                return LuminFiledType.Decimal;
            case LuminGenericsType.Char:
                return LuminFiledType.Char;
            case LuminGenericsType.String:
                return LuminFiledType.String;
            case LuminGenericsType.Bool:
                return LuminFiledType.Bool;
            case LuminGenericsType.Class:
                return LuminFiledType.Class;
            case LuminGenericsType.Struct:
                return LuminFiledType.Struct;
            case LuminGenericsType.List:
                return LuminFiledType.List;
            case LuminGenericsType.Array:
                return LuminFiledType.Array;
            case LuminGenericsType.Enum:
                return LuminFiledType.Enum;
            default:
                {
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<LuminFiledType>();
                }
        }
    }

    /// <summary>Converts the supplied input to the generics to field type representation.</summary>
    /// <param name="genericTypes">The generic types used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static LuminFiledType ConvertGenericsToFieldType(List<LuminGenericsType> genericTypes)
    {
        if (genericTypes.Count == 0)
        {
            LuminPackExceptionHelper.ThrowArgumentException("Empty generic types");
        }
        switch (genericTypes[0])
        {
            case LuminGenericsType.List:
                if (genericTypes.Count < 2)
                {
                    LuminPackExceptionHelper.ThrowArgumentException("Insufficient generic parameters for List");
                }
                return ConvertGenericsToFieldType(genericTypes.Skip(1).ToList());
            case LuminGenericsType.Byte:
                return LuminFiledType.Byte;
            case LuminGenericsType.SByte:
                return LuminFiledType.SByte;
            case LuminGenericsType.Short:
                return LuminFiledType.Short;
            case LuminGenericsType.UShort:
                return LuminFiledType.UShort;
            case LuminGenericsType.Int:
                return LuminFiledType.Int;
            case LuminGenericsType.UInt:
                return LuminFiledType.UInt;
            case LuminGenericsType.Long:
                return LuminFiledType.Long;
            case LuminGenericsType.ULong:
                return LuminFiledType.ULong;
            case LuminGenericsType.Float:
                return LuminFiledType.Float;
            case LuminGenericsType.Double:
                return LuminFiledType.Double;
            case LuminGenericsType.Decimal:
                return LuminFiledType.Decimal;
            case LuminGenericsType.Bool:
                return LuminFiledType.Bool;
            case LuminGenericsType.Char:
                return LuminFiledType.Char;
            case LuminGenericsType.String:
                return LuminFiledType.String;
            case LuminGenericsType.Class:
                return LuminFiledType.Class;
            case LuminGenericsType.Struct:
                return LuminFiledType.Struct;
            case LuminGenericsType.Enum:
                return LuminFiledType.Enum;
            default:
                {
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<LuminFiledType>();
                }
        }
    }

    /// <summary>Determines whether the supplied value has the reference generic type classification.</summary>
    /// <param name="genericType">The generic type used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsReferenceGenericType(LuminGenericsType? genericType)
    {
        switch (genericType)
        {
            case LuminGenericsType.Byte:
                return false;
            case LuminGenericsType.SByte:
                return false;
            case LuminGenericsType.Bool:
                return false;
            case LuminGenericsType.Short:
                return false;
            case LuminGenericsType.UShort:
                return false;
            case LuminGenericsType.Int:
                return false;
            case LuminGenericsType.UInt:
                return false;
            case LuminGenericsType.Long:
                return false;
            case LuminGenericsType.ULong:
                return false;
            case LuminGenericsType.Float:
                return false;
            case LuminGenericsType.Double:
                return false;
            case LuminGenericsType.Decimal:
                return false;
            case LuminGenericsType.Enum:
                return false;
            case LuminGenericsType.Char:
                return false;
            case LuminGenericsType.String:
                return true;
            case LuminGenericsType.Class:
                return true;
            case LuminGenericsType.Struct:
                return true;
            case LuminGenericsType.List:
                return true;
            case LuminGenericsType.Array:
                return true;
            case LuminGenericsType.Null:
                return true;
            default:
                {
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<bool>();
                }
        }
    }

    /// <summary>Determines whether the supplied value has the reference filed type classification.</summary>
    /// <param name="filedType">The filed type used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsReferenceFiledType(LuminFiledType filedType)
    {
        switch (filedType)
        {
            case LuminFiledType.Byte:
                return false;
            case LuminFiledType.SByte:
                return false;
            case LuminFiledType.Bool:
                return false;
            case LuminFiledType.Short:
                return false;
            case LuminFiledType.UShort:
                return false;
            case LuminFiledType.Int:
                return false;
            case LuminFiledType.UInt:
                return false;
            case LuminFiledType.Long:
                return false;
            case LuminFiledType.ULong:
                return false;
            case LuminFiledType.Float:
                return false;
            case LuminFiledType.Double:
                return false;
            case LuminFiledType.Decimal:
                return false;
            case LuminFiledType.Char:
                return false;
            case LuminFiledType.String:
                return true;
            case LuminFiledType.Class:
                return true;
            case LuminFiledType.Struct:
                return true;
            case LuminFiledType.List:
                return true;
            case LuminFiledType.Array:
                return true;
            case LuminFiledType.Enum:
                return false;
            default:
                {
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<bool>();
                }
        }
    }

    /// <summary>Determines whether the supplied value has the unmanaged filed type classification.</summary>
    /// <param name="filedType">The filed type used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsUnmanagedFiledType(LuminFiledType filedType)
    {
        return filedType switch
        {
            LuminFiledType.Byte => true,
            LuminFiledType.SByte => true,
            LuminFiledType.Bool => true,
            LuminFiledType.Short => true,
            LuminFiledType.UShort => true,
            LuminFiledType.Int => true,
            LuminFiledType.UInt => true,
            LuminFiledType.Long => true,
            LuminFiledType.ULong => true,
            LuminFiledType.Float => true,
            LuminFiledType.Double => true,
            LuminFiledType.Decimal => true,
            LuminFiledType.Char => true,
            LuminFiledType.Enum => true,
            _ => false,
        };
    }

    /// <summary>Determines whether the supplied value has the unmanaged filed type classification.</summary>
    /// <param name="filedType">The filed type used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    public static bool IsUnmanagedFiledType(string filedType)
    {
        return filedType switch
        {
            "byte" => true,
            "sbyte" => true,
            "bool" => true,
            "short" => true,
            "ushort" => true,
            "int" => true,
            "uint" => true,
            "long" => true,
            "ulong" => true,
            "float" => true,
            "double" => true,
            "decimal" => true,
            "char" => true,
            _ => false,
        };
    }

    /// <summary>Determines whether the metadata contains serialized string record.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool ContainsSerializedStringRecord(LuminDataField field)
    {
        if (field.Type != LuminFiledType.String && !field.GenericType.Any((LuminGenericsType type) => type == LuminGenericsType.String))
        {
            return field.ClassFields.Any(ContainsSerializedStringRecord);
        }
        return true;
    }

    /// <summary>Performs the can deserialize directly into constructed object step for the current operation.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool CanDeserializeDirectlyIntoConstructedObject(LuminDataField field)
    {
        return !field.IsPrivate && !field.isProperty;
    }

    /// <summary>Performs the can deserialize directly into nested object step for the current operation.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool CanDeserializeDirectlyIntoNestedObject(LuminDataField field)
    {
        if (field.Type == LuminFiledType.Class && field.GenericType.Count == 0 && field.ClassFields.Count > 0 && field.RentPoolMethod == null && (field.SelectedConstructor == null || field.SelectedConstructor.Parameters.Count == 0))
        {
            return field.ClassFields.All((LuminDataField subField) => !subField.IsPrivate && !subField.isProperty);
        }
        return false;
    }

    /// <summary>Determines whether the supplied value has the string list classification.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsStringList(LuminDataField field)
    {
        if (field.Type == LuminFiledType.List && field.GenericType.Count == 1)
        {
            return field.GenericType[0] == LuminGenericsType.String;
        }
        return false;
    }

    /// <summary>Determines whether the supplied value has the exact managed array type classification.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsExactManagedArrayType(LuminDataField field)
    {
        return field.TypeSymbol is IArrayTypeSymbol { Rank: 1 } array &&
               !EmitterTypeTraits.CanUseUnmanagedSerialization(array.ElementType) && !ContainsTypeParameter(array.ElementType);
    }

    /// <summary>Determines whether the supplied value has the exact fresh collection type classification.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsExactFreshCollectionType(LuminDataField field)
    {
        string fieldTypeName = field.FullTypeName ?? field.TypeName ?? string.Empty;
        if (!fieldTypeName.EndsWith(">", StringComparison.Ordinal))
        {
            return false;
        }
        if (!fieldTypeName.StartsWith("global::System.Collections.Generic.List<", StringComparison.Ordinal) && !fieldTypeName.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal) && !fieldTypeName.StartsWith("global::System.Collections.Generic.Dictionary<", StringComparison.Ordinal))
        {
            return fieldTypeName.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal);
        }
        return true;
    }

    /// <summary>Determines whether the metadata contains type parameter.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool ContainsTypeParameter(ITypeSymbol type)
    {
        return type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
            INamedTypeSymbol named =>
                (named.ContainingType is not null && ContainsTypeParameter(named.ContainingType)) ||
                named.TypeArguments.Any(ContainsTypeParameter),
            _ => false
        };
    }
}
