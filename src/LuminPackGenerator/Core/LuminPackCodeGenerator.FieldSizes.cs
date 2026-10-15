using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{

    /// <summary>Obtains the serialize field length for the supplied input.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <param name="stringRecordLength">The expression that yields the current string header length.</param>
    /// <param name="multList">The prefix used to distinguish nested collection variables.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetSerializeFieldLength(LuminDataField field, int depth, string stringRecordLength, string multList = "_")
    {
        return GetFieldLength(field, depth, multList, "writer").Replace("writer.StringRecordLength()", stringRecordLength);
    }

    /// <summary>Obtains the field length for the supplied input.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <param name="multList">The prefix used to distinguish nested collection variables.</param>
    /// <param name="pattern">The pattern used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetFieldLength(LuminDataField field, int depth, string multList = "_", string pattern = "null")
    {
        string depthSuffix = $"{multList}{depth}";
        switch (field.Type)
        {
            case LuminFiledType.Byte:
                return "1";
            case LuminFiledType.SByte:
                return "1";
            case LuminFiledType.Short:
                return "2";
            case LuminFiledType.UShort:
                return "2";
            case LuminFiledType.Int:
                return "4";
            case LuminFiledType.UInt:
                return "4";
            case LuminFiledType.Long:
                return "8";
            case LuminFiledType.ULong:
                return "8";
            case LuminFiledType.Float:
                return "4";
            case LuminFiledType.Double:
                return "8";
            case LuminFiledType.Decimal:
                return "16";
            case LuminFiledType.Bool:
                return "1";
            case LuminFiledType.Char:
                return "2";
            case LuminFiledType.String:
                return (pattern == "null") ? (field.Name + "Length + 1") : (field.Name + "Length + " + pattern + ".StringRecordLength()");
            case LuminFiledType.List:
            case LuminFiledType.Array:
                return field.Name + "ListOffset" + depthSuffix;
            case LuminFiledType.Struct:
            case LuminFiledType.Class:
                return string.Join(" + ", field.ClassFields.Select((LuminDataField sub) => GetFieldLength(sub, depth + 1)));
            case LuminFiledType.Enum:
                return GetEnumFieldLength(field.EnumType);
            case LuminFiledType.Other:
                return "0";
            default:
                {
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<string>();
                }
        }
    }

    /// <summary>Obtains the enum field length for the supplied input.</summary>
    /// <param name="enumField">The enum field used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetEnumFieldLength(LuminEnumFieldType enumField)
    {
        switch (enumField)
        {
            case LuminEnumFieldType.Byte:
                return "1";
            case LuminEnumFieldType.SByte:
                return "1";
            case LuminEnumFieldType.Short:
                return "2";
            case LuminEnumFieldType.UShort:
                return "2";
            case LuminEnumFieldType.Int:
                return "4";
            case LuminEnumFieldType.UInt:
                return "4";
            case LuminEnumFieldType.Long:
                return "8";
            case LuminEnumFieldType.ULong:
                return "8";
            case LuminEnumFieldType.Float:
                return "4";
            case LuminEnumFieldType.Double:
                return "8";
            default:
                {
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<string>();
                }
        }
    }

    /// <summary>Determines whether the supplied value has the fixed length type classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsFixedLengthType(LuminFiledType type)
    {
        return type switch
        {
            LuminFiledType.Byte => true,
            LuminFiledType.SByte => true,
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
            LuminFiledType.Bool => true,
            LuminFiledType.Enum => true,
            _ => false,
        };
    }

    /// <summary>Obtains the fixed field length for the supplied input.</summary>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <returns>The result of the operation.</returns>
    private static string GetFixedFieldLength(LuminDataField field)
    {
        return field.Type switch
        {
            LuminFiledType.Byte => "1",
            LuminFiledType.SByte => "1",
            LuminFiledType.Short => "2",
            LuminFiledType.UShort => "2",
            LuminFiledType.Char => "2",
            LuminFiledType.Int => "4",
            LuminFiledType.UInt => "4",
            LuminFiledType.Long => "8",
            LuminFiledType.ULong => "8",
            LuminFiledType.Float => "4",
            LuminFiledType.Double => "8",
            LuminFiledType.Decimal => "16",
            LuminFiledType.Bool => "1",
            LuminFiledType.Enum => GetEnumFieldLength(field.EnumType),
            _ => "Unsafe.SizeOf<" + GetGeneratedTypeName(field) + ">()",
        };
    }
}
