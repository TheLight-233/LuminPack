using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{

    /// <summary>Obtains the enum type name for the supplied input.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetEnumTypeName(LuminDataField field)
    {
        return $"{field.EnumType}";
    }

    /// <summary>Obtains the field local variable type for the supplied input.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetFieldLocalVariableType(LuminDataField field)
    {
        string fieldTypeName = field.TypeName;
        if (string.IsNullOrEmpty(fieldTypeName) || fieldTypeName.EndsWith("?"))
        {
            fieldTypeName = (string.IsNullOrEmpty(field.FullTypeName) ? (fieldTypeName?.TrimEnd(new char[1] { '?' }) ?? "object") : field.FullTypeName);
        }
        return fieldTypeName.TrimEnd(new char[1] { '?' });
    }

    /// <summary>Obtains the generic type name for the supplied input.</summary>
    /// <param name="genericType">The generic type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetGenericTypeName(LuminGenericsType genericType)
    {
        return genericType switch
        {
            LuminGenericsType.Byte => "byte",
            LuminGenericsType.SByte => "sbyte",
            LuminGenericsType.Short => "short",
            LuminGenericsType.UShort => "ushort",
            LuminGenericsType.Int => "int",
            LuminGenericsType.UInt => "uint",
            LuminGenericsType.Long => "long",
            LuminGenericsType.ULong => "ulong",
            LuminGenericsType.Float => "float",
            LuminGenericsType.Double => "double",
            LuminGenericsType.Decimal => "decimal",
            LuminGenericsType.Char => "char",
            LuminGenericsType.String => "string",
            LuminGenericsType.Bool => "bool",
            LuminGenericsType.List => "List",
            LuminGenericsType.Enum => "Enum",
            _ => genericType.ToString(),
        };
    }

    /// <summary>Obtains the enum type name for the supplied input.</summary>
    /// <param name="genericType">The generic type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetEnumTypeName(LuminEnumFieldType genericType)
    {
        switch (genericType)
        {
            case LuminEnumFieldType.Byte:
                return "byte";
            case LuminEnumFieldType.SByte:
                return "sbyte";
            case LuminEnumFieldType.Short:
                return "short";
            case LuminEnumFieldType.UShort:
                return "ushort";
            case LuminEnumFieldType.Int:
                return "int";
            case LuminEnumFieldType.UInt:
                return "uint";
            case LuminEnumFieldType.Long:
                return "long";
            case LuminEnumFieldType.ULong:
                return "ulong";
            case LuminEnumFieldType.Float:
                return "float";
            case LuminEnumFieldType.Double:
                return "double";
            default:
                {
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<string>();
                }
        }
    }

    /// <summary>Obtains the full generic type name for the supplied input.</summary>
    /// <param name="genericTypes">The generic types used by this operation.</param>
    /// <param name="classname">The classname used by this operation.</param>
    /// <param name="genericType">The generic type used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetFullGenericTypeName(List<LuminGenericsType> genericTypes, string classname = "", string genericType = "")
    {
        if (genericTypes.Count == 0)
        {
            LuminPackExceptionHelper.ThrowArgumentException("Empty generic types");
        }
        LuminGenericsType luminGenericsType = genericTypes.FirstOrDefault();
        switch (luminGenericsType)
        {
            case LuminGenericsType.List:
                return "List<" + GetFullGenericTypeName(genericTypes.Skip(1).ToList(), classname, genericType) + ">";
            case LuminGenericsType.Array:
                return GetFullGenericTypeName(genericTypes.Skip(1).ToList(), classname) + "[]";
            default:
                if (!(classname == "") && !(classname == "Your Class Name"))
                {
                    if (!(genericType == "") && genericType != null)
                    {
                        return classname + "<" + genericType + ">";
                    }
                    return luminGenericsType switch
                    {
                        LuminGenericsType.Class => classname,
                        LuminGenericsType.Struct => classname,
                        _ => classname,
                    };
                }
                return GetGenericTypeName(luminGenericsType);
        }
    }

    /// <summary>Obtains the generated type name for the supplied input.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetGeneratedTypeName(LuminDataField field)
    {
        return field.TypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            ?? field.TypeName;
    }

}
