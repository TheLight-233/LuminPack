using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="targetObj">The expression identifying the deserialization target.</param>
    /// <param name="span">The buffer used by this operation.</param>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <param name="isFirst">The is first used by this operation.</param>
    /// <param name="fieldPath">The expression that accesses the field in generated code.</param>
    /// <param name="isArray">The is array used by this operation.</param>
    /// <param name="isList">The is constructorArgumentExpressions used by this operation.</param>
    /// <param name="multList">The prefix used to distinguish nested collection variables.</param>
    /// <param name="isPrivateFiled">The is private filed used by this operation.</param>
    /// <param name="parentName">The parent name used by this operation.</param>
    /// <param name="collectionHeadAlreadyRead">The collection head already read used by this operation.</param>
    /// <param name="targetIsKnownFresh">The target is known fresh used by this operation.</param>
    private static void GenerateDeserializeCode(StringBuilder sb, LuminDataField field, string targetObj, string span, string offset, int indent, int depth, bool isFirst = true, string fieldPath = "", bool isArray = false, bool isList = false, string multList = "_", bool isPrivateFiled = false, string parentName = "", bool collectionHeadAlreadyRead = false, bool targetIsKnownFresh = false)
    {
        string indentation = new string(' ', indent * 4);
        string depthSuffix = $"{multList}{depth}";
        switch (field.Type)
        {
            case LuminFiledType.Byte:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out byte " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.SByte:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out sbyte " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Bool:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out bool " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Short:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out short " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.UShort:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out ushort " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Int:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out int " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.UInt:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out uint " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Long:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out long " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.ULong:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out ulong " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Float:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out float " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Double:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out double " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Decimal:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out decimal " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.Char:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out char " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            case LuminFiledType.String:
                sb.AppendLine(indentation + targetObj + " = reader.ReadStringAndAdvance(ref " + offset + ")!;");
                break;
            case LuminFiledType.List:
                {
                    if (isFirst && depth == 0 && !collectionHeadAlreadyRead)
                    {
                        sb.AppendLine(indentation + "reader.TryReadCollectionHead(ref " + offset + ", out int " + field.Name + "Count" + depthSuffix + ");");
                    }
                    LuminDataField elementField = new LuminDataField
                    {
                        Name = field.Name,
                        Type = ConvertGenericsToFieldType(field.GenericType.FirstOrDefault()),
                        GenericType = field.GenericType.Skip(1).ToList(),
                        ClassName = field.ClassName,
                        ClassFields = field.ClassFields,
                        ClassGenericType = field.ClassGenericType,
                        ConstructParameterCount = field.ConstructParameterCount,
                        IsCompress = field.IsCompress
                    };
                    string fullGenericTypeName = GetFullGenericTypeName(field.GenericType, elementField.ClassName, elementField.ClassGenericType);
                    bool isRootCollection = isFirst && depth == 0;
                    if (isRootCollection)
                    {
                        sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = reader.CreateFreshListSpan<" + fullGenericTypeName + ">(" + field.Name + "Count" + depthSuffix + ", out " + targetObj + ");");
                    }
                    else if (isFirst)
                    {
                        sb.AppendLine(indentation + targetObj + " = new List<" + fullGenericTypeName + ">(" + field.Name + "Count" + depthSuffix + ");");
                    }
                    sb.AppendLine(indentation + "int " + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + 4;");
                    if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
                    {
                        if (depth > 0)
                        {
                            sb.AppendLine($"{indentation}var {field.Name}TempSpan{depthSuffix} = LuminPackMarshal.GetListSpan(element__{depth - 1}, {field.Name}Count{depthSuffix});");
                            if (field.IsCompress)
                            {
                                sb.AppendLine(indentation + "reader.DangerousReadUnmanagedSpanWithCompress(ref " + offset + ", ref " + field.Name + "TempSpan" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                                sb.AppendLine($"{indentation}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{depthSuffix});");
                                sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + " + field.Name + "TempLength" + depthSuffix + ";");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "reader.ReadUnmanagedSpan(ref " + field.Name + "ListOffset" + depthSuffix + ", ref " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                                sb.AppendLine($"{indentation}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{depthSuffix});");
                                sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " += " + field.Name + "TempLength" + depthSuffix + ";");
                            }
                            break;
                        }
                        if (!isRootCollection)
                        {
                            sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + depthSuffix + ");");
                        }
                        if (field.IsCompress)
                        {
                            sb.AppendLine(indentation + "reader.DangerousReadUnmanagedSpanWithCompress(ref " + offset + ", ref " + field.Name + "TempSpan" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                            sb.AppendLine(indentation + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + depthSuffix + ");");
                            sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + " + field.Name + "TempLength" + depthSuffix + ";");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "reader.ReadUnmanagedSpan(ref " + field.Name + "ListOffset" + depthSuffix + ", ref " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                            sb.AppendLine(indentation + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + depthSuffix + ");");
                            sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " += " + field.Name + "TempLength" + depthSuffix + ";");
                        }
                        break;
                    }
                    if (IsPureValueTypeStruct(elementField))
                    {
                        if (field.IsCompress)
                        {
                            if (depth > 0)
                            {
                                sb.AppendLine($"{indentation}var {field.Name}TempSpan{depthSuffix} = LuminPackMarshal.GetListSpan(element__{depth - 1}, {field.Name}Count{depthSuffix});");
                                sb.AppendLine(indentation + "reader.DangerousReadUnmanagedSpanWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + depthSuffix + ", ref " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                                sb.AppendLine($"{indentation}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{depthSuffix});");
                            }
                            else
                            {
                                if (!isRootCollection)
                                {
                                    sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + depthSuffix + ");");
                                }
                                sb.AppendLine(indentation + "reader.DangerousReadUnmanagedSpanWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + depthSuffix + ", ref " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                                sb.AppendLine(indentation + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + depthSuffix + ");");
                            }
                        }
                        else if (depth > 0)
                        {
                            sb.AppendLine($"{indentation}var {field.Name}TempSpan{depthSuffix} = LuminPackMarshal.GetListSpan(element__{depth - 1}, {field.Name}Count{depthSuffix});");
                            sb.AppendLine(indentation + "reader.ReadUnmanagedSpan(ref " + offset + ", ref " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                            sb.AppendLine($"{indentation}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{depthSuffix});");
                        }
                        else
                        {
                            if (!isRootCollection)
                            {
                                sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + depthSuffix + ");");
                            }
                            sb.AppendLine(indentation + "reader.ReadUnmanagedSpan(ref " + offset + ", ref " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                            sb.AppendLine(indentation + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + depthSuffix + ");");
                        }
                        sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " += " + field.Name + "TempLength" + depthSuffix + ";");
                        break;
                    }
                    if (depth > 0)
                    {
                        sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = LuminPackMarshal.GetListSpan(" + fieldPath + "!, " + field.Name + "Count" + depthSuffix + ");");
                    }
                    else if (!isRootCollection)
                    {
                        sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + depthSuffix + ");");
                    }
                    sb.AppendLine(indentation + "for (int i" + depthSuffix + " = 0; i" + depthSuffix + " < " + field.Name + "Count" + depthSuffix + "; i" + depthSuffix + "++)");
                    sb.AppendLine(indentation + "{");
                    StringBuilder stringBuilder = new StringBuilder();
                    if (isArray)
                    {
                        stringBuilder.Append($"[i{multList}{depth}]");
                    }
                    if (elementField.Type == LuminFiledType.List)
                    {
                        sb.AppendLine($"{indentation}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{depthSuffix}, out int {elementField.Name}Count_{depth + 1}))");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                        sb.AppendLine(indentation + "        " + field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "] = default!;");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                        string fullGenericTypeName2 = GetFullGenericTypeName(elementField.GenericType, elementField.ClassName, elementField.ClassGenericType);
                        sb.AppendLine($"{indentation}    var element_{depthSuffix} = new List<{fullGenericTypeName2}>({elementField.Name}Count_{depth + 1});");
                        sb.AppendLine(indentation + "    " + field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "] = element_" + depthSuffix + ";");
                    }
                    else if (elementField.Type == LuminFiledType.Array)
                    {
                        LuminGenericsType luminGenericsType = field.GenericType.Last();
                        sb.AppendLine($"{indentation}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{depthSuffix}, out int {elementField.Name}Count_{depth + 1}))");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                        sb.AppendLine(indentation + "        " + field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "] = default!;");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                        StringBuilder stringBuilder2 = new StringBuilder();
                        for (int i = 1; i < field.GenericType.Count && field.GenericType[i] == LuminGenericsType.Array; i++)
                        {
                            stringBuilder2.Append("[]");
                        }
                        if (luminGenericsType != LuminGenericsType.Struct && luminGenericsType != LuminGenericsType.Class)
                        {
                            sb.AppendLine($"{indentation}    var element_{depthSuffix} = LuminPackMarshal.AllocateUninitializedArray<{GetGenericTypeName(luminGenericsType)}{stringBuilder2}>({elementField.Name}Count_{depth + 1});");
                        }
                        else
                        {
                            sb.AppendLine($"{indentation}    var element_{depthSuffix} = LuminPackMarshal.AllocateUninitializedArray<{field.ClassName}{stringBuilder2}>({elementField.Name}Count_{depth + 1});");
                        }
                        sb.AppendLine(indentation + "    " + field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "] = element_" + depthSuffix + ";");
                    }
                    else if (elementField.Type == LuminFiledType.Class)
                    {
                        sb.AppendLine(indentation + "    if (reader.PeekIsNullObject(ref " + field.Name + "ListOffset" + depthSuffix + "))");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                        sb.AppendLine(indentation + "        " + field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "] = default!;");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                    }
                    string elementTarget3 = field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "]";
                    GenerateDeserializeCode(sb, elementField, elementTarget3, span, field.Name + "ListOffset" + depthSuffix, indent + 1, depth + 1, isFirst: false, elementTarget3, isArray: false, isList: true, multList, isPrivateFiled: false, parentName + field.Name);
                    LuminFiledType type = elementField.Type;
                    if (type != LuminFiledType.String && type != LuminFiledType.Class && type != LuminFiledType.Struct)
                    {
                        type = elementField.Type;
                        if (type - 17 <= LuminFiledType.UInt)
                        {
                            sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " = " + GetFieldLength(elementField, depth + 1) + ";");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += " + GetFieldLength(elementField, depth + 1) + ";");
                        }
                    }
                    sb.AppendLine(indentation + "}");
                    break;
                }
            case LuminFiledType.Array:
                {
                    if (isFirst && !collectionHeadAlreadyRead && (IsReferenceGenericType(field.GenericType.FirstOrDefault()) || !field.IsCompress))
                    {
                        sb.AppendLine(indentation + "reader.TryReadCollectionHead(ref " + offset + ", out int " + field.Name + "Count" + depthSuffix + ");");
                    }
                    LuminDataField elementField5 = new LuminDataField
                    {
                        Name = field.Name,
                        Type = ConvertGenericsToFieldType(field.GenericType.FirstOrDefault()),
                        GenericType = field.GenericType.Skip(1).ToList(),
                        ClassName = field.ClassName,
                        ClassFields = field.ClassFields,
                        ClassGenericType = field.ClassGenericType,
                        ConstructParameterCount = field.ConstructParameterCount,
                        IsCompress = field.IsCompress
                    };
                    StringBuilder stringBuilder3 = new StringBuilder();
                    for (int fieldIndex3 = 0; fieldIndex3 < field.GenericType.Count && field.GenericType[fieldIndex3] == LuminGenericsType.Array; fieldIndex3++)
                    {
                        stringBuilder3.Append("[]");
                    }
                    if (isFirst && (IsReferenceGenericType(field.GenericType.FirstOrDefault()) || !field.IsCompress))
                    {
                        LuminGenericsType luminGenericsType2 = field.GenericType.Last();
                        if (elementField5.Type == LuminFiledType.List)
                        {
                            string fullGenericTypeName3 = GetFullGenericTypeName(elementField5.GenericType, elementField5.ClassName, elementField5.ClassGenericType);
                            sb.AppendLine($"{indentation}{targetObj} = LuminPackMarshal.AllocateUninitializedArray<List<{fullGenericTypeName3}>{stringBuilder3}>({field.Name}Count{depthSuffix});");
                        }
                        else if (luminGenericsType2 != LuminGenericsType.Struct && luminGenericsType2 != LuminGenericsType.Class)
                        {
                            sb.AppendLine($"{indentation}{targetObj} = LuminPackMarshal.AllocateUninitializedArray<{GetGenericTypeName(luminGenericsType2)}{stringBuilder3}>({field.Name}Count{depthSuffix});");
                        }
                        else
                        {
                            sb.AppendLine($"{indentation}{targetObj} = LuminPackMarshal.AllocateUninitializedArray<{field.ClassName}{stringBuilder3}>({field.Name}Count{depthSuffix});");
                        }
                    }
                    sb.AppendLine(indentation + "int " + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + 4;");
                    if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
                    {
                        if (field.IsCompress)
                        {
                            if (depth > 0)
                            {
                                sb.AppendLine($"{indentation}reader.DangerousReadUnmanagedArrayWithCompress(ref {offset}, ref element__{depth - 1}, out var {field.Name}TempLength{depthSuffix});");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "reader.DangerousReadUnmanagedArrayWithCompress(ref " + offset + ", ref " + targetObj + "!, out var " + field.Name + "TempLength" + depthSuffix + ");");
                            }
                            sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + " + field.Name + "TempLength" + depthSuffix + ";");
                            break;
                        }
                        if (depth > 0)
                        {
                            sb.AppendLine($"{indentation}reader.ReadFreshUnmanagedArray(ref {field.Name}ListOffset{depthSuffix}, element__{depth - 1}, {field.Name}Count{depthSuffix}, out var {field.Name}TempLength{depthSuffix});");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "reader.ReadFreshUnmanagedArray(ref " + field.Name + "ListOffset" + depthSuffix + ", " + targetObj + "!, " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " += " + field.Name + "TempLength" + depthSuffix + ";");
                        break;
                    }
                    if (IsPureValueTypeStruct(elementField5))
                    {
                        if (field.IsCompress)
                        {
                            if (depth > 0)
                            {
                                sb.AppendLine($"{indentation}reader.DangerousReadUnmanagedArrayWithOutHeaderWithCompress(ref {field.Name}ListOffset{depthSuffix}, ref element__{depth - 1}, {field.Name}Count{depthSuffix}, out var {field.Name}TempLength{depthSuffix});");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "reader.DangerousReadUnmanagedArrayWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + depthSuffix + ", ref " + targetObj + "!, " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                            }
                        }
                        else if (depth > 0)
                        {
                            sb.AppendLine($"{indentation}reader.ReadFreshUnmanagedArray(ref {offset}, element__{depth - 1}, {field.Name}Count{depthSuffix}, out var {field.Name}TempLength{depthSuffix});");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "reader.ReadFreshUnmanagedArray(ref " + offset + ", " + targetObj + "!, " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " += " + field.Name + "TempLength" + depthSuffix + ";");
                        break;
                    }
                    string elementTypeName;
                    if (depth > 0)
                    {
                        sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = " + fieldPath + ".AsSpan();");
                        elementTypeName = field.Name + "Count" + depthSuffix;
                    }
                    else
                    {
                        sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = " + targetObj + "!;");
                        elementTypeName = field.Name + "Count" + depthSuffix;
                    }
                    sb.AppendLine(indentation + "for (int i" + depthSuffix + " = 0; i" + depthSuffix + " < " + elementTypeName + "; i" + depthSuffix + "++)");
                    sb.AppendLine(indentation + "{");
                    if (elementField5.Type == LuminFiledType.List)
                    {
                        sb.AppendLine($"{indentation}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{depthSuffix}, out int {elementField5.Name}Count_{depth + 1}))");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                        string fullGenericTypeName4 = GetFullGenericTypeName(elementField5.GenericType, elementField5.ClassName, elementField5.ClassGenericType);
                        sb.AppendLine($"{indentation}    var element_{depthSuffix} = new List<{fullGenericTypeName4}>({elementField5.Name}Count_{depth + 1});");
                        sb.AppendLine(indentation + "    " + field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "] = element_" + depthSuffix + ";");
                    }
                    else if (elementField5.Type == LuminFiledType.Array)
                    {
                        sb.AppendLine($"{indentation}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{depthSuffix}, out int {elementField5.Name}Count_{depth + 1}))");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                        StringBuilder stringBuilder4 = new StringBuilder();
                        for (int fieldIndex4 = 1; fieldIndex4 < field.GenericType.Count && field.GenericType[fieldIndex4] == LuminGenericsType.Array; fieldIndex4++)
                        {
                            stringBuilder4.Append("[]");
                        }
                        LuminGenericsType luminGenericsType3 = field.GenericType.Last();
                        if (luminGenericsType3 != LuminGenericsType.Struct && luminGenericsType3 != LuminGenericsType.Class)
                        {
                            sb.AppendLine($"{indentation}    var element_{depthSuffix} = LuminPackMarshal.AllocateUninitializedArray<{GetGenericTypeName(luminGenericsType3)}{stringBuilder4}>({elementField5.Name}Count_{depth + 1});");
                        }
                        else
                        {
                            sb.AppendLine($"{indentation}    var element_{depthSuffix} = LuminPackMarshal.AllocateUninitializedArray<{field.ClassName}{stringBuilder4}>({elementField5.Name}Count_{depth + 1});");
                        }
                        sb.AppendLine(indentation + "    " + field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "] = element_" + depthSuffix + ";");
                    }
                    else if (elementField5.Type == LuminFiledType.Class)
                    {
                        sb.AppendLine(indentation + "    if (reader.PeekIsNullObject(ref " + field.Name + "ListOffset" + depthSuffix + "))");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                    }
                    string elementTarget11 = field.Name + "TempSpan" + depthSuffix + "[i" + depthSuffix + "]";
                    GenerateDeserializeCode(sb, elementField5, elementTarget11, span, field.Name + "ListOffset" + depthSuffix, indent + 1, depth + 1, isFirst: false, elementTarget11, isArray: true, isList: false, multList, isPrivateFiled: false, parentName + field.Name);
                    LuminFiledType type = elementField5.Type;
                    if (type != LuminFiledType.String && type != LuminFiledType.Class && type != LuminFiledType.Struct)
                    {
                        type = elementField5.Type;
                        if (type - 17 <= LuminFiledType.UInt)
                        {
                            sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " = " + GetFieldLength(elementField5, depth + 1) + ";");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += " + GetFieldLength(elementField5, depth + 1) + ";");
                        }
                    }
                    sb.AppendLine(indentation + "}");
                    break;
                }
            case LuminFiledType.Struct:
            case LuminFiledType.Class:
                {
                    if (IsPureValueTypeStruct(field))
                    {
                        sb.AppendLine(indentation + "// Unmanaged struct");
                        sb.AppendLine(indentation + offset + " += reader.ReadUnmanaged(ref " + offset + ", out " + targetObj + ");");
                        break;
                    }
                    sb.AppendLine(indentation + "// Deserialize" + field.ClassName);
                    sb.AppendLine(indentation + offset + " += 1;");
                    bool useNestedTarget2 = CanDeserializeDirectlyIntoNestedObject(field);
                    string resultVariable4 = parentName + field.Name + "Result" + depthSuffix;
                    if (useNestedTarget2)
                    {
                        if (string.IsNullOrWhiteSpace(field.FullTypeName))
                        {
                            sb.AppendLine(indentation + "var " + resultVariable4 + " = " + targetObj + " = new();");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "var " + resultVariable4 + " = new " + field.FullTypeName + "();");
                        }
                    }
                    if (!useNestedTarget2)
                    {
                        sb.AppendLine(indentation + "// Declare locals for nested type members.");
                        foreach (LuminDataField classField in field.ClassFields)
                        {
                            string nullableSuffix5 = ((classField.FieldType == LuminDataType.Reference) ? "?" : "");
                            sb.AppendLine(indentation + GetFieldLocalVariableType(classField) + nullableSuffix5 + " " + parentName + field.Name + classField.Name + "Temp" + depthSuffix + " = default!;");
                        }
                        sb.AppendLine();
                    }
                    for (int j = 0; j < field.ClassFields.Count; j++)
                    {
                        LuminDataField nestedField2 = field.ClassFields[j];
                        string fieldTarget6 = (useNestedTarget2 ? (resultVariable4 + "." + nestedField2.Identifier) : (parentName + field.Name + nestedField2.Name + "Temp" + depthSuffix));
                        nestedField2.belongClassFieldName = field.Name;
                        int fieldIndentation = indent;
                        if (IsMergeableField(nestedField2))
                        {
                            int mergeEndIndex2 = FindNextMergeableField(field.ClassFields, j);
                            if (mergeEndIndex2 != j)
                            {
                                if (mergeEndIndex2 - j >= 14)
                                {
                                    mergeEndIndex2 = j + 14;
                                }
                                if (TryGenerateFixedUnmanagedRead(sb, field.ClassFields, j, mergeEndIndex2,
                                    member => useNestedTarget2 ? resultVariable4 + "." + member.Identifier : parentName + field.Name + member.Name + "Temp" + depthSuffix,
                                    offset, indentation))
                                {
                                    j = mergeEndIndex2;
                                    continue;
                                }
                                sb.Append(indentation + offset + " += reader.ReadUnmanaged(ref " + offset);
                                for (int k = j; k <= mergeEndIndex2; k++)
                                {
                                    LuminDataField nestedField3 = field.ClassFields[k];
                                    string fieldTarget7 = (useNestedTarget2 ? (resultVariable4 + "." + nestedField3.Identifier) : (parentName + field.Name + nestedField3.Name + "Temp" + depthSuffix));
                                    sb.Append(", out " + fieldTarget7);
                                }
                                sb.Append(");");
                                sb.AppendLine();
                                j = mergeEndIndex2;
                                continue;
                            }
                        }
                        switch (nestedField2.Type)
                        {
                            case LuminFiledType.List:
                            case LuminFiledType.Array:
                                sb.AppendLine(indentation + "if (reader.PeekIsNullCollection(ref " + offset + "))");
                                sb.AppendLine(indentation + "{");
                                sb.AppendLine(indentation + "    " + offset + " += 4;");
                                sb.AppendLine(indentation + "    " + fieldTarget6 + " = default;");
                                sb.AppendLine(indentation + "}");
                                sb.AppendLine(indentation + "else");
                                fieldIndentation++;
                                break;
                            case LuminFiledType.Class:
                                sb.AppendLine(indentation + "if (reader.PeekIsNullObject(ref " + offset + "))");
                                sb.AppendLine(indentation + "{");
                                sb.AppendLine(indentation + "    " + offset + " += 1;");
                                sb.AppendLine(indentation + "    " + fieldTarget6 + " = default;");
                                sb.AppendLine(indentation + "}");
                                sb.AppendLine(indentation + "else");
                                fieldIndentation++;
                                break;
                        }
                        if (fieldIndentation != indent)
                        {
                            sb.AppendLine(indentation + "{");
                        }
                        LuminFiledType type = nestedField2.Type;
                        bool isNestedValueType3 = type - 17 <= LuminFiledType.UInt;
                        int depth2 = ((!isNestedValueType3) ? (depth + 1) : 0);
                        GenerateDeserializeCode(sb, nestedField2, fieldTarget6, span, offset, fieldIndentation, depth2, isFirst: true, "", isArray: false, isList: false, multList + "_", isPrivateFiled: true, parentName + field.Name + "_", collectionHeadAlreadyRead: false, useNestedTarget2);
                        string typeExpression8 = ((fieldIndentation == indent) ? indentation : (indentation + "    "));
                        type = field.ClassFields[j].Type;
                        if (type != LuminFiledType.String && type != LuminFiledType.Class && type != LuminFiledType.Struct)
                        {
                            type = field.ClassFields[j].Type;
                            if (type - 17 <= LuminFiledType.UInt)
                            {
                                sb.AppendLine(typeExpression8 + offset + " = " + GetFieldLength(nestedField2, depth2, multList + "_") + ";");
                            }
                            else
                            {
                                sb.AppendLine(typeExpression8 + offset + " += " + GetFieldLength(nestedField2, depth2, multList + "_") + ";");
                            }
                        }
                        if (fieldIndentation != indent)
                        {
                            sb.AppendLine(indentation + "}");
                        }
                    }
                    if (useNestedTarget2)
                    {
                        if (!string.IsNullOrWhiteSpace(field.FullTypeName))
                        {
                            sb.AppendLine(indentation + targetObj + " = " + resultVariable4 + ";");
                        }
                        break;
                    }
                    List<string> constructorArgumentExpressions = new List<string>();
                    if (field.SelectedConstructor != null && field.SelectedConstructor.Parameters.Count > 0)
                    {
                        foreach (ConstructorParameter param in field.SelectedConstructor.Parameters)
                        {
                            LuminDataField nestedField4 = field.ClassFields.FirstOrDefault((LuminDataField f) => f.Name == param.MatchingFieldName);
                            if (nestedField4 != null)
                            {
                                constructorArgumentExpressions.Add(parentName + field.Name + nestedField4.Name + "Temp" + depthSuffix);
                            }
                            else
                            {
                                constructorArgumentExpressions.Add("default");
                            }
                        }
                    }
                    string constructorArguments = string.Join(", ", constructorArgumentExpressions);
                    List<LuminDataField> publicFieldAssignments = field.ClassFields.Where((LuminDataField f) => (field.SelectedConstructor == null || !field.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && !f.IsPrivate).ToList();
                    List<LuminDataField> privateFieldAssignments = field.ClassFields.Where((LuminDataField f) => (field.SelectedConstructor == null || !field.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && f.IsPrivate).ToList();
                    if (field.RentPoolMethod != null)
                    {
                        if (field.RentPoolMethod.ReturnsByRef)
                        {
                            sb.AppendLine("            value = ref " + field.FullTypeName + "." + ((ISymbol)field.RentPoolMethod).Name + "();");
                        }
                        else
                        {
                            sb.AppendLine(indentation + targetObj + " = " + field.FullTypeName + "." + ((ISymbol)field.RentPoolMethod).Name + "();");
                        }
                        sb.AppendLine(indentation + "// Populate every field on the pooled instance.");
                        {
                            foreach (LuminDataField classField2 in field.ClassFields)
                            {
                                if (classField2.IsPrivate)
                                {
                                    sb.AppendLine(indentation + "Get" + field.Name + classField2.Name + "(" + targetObj + ") = " + parentName + field.Name + classField2.Name + "Temp" + depthSuffix + ";");
                                }
                                else
                                {
                                    sb.AppendLine(indentation + targetObj + "." + classField2.Identifier + " = " + parentName + field.Name + classField2.Name + "Temp" + depthSuffix + ";");
                                }
                            }
                            break;
                        }
                    }
                    if (publicFieldAssignments.Count > 0)
                    {
                        if (field.SelectedConstructor != null && field.SelectedConstructor.Parameters.Count > 0)
                        {
                            sb.AppendLine(indentation + targetObj + " = new " + field.FullTypeName + "(" + constructorArguments + ")");
                        }
                        else
                        {
                            sb.AppendLine(indentation + targetObj + " = new " + field.FullTypeName + "()");
                        }
                        sb.AppendLine(indentation + "{");
                        foreach (LuminDataField item in publicFieldAssignments)
                        {
                            sb.AppendLine(indentation + "    " + item.Name + " = " + parentName + field.Name + item.Name + "Temp" + depthSuffix + ",");
                        }
                        sb.AppendLine(indentation + "};");
                    }
                    else if (field.SelectedConstructor != null && field.SelectedConstructor.Parameters.Count > 0)
                    {
                        sb.AppendLine(indentation + targetObj + " = new " + field.FullTypeName + "(" + constructorArguments + ");");
                    }
                    else
                    {
                        sb.AppendLine(indentation + targetObj + " = new " + field.FullTypeName + "();");
                    }
                    if (privateFieldAssignments.Count <= 0)
                    {
                        break;
                    }
                    sb.AppendLine(indentation + "// Assign private fields.");
                    {
                        foreach (LuminDataField item2 in privateFieldAssignments)
                        {
                            sb.AppendLine(indentation + "Get" + field.Name + item2.Name + "(" + targetObj + ") = " + parentName + field.Name + item2.Name + "Temp" + depthSuffix + ";");
                        }
                        break;
                    }
                }
            case LuminFiledType.Enum:
                if (isFirst)
                {
                    sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn<" + field.TypeName + ">(ref " + offset + ", out " + targetObj + ");");
                    break;
                }
                sb.AppendLine(indentation + "reader.ReadUnmanagedWithoutSizeReturn<" + field.TypeName + ">(ref " + offset + ", out var " + field.Name + "TempValue" + depthSuffix + ");");
                sb.AppendLine(indentation + targetObj + " = " + field.Name + "TempValue" + depthSuffix + ";");
                break;
            default:
                // A formatter consumes through reader state, which can lag a local offset used by
                // inline collection members. Synchronize at the boundary and resume inline reads
                // from the formatter's resulting position.
                if (field.Type == LuminFiledType.Other)
                {
                    sb.AppendLine(indentation + "reader.FlushCurrentIndex(" + offset + ");");
                }
                if (field.IsCompress)
                {
                    sb.AppendLine(indentation + "reader.ReadValueWithCompress(ref " + targetObj + ");");
                }
                else if (targetIsKnownFresh && IsExactFreshCollectionType(field) &&
                         field.TypeSymbol is not null && !ContainsTypeParameter(field.TypeSymbol))
                {
                    sb.AppendLine(indentation + "reader.ReadFreshValue(ref " + targetObj + ");");
                }
                else
                {
                    sb.AppendLine(indentation + "reader.ReadValue(ref " + targetObj + ");");
                }
                if (field.Type == LuminFiledType.Other)
                {
                    sb.AppendLine(indentation + offset + " = reader.GetCurrentSpanIndex();");
                }
                break;
        }
    }

    /// <summary>Appends binary deserialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="polymorphism">The polymorphism used by this operation.</param>
    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
    {
        string concreteTypeName = data.classFullName;
        if (!concreteTypeName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            concreteTypeName = "global::" + data.classNameSpace + "." + data.classFullName;
        }
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
        }
        if (IsPureUnmanagedStruct(data))
        {
            sb.AppendLine("            ref int offset = ref reader.GetCurrentSpanOffset();");
            sb.AppendLine("            reader.Advance(reader.ReadUnmanaged(ref offset, out value));");
            foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserialized))
            {
                sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
            }
            sb.AppendLine();
            return;
        }
        bool canConstructDirectly = data.RentPoolMethod == null && (data.SelectedConstructor == null || data.SelectedConstructor.Parameters.Count == 0);
        bool canUseLocalOffset = canConstructDirectly && data.fields.All((LuminDataField field) => !ContainsNestedFormatterCall(field));
        sb.AppendLine();
        sb.AppendLine(canUseLocalOffset ? "            int offset = reader.GetCurrentSpanIndex();" : "            ref int offset = ref reader.GetCurrentSpanOffset();");
        if (!canConstructDirectly || data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
        {
            sb.AppendLine("            // Declare locals to hold the field values.");
            foreach (LuminLocalFieldData localField in data.localFields)
            {
                if (canConstructDirectly && data.fields.Any(field => field.Name == localField.Name && CanDeserializeDirectlyIntoConstructedObject(field)))
                {
                    continue;
                }
                string nullableSuffix3 = (localField.IsValue ? "" : "?");
                sb.AppendLine("            " + localField.TypeName + nullableSuffix3 + " " + localField.Name + "Temp = default!;");
            }
            sb.AppendLine();
        }
        if (!polymorphism && !data.isValueType)
        {
            sb.AppendLine("            if (reader.PeekIsNullObject(ref offset))");
            sb.AppendLine("            {");
            sb.AppendLine("                offset += 1;");
            if (canUseLocalOffset)
            {
                sb.AppendLine("                reader.FlushCurrentIndex(offset);");
            }
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        if (!polymorphism)
        {
            sb.AppendLine("            offset += 1;");
            sb.AppendLine();
        }
        if (canConstructDirectly)
        {
            sb.AppendLine("            var result = new " + concreteTypeName + "();");
        }
        for (int fieldIndex = 0; fieldIndex < data.fields.Count; fieldIndex++)
        {
            LuminDataField nestedField = data.fields[fieldIndex];
            string fieldTarget4 = (canConstructDirectly && CanDeserializeDirectlyIntoConstructedObject(nestedField)) ? ("result." + nestedField.Identifier) : (nestedField.Name + "Temp");
            if (nestedField.FieldType == LuminDataType.Reference && nestedField.Type != LuminFiledType.Other)
            {
                switch (nestedField.Type)
                {
                    case LuminFiledType.List:
                    case LuminFiledType.Array:
                        {
                            if (nestedField.Type == LuminFiledType.Array &&
                                !nestedField.IsCompress && IsExactManagedArrayType(nestedField))
                            {
                                if (canUseLocalOffset)
                                {
                                    sb.AppendLine("            reader.FlushCurrentIndex(offset);");
                                }
                                sb.AppendLine("            reader.ReadFreshValue(ref " + fieldTarget4 + ");");
                                if (canUseLocalOffset)
                                {
                                    sb.AppendLine("            offset = reader.GetCurrentSpanIndex();");
                                }
                                break;
                            }
                            if (IsStringList(nestedField))
                            {
                                sb.AppendLine("            " + fieldTarget4 + " = reader.ReadStringListAndAdvance(ref offset)!;");
                                break;
                            }
                            bool useCollectionFormatter3 = nestedField.Type == LuminFiledType.List || IsReferenceGenericType(nestedField.GenericType.FirstOrDefault()) || !nestedField.IsCompress;
                            if (useCollectionFormatter3)
                            {
                                sb.AppendLine("            if (!reader.TryReadCollectionHead(ref offset, out int " + nestedField.Name + "Count_0))");
                            }
                            else
                            {
                                sb.AppendLine("            if (reader.PeekIsNullCollection(ref offset))");
                            }
                            sb.AppendLine("            {");
                            sb.AppendLine("                offset += 4;");
                            sb.AppendLine("                " + fieldTarget4 + " = default;");
                            sb.AppendLine("            }");
                            sb.AppendLine("            else");
                            sb.AppendLine("            {");
                            GenerateDeserializeCode(sb, nestedField, fieldTarget4, "span", "offset", 4, 0, isFirst: true, "", isArray: false, isList: false, "_", isPrivateFiled: false, "", useCollectionFormatter3);
                            if (nestedField.Type != LuminFiledType.String)
                            {
                                sb.AppendLine("                offset = " + GetFieldLength(nestedField, 0, "_", "reader") + ";");
                            }
                            sb.AppendLine("            }");
                            break;
                        }
                    case LuminFiledType.String:
                        GenerateDeserializeCode(sb, nestedField, fieldTarget4, "span", "offset", 4, 0);
                        break;
                    default:
                        {
                            sb.AppendLine("            if (reader.PeekIsNullObject(ref offset))");
                            sb.AppendLine("            {");
                            sb.AppendLine("                offset += 1;");
                            sb.AppendLine("                " + fieldTarget4 + " = default;");
                            sb.AppendLine("            }");
                            sb.AppendLine("            else");
                            sb.AppendLine("            {");
                            GenerateDeserializeCode(sb, nestedField, fieldTarget4, "span", "offset", 4, 0);
                            LuminFiledType type = nestedField.Type;
                            if (type != LuminFiledType.String && type != LuminFiledType.Class)
                            {
                                sb.AppendLine("                offset += " + GetFieldLength(nestedField, 0, "_", "reader") + ";");
                            }
                            sb.AppendLine("            }");
                            break;
                        }
                }
                continue;
            }
            if (IsMergeableField(data.fields[fieldIndex]))
            {
                int mergeEndIndex2 = FindNextMergeableField(data, fieldIndex);
                if (mergeEndIndex2 != fieldIndex)
                {
                    if (mergeEndIndex2 - fieldIndex >= 14)
                    {
                        mergeEndIndex2 = fieldIndex + 14;
                    }
                    if (TryGenerateFixedUnmanagedRead(sb, data.fields, fieldIndex, mergeEndIndex2,
                        member => canConstructDirectly && CanDeserializeDirectlyIntoConstructedObject(member) ? "result." + member.Identifier : member.Name + "Temp",
                        "offset", "            "))
                    {
                        fieldIndex = mergeEndIndex2;
                        continue;
                    }
                    sb.Append("            offset += reader.ReadUnmanaged(ref offset, ");
                    for (int fieldIndex3 = fieldIndex; fieldIndex3 <= mergeEndIndex2; fieldIndex3++)
                    {
                        LuminDataField nestedField2 = data.fields[fieldIndex3];
                        string fieldTarget5 = (canConstructDirectly && CanDeserializeDirectlyIntoConstructedObject(nestedField2)) ? ("result." + nestedField2.Identifier) : (nestedField2.Name + "Temp");
                        if (fieldIndex3 == fieldIndex)
                        {
                            sb.Append("out " + fieldTarget5);
                        }
                        else
                        {
                            sb.Append(", out " + fieldTarget5);
                        }
                    }
                    sb.Append(");");
                    sb.AppendLine();
                    fieldIndex = mergeEndIndex2;
                    continue;
                }
            }
            bool previousFieldUsesFormatter4 = fieldIndex > 0 && data.fields[fieldIndex - 1].Type == LuminFiledType.Other;
            if (canUseLocalOffset && nestedField.Type == LuminFiledType.Other && !previousFieldUsesFormatter4)
            {
                sb.AppendLine("            reader.FlushCurrentIndex(offset);");
            }
            GenerateDeserializeCode(sb, nestedField, fieldTarget4, "span", "offset", 3, 0, isFirst: true, "", isArray: false, isList: false, "_", isPrivateFiled: false, "", collectionHeadAlreadyRead: false, canConstructDirectly && CanDeserializeDirectlyIntoConstructedObject(nestedField));
            bool nextFieldUsesFormatter5 = fieldIndex + 1 < data.fields.Count && data.fields[fieldIndex + 1].Type == LuminFiledType.Other;
            if (canUseLocalOffset && nestedField.Type == LuminFiledType.Other && !nextFieldUsesFormatter5 && fieldIndex + 1 < data.fields.Count)
            {
                sb.AppendLine("            offset = reader.GetCurrentSpanIndex();");
            }
            LuminFiledType type2 = nestedField.Type;
            if (type2 != LuminFiledType.String && type2 != LuminFiledType.Struct && type2 != LuminFiledType.Other)
            {
                sb.AppendLine("            offset += " + GetFieldLength(nestedField, 0, "_", "reader") + ";");
            }
        }
        sb.AppendLine();
        sb.AppendLine("            // Construct the object and populate its members.");
        if (canUseLocalOffset && (data.fields.Count == 0 || data.fields[data.fields.Count - 1].Type != LuminFiledType.Other))
        {
            sb.AppendLine("            reader.FlushCurrentIndex(offset);");
        }
        if (canConstructDirectly)
        {
            if (data.isValueType || polymorphism)
            {
                sb.AppendLine("            value = result;");
            }
            else
            {
                // Deserialize-into-existing: when a root instance is supplied, preserve its identity and
                // commit members into it only on success so a parse failure never publishes a partially
                // initialized object into value.
                sb.AppendLine("            if (value is null)");
                sb.AppendLine("            {");
                sb.AppendLine("                value = result;");
                sb.AppendLine("            }");
                foreach (LuminDataField field in data.fields.Where(static field => CanDeserializeDirectlyIntoConstructedObject(field)))
                {
                    sb.AppendLine("                value." + field.Identifier + " = result." + field.Identifier + ";");
                }
            }
            if (data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
            {
                if (data.isValueType)
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
                }
                else
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
                }
                foreach (LuminDataField field in data.fields.Where(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
                {
                    sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
                }
            }
        }
        else
        {
            List<string> constructorArgumentExpressions = new List<string>();
            if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
            {
                foreach (ConstructorParameter param in data.SelectedConstructor.Parameters)
                {
                    LuminDataField nestedField3 = data.fields.FirstOrDefault((LuminDataField f) => f.Name == param.MatchingFieldName);
                    if (nestedField3 != null)
                    {
                        constructorArgumentExpressions.Add(nestedField3.Name + "Temp!");
                    }
                    else
                    {
                        constructorArgumentExpressions.Add("default");
                    }
                }
            }
            string constructorArguments = string.Join(", ", constructorArgumentExpressions);
            List<LuminDataField> publicFieldAssignments = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && !f.IsPrivate && !f.isProperty).ToList();
            List<LuminDataField> privateFieldAssignments = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && (f.IsPrivate || f.isProperty)).ToList();
            if (data.RentPoolMethod != null)
            {
                if (data.RentPoolMethod.ReturnsByRef)
                {
                    sb.AppendLine("            value = ref " + concreteTypeName + "." + ((ISymbol)data.RentPoolMethod).Name + "()!;");
                }
                else
                {
                    sb.AppendLine("            value = " + concreteTypeName + "." + ((ISymbol)data.RentPoolMethod).Name + "();");
                }
                sb.AppendLine("            // Populate every field on the pooled instance.");
                if (data.isValueType)
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
                }
                else
                {
                    sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
                }
                foreach (LuminDataField field in data.fields)
                {
                    sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
                }
            }
            else
            {
                if (publicFieldAssignments.Count > 0)
                {
                    if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                    {
                        sb.AppendLine("            value = new " + concreteTypeName + "(" + constructorArguments + ")");
                    }
                    else
                    {
                        sb.AppendLine("            value = new " + concreteTypeName + "()");
                    }
                    sb.AppendLine("            {");
                    foreach (LuminDataField item2 in publicFieldAssignments)
                    {
                        sb.AppendLine("                " + item2.Identifier + " = " + item2.Name + "Temp!,");
                    }
                    sb.AppendLine("            };");
                }
                else if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                {
                    sb.AppendLine("            value = new " + concreteTypeName + "(" + constructorArguments + ");");
                }
                else
                {
                    sb.AppendLine("            value = new " + concreteTypeName + "();");
                }
                if (privateFieldAssignments.Count > 0)
                {
                    sb.AppendLine("            // Assign private fields.");
                    if (data.isValueType)
                    {
                        sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
                    }
                    else
                    {
                        sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
                    }
                    foreach (LuminDataField item3 in privateFieldAssignments)
                    {
                        sb.AppendLine("            local." + item3.Identifier + " = " + item3.Name + "Temp!;");
                    }
                }
            }
        }
        foreach (var item4 in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item4.Item3 ? ("            " + concreteTypeName + "." + item4.Item1 + "();") : ("            value?." + item4.Item1 + "();"));
        }
        sb.AppendLine();
    }
}
