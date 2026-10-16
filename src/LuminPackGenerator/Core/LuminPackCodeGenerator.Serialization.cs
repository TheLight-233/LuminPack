using System;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="fieldPath">The expression that accesses the field in generated code.</param>
    /// <param name="span">The buffer used by this operation.</param>
    /// <param name="offset">The position at which the operation starts.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <param name="isMultClass">The is mult class used by this operation.</param>
    /// <param name="stringRecordLength">The expression that yields the current string header length.</param>
    private static void GenerateSerializeCode(StringBuilder sb, LuminDataField field, string fieldPath, string span, string offset, int indent, int depth, bool isMultClass = false, string stringRecordLength = "writer.StringRecordLength()")
    {
        string indentation = new string(' ', indent * 4);
        string depthSuffix = $"_{depth}";
        switch (field.Type)
        {
            case LuminFiledType.Byte:
            case LuminFiledType.SByte:
                sb.AppendLine(indentation + "writer.GetSpanReference(" + offset + ") = " + fieldPath + ";");
                if (isMultClass)
                {
                    sb.AppendLine(indentation + offset + " += 1;");
                }
                break;
            case LuminFiledType.Bool:
                if (isMultClass)
                {
                    sb.AppendLine(indentation + offset + " += writer.WriteUnmanaged(ref " + offset + ", " + fieldPath + ");");
                }
                else
                {
                    sb.AppendLine(indentation + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
                }
                break;
            case LuminFiledType.Short:
            case LuminFiledType.UShort:
            case LuminFiledType.Char:
                sb.AppendLine(indentation + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
                if (isMultClass)
                {
                    sb.AppendLine(indentation + offset + " += 2;");
                }
                break;
            case LuminFiledType.Int:
            case LuminFiledType.UInt:
            case LuminFiledType.Float:
                sb.AppendLine(indentation + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
                if (isMultClass)
                {
                    sb.AppendLine(indentation + offset + " += 4;");
                }
                break;
            case LuminFiledType.Long:
            case LuminFiledType.ULong:
            case LuminFiledType.Double:
                sb.AppendLine(indentation + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
                if (isMultClass)
                {
                    sb.AppendLine(indentation + offset + " += 8;");
                }
                break;
            case LuminFiledType.Decimal:
                sb.AppendLine(indentation + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
                if (isMultClass)
                {
                    sb.AppendLine(indentation + offset + " += 16;");
                }
                break;
            case LuminFiledType.String:
                sb.AppendLine(indentation + "var " + field.Name + "Length = writer.WriteString(ref " + offset + ", " + fieldPath + ");");
                if (isMultClass)
                {
                    sb.AppendLine(indentation + offset + " += " + field.Name + "Length + " + stringRecordLength + ";");
                }
                break;
            case LuminFiledType.List:
                {
                    if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
                    {
                        if (field.IsCompress)
                        {
                            sb.AppendLine(indentation + "var " + field.Name + "ListTempValue" + depthSuffix + " = " + fieldPath + ";");
                            sb.AppendLine(indentation + "var " + field.Name + "ListTempSpan" + depthSuffix + " = LuminPackMarshal.GetListSpan(" + field.Name + "ListTempValue" + depthSuffix + "!);");
                            sb.AppendLine(indentation + "writer.DangerousWriteUnmanagedSpanWithCompress(ref " + offset + ", " + field.Name + "ListTempSpan" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "var " + field.Name + "ListTempValue" + depthSuffix + " = " + fieldPath + ";");
                            sb.AppendLine(indentation + "writer.WriteUnmanagedList(ref " + offset + ", ref " + field.Name + "ListTempValue" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        sb.AppendLine(indentation + "var " + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + " + field.Name + "TempLength" + depthSuffix + ";");
                        if (isMultClass)
                        {
                            sb.AppendLine(indentation + offset + " = " + field.Name + "ListOffset" + depthSuffix + ";");
                        }
                        break;
                    }
                    if (depth > 0)
                    {
                        sb.AppendLine(indentation + "var " + field.Name + "v" + depthSuffix + " = " + fieldPath + ";");
                        sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = writer.WriteListHeaderAndGetSpan(ref " + offset + ", " + field.Name + "v" + depthSuffix + "!);");
                    }
                    else
                    {
                        sb.AppendLine(indentation + "var " + field.Name + "TempSpan" + depthSuffix + " = writer.WriteListHeaderAndGetSpan(ref " + offset + ", " + fieldPath + "!);");
                    }
                    sb.AppendLine(indentation + "int " + field.Name + "Count" + depthSuffix + " = " + field.Name + "TempSpan" + depthSuffix + ".Length;");
                    sb.AppendLine(indentation + "int " + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + 4;");
                    LuminDataField elementField4 = new LuminDataField
                    {
                        Type = ConvertGenericsToFieldType(field.GenericType[0]),
                        ClassName = field.ClassName,
                        ClassFields = field.ClassFields,
                        ClassGenericType = field.ClassGenericType,
                        GenericType = field.GenericType.Skip(1).ToList(),
                        IsCompress = field.IsCompress
                    };
                    if (IsPureValueTypeStruct(elementField4))
                    {
                        if (field.IsCompress)
                        {
                            sb.AppendLine(indentation + "writer.DangerousWriteUnmanagedSpanWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + depthSuffix + ", " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "writer.WriteUnmanagedSpanWithOutHeader(ref " + offset + ", " + field.Name + "TempSpan" + depthSuffix + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " += " + field.Name + "TempLength" + depthSuffix + ";");
                        break;
                    }
                    sb.AppendLine(indentation + "foreach (ref var v" + depthSuffix + " in " + field.Name + "TempSpan" + depthSuffix + ")");
                    sb.AppendLine(indentation + "{");
                    if (elementField4.Type == LuminFiledType.String)
                    {
                        sb.AppendLine(indentation + "    if (v" + depthSuffix + " == null)");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += " + stringRecordLength + ";");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                        sb.AppendLine(indentation + "    int strLen_" + depthSuffix + " = writer.WriteString(ref " + field.Name + "ListOffset" + depthSuffix + ", v" + depthSuffix + ");");
                        sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += strLen_" + depthSuffix + " + " + stringRecordLength + ";");
                        if (isMultClass)
                        {
                            sb.AppendLine(indentation + "    " + offset + " = " + field.Name + "ListOffset" + depthSuffix + ";");
                        }
                    }
                    else
                    {
                        LuminFiledType type = elementField4.Type;
                        if (type - 15 <= LuminFiledType.UInt)
                        {
                            if (IsPureValueTypeStruct(elementField4))
                            {
                                sb.AppendLine(indentation + "    // Unmanaged struct");
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + depthSuffix + ", v" + depthSuffix + ");");
                                sb.AppendLine(indentation + "}");
                                break;
                            }
                            if (elementField4.Type == LuminFiledType.Class)
                            {
                                sb.AppendLine(indentation + "    var element = v" + depthSuffix + ";");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "    ref var element = ref v" + depthSuffix + ";");
                            }
                            if (elementField4.Type == LuminFiledType.Class)
                            {
                                sb.AppendLine(indentation + "    if (element == null)");
                                sb.AppendLine(indentation + "    {");
                                sb.AppendLine(indentation + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                                sb.AppendLine(indentation + "        continue;");
                                sb.AppendLine(indentation + "    }");
                            }
                            int fieldIndex4 = 0;
                            if (elementField4.ClassFields.Count > 0 && IsMergeableField(elementField4.ClassFields[0]))
                            {
                                int mergeEndIndex5 = Math.Min(FindNextMergeableField(elementField4.ClassFields, 0), 13);
                                if (!TryGenerateFixedUnmanagedWrite(sb, elementField4.ClassFields, 0, mergeEndIndex5,
                                    member => "element." + member.Identifier, field.Name + "ListOffset" + depthSuffix,
                                    indentation + "    ", advanceWriter: false, writeHeader: true))
                                {
                                    sb.Append($"{indentation}    {field.Name}ListOffset{depthSuffix} += writer.WriteUnmanaged(ref {field.Name}ListOffset{depthSuffix}, (byte){elementField4.ClassFields.Count}");
                                    for (int l = 0; l <= mergeEndIndex5; l++)
                                    {
                                        LuminDataField nestedField5 = elementField4.ClassFields[l];
                                        if (nestedField5.IsPrivate)
                                        {
                                            sb.Append(", Get" + field.Name + nestedField5.Name + "(element)");
                                        }
                                        else
                                        {
                                            sb.Append(", element." + nestedField5.Name);
                                        }
                                    }
                                    sb.AppendLine(");");
                                }
                                fieldIndex4 = mergeEndIndex5 + 1;
                            }
                            else
                            {
                                sb.AppendLine($"{indentation}    writer.WriteObjectHeader(ref {field.Name}ListOffset{depthSuffix}, {elementField4.ClassFields.Count});");
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                            }
                            sb.AppendLine();
                            for (int m = fieldIndex4; m < elementField4.ClassFields.Count; m++)
                            {
                                LuminDataField nestedField6 = elementField4.ClassFields[m];
                                if (nestedField6.FieldType == LuminDataType.Reference && nestedField6.Type != LuminFiledType.Other)
                                {
                                    string fieldVariable6 = "serialize" + nestedField6.Name + "Field" + depthSuffix;
                                    if (nestedField6.IsPrivate)
                                    {
                                        sb.AppendLine(indentation + "    if (Get" + field.Name + nestedField6.Name + "(element) is { } " + fieldVariable6 + ")");
                                    }
                                    else
                                    {
                                        sb.AppendLine(indentation + "    if (element." + nestedField6.Identifier + " is { } " + fieldVariable6 + ")");
                                    }
                                    sb.AppendLine(indentation + "    {");
                                    GenerateSerializeCode(sb, nestedField6, fieldVariable6, span, field.Name + "ListOffset" + depthSuffix, indent + 2, depth + 1, isMultClass: true, stringRecordLength);
                                    sb.AppendLine(indentation + "    }");
                                    sb.AppendLine(indentation + "    else");
                                    sb.AppendLine(indentation + "    {");
                                    switch (nestedField6.Type)
                                    {
                                        case LuminFiledType.List:
                                        case LuminFiledType.Array:
                                            sb.AppendLine(indentation + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                            sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                                            break;
                                        case LuminFiledType.String:
                                            sb.AppendLine(indentation + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                            sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += " + stringRecordLength + ";");
                                            break;
                                        default:
                                            sb.AppendLine(indentation + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                            sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                                            break;
                                    }
                                    sb.AppendLine(indentation + "    }");
                                    continue;
                                }
                                if (IsMergeableField(nestedField6))
                                {
                                    int mergeEndIndex6 = FindNextMergeableField(elementField4.ClassFields, m);
                                    if (mergeEndIndex6 != m)
                                    {
                                        if (mergeEndIndex6 - m >= 14)
                                        {
                                            mergeEndIndex6 = m + 14;
                                        }
                                        if (TryGenerateFixedUnmanagedWrite(sb, elementField4.ClassFields, m, mergeEndIndex6,
                                            member => "element." + member.Identifier, field.Name + "ListOffset" + depthSuffix,
                                            indentation + "    ", advanceWriter: false))
                                        {
                                            m = mergeEndIndex6;
                                            continue;
                                        }
                                        sb.Append(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + depthSuffix);
                                        for (int n = m; n <= mergeEndIndex6; n++)
                                        {
                                            if (elementField4.ClassFields[n].IsPrivate)
                                            {
                                                sb.Append(", Get" + field.Name + elementField4.ClassFields[n].Name + "(element)");
                                            }
                                            else
                                            {
                                                sb.Append(", element." + elementField4.ClassFields[n].Name);
                                            }
                                        }
                                        sb.Append(");");
                                        sb.AppendLine();
                                        m = mergeEndIndex6;
                                        continue;
                                    }
                                }
                                if (nestedField6.IsPrivate)
                                {
                                    GenerateSerializeCode(sb, nestedField6, "Get" + field.Name + nestedField6.Name + "(element)", span, field.Name + "ListOffset" + depthSuffix, indent + 2, depth + 1, isMultClass: true, stringRecordLength);
                                }
                                else
                                {
                                    GenerateSerializeCode(sb, nestedField6, "element." + nestedField6.Identifier, span, field.Name + "ListOffset" + depthSuffix, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
                                }
                            }
                        }
                        else
                        {
                            type = elementField4.Type;
                            if (type - 17 <= LuminFiledType.UInt)
                            {
                                sb.AppendLine(indentation + "    if (v" + depthSuffix + " == null)");
                                sb.AppendLine(indentation + "    {");
                                sb.AppendLine(indentation + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                                sb.AppendLine(indentation + "        continue;");
                                sb.AppendLine(indentation + "    }");
                            }
                            GenerateSerializeCode(sb, elementField4, "v" + depthSuffix, span, field.Name + "ListOffset" + depthSuffix, indent + 1, depth + 1, isMultClass: false, stringRecordLength);
                            type = elementField4.Type;
                            if (type - 17 <= LuminFiledType.UInt)
                            {
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " = " + GetSerializeFieldLength(elementField4, depth + 1, stringRecordLength) + ";");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += " + GetSerializeFieldLength(elementField4, depth + 1, stringRecordLength) + ";");
                            }
                        }
                    }
                    sb.AppendLine(indentation + "}");
                    break;
                }
            case LuminFiledType.Array:
                {
                    if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
                    {
                        if (field.IsCompress)
                        {
                            sb.AppendLine(indentation + "writer.DangerousWriteUnmanagedArrayWithCompress(ref " + offset + ", " + fieldPath + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "writer.EnsureAdditionalCapacity(checked(4 + global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(" + fieldPath + ".AsSpan()).Length));");
                            sb.AppendLine(indentation + "writer.DangerousWriteUnmanagedArray(ref " + offset + ", " + fieldPath + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        sb.AppendLine(indentation + "var " + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + " + field.Name + "TempLength" + depthSuffix + ";");
                        if (isMultClass)
                        {
                            sb.AppendLine(indentation + offset + " = " + field.Name + "ListOffset" + depthSuffix + ";");
                        }
                        break;
                    }
                    LuminDataField elementField = new LuminDataField
                    {
                        Type = ConvertGenericsToFieldType(field.GenericType[0]),
                        ClassName = field.ClassName,
                        ClassFields = field.ClassFields,
                        ClassGenericType = field.ClassGenericType,
                        GenericType = field.GenericType.Skip(1).ToList(),
                        IsCompress = field.IsCompress
                    };
                    sb.AppendLine(indentation + "int " + field.Name + "Count" + depthSuffix + " = " + fieldPath + ".Length;");
                    sb.AppendLine(indentation + "writer.WriteCollectionHeader(ref " + offset + ", " + field.Name + "Count" + depthSuffix + ");");
                    sb.AppendLine(indentation + "int " + field.Name + "ListOffset" + depthSuffix + " = " + offset + " + 4;");
                    if (IsPureValueTypeStruct(elementField))
                    {
                        if (field.IsCompress)
                        {
                            sb.AppendLine(indentation + "writer.DangerousWriteUnmanagedArrayWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + depthSuffix + ", " + fieldPath + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        else
                        {
                            sb.AppendLine(indentation + "writer.WriteUnmanagedArrayWithOutHeader(ref " + offset + ", " + fieldPath + ", " + field.Name + "Count" + depthSuffix + ", out var " + field.Name + "TempLength" + depthSuffix + ");");
                        }
                        sb.AppendLine(indentation + field.Name + "ListOffset" + depthSuffix + " += " + field.Name + "TempLength" + depthSuffix + ";");
                        break;
                    }
                    sb.AppendLine(indentation + "for (int i" + depthSuffix + " = 0; i" + depthSuffix + " < " + field.Name + "Count" + depthSuffix + "; i" + depthSuffix + "++)");
                    sb.AppendLine(indentation + "{");
                    sb.AppendLine(indentation + "    ref var v" + depthSuffix + " = ref " + fieldPath + "[i" + depthSuffix + "];");
                    if (elementField.Type == LuminFiledType.String)
                    {
                        sb.AppendLine(indentation + "    if (v" + depthSuffix + " == null)");
                        sb.AppendLine(indentation + "    {");
                        sb.AppendLine(indentation + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                        sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += " + stringRecordLength + ";");
                        sb.AppendLine(indentation + "        continue;");
                        sb.AppendLine(indentation + "    }");
                        sb.AppendLine(indentation + "    int strLen_" + depthSuffix + " = writer.WriteString(ref " + field.Name + "ListOffset" + depthSuffix + ", v" + depthSuffix + ");");
                        sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += strLen_" + depthSuffix + " + " + stringRecordLength + ";");
                        if (isMultClass)
                        {
                            sb.AppendLine(indentation + "    " + offset + " = " + field.Name + "ListOffset" + depthSuffix + ";");
                        }
                    }
                    else
                    {
                        LuminFiledType type = elementField.Type;
                        if (type - 15 <= LuminFiledType.UInt)
                        {
                            if (IsPureValueTypeStruct(elementField))
                            {
                                sb.AppendLine(indentation + "    // Unmanaged struct");
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + depthSuffix + ", v" + depthSuffix + ");");
                                sb.AppendLine(indentation + "}");
                                break;
                            }
                            string elementExpression = "v" + depthSuffix;
                            if (elementField.Type == LuminFiledType.Class)
                            {
                                string elementVariable4 = "element" + depthSuffix;
                                sb.AppendLine(indentation + "    var " + elementVariable4 + " = v" + depthSuffix + ";");
                                elementExpression = elementVariable4;
                                sb.AppendLine(indentation + "    if (" + elementExpression + " == null)");
                                sb.AppendLine(indentation + "    {");
                                sb.AppendLine(indentation + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                                sb.AppendLine(indentation + "        continue;");
                                sb.AppendLine(indentation + "    }");
                            }
                            int fieldIndex = 0;
                            if (elementField.ClassFields.Count > 0 && IsMergeableField(elementField.ClassFields[0]))
                            {
                                int mergeEndIndex2 = Math.Min(FindNextMergeableField(elementField.ClassFields, 0), 13);
                                if (!TryGenerateFixedUnmanagedWrite(sb, elementField.ClassFields, 0, mergeEndIndex2,
                                    member => elementExpression + "." + member.Identifier, field.Name + "ListOffset" + depthSuffix,
                                    indentation + "    ", advanceWriter: false, writeHeader: true))
                                {
                                    sb.Append($"{indentation}    {field.Name}ListOffset{depthSuffix} += writer.WriteUnmanaged(ref {field.Name}ListOffset{depthSuffix}, (byte){elementField.ClassFields.Count}");
                                    for (int i = 0; i <= mergeEndIndex2; i++)
                                    {
                                        LuminDataField nestedField2 = elementField.ClassFields[i];
                                        if (nestedField2.IsPrivate)
                                        {
                                            sb.Append(", Get" + field.Name + nestedField2.Name + "(" + elementExpression + ")");
                                        }
                                        else
                                        {
                                            sb.Append(", " + elementExpression + "." + nestedField2.Name);
                                        }
                                    }
                                    sb.AppendLine(");");
                                }
                                fieldIndex = mergeEndIndex2 + 1;
                            }
                            else
                            {
                                sb.AppendLine($"{indentation}    writer.WriteObjectHeader(ref {field.Name}ListOffset{depthSuffix}, {elementField.ClassFields.Count});");
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                            }
                            sb.AppendLine();
                            for (int j = fieldIndex; j < elementField.ClassFields.Count; j++)
                            {
                                LuminDataField nestedField3 = elementField.ClassFields[j];
                                if (nestedField3.FieldType == LuminDataType.Reference && nestedField3.Type != LuminFiledType.Other)
                                {
                                    string fieldVariable5 = "serialize" + nestedField3.Name + "Field" + depthSuffix;
                                    if (nestedField3.IsPrivate)
                                    {
                                        sb.AppendLine(indentation + "    if (Get" + field.Name + nestedField3.Name + "(" + elementExpression + ") is { } " + fieldVariable5 + ")");
                                    }
                                    else
                                    {
                                        sb.AppendLine(indentation + "    if (" + elementExpression + "." + nestedField3.Identifier + " is { } " + fieldVariable5 + ")");
                                    }
                                    sb.AppendLine(indentation + "    {");
                                    GenerateSerializeCode(sb, nestedField3, fieldVariable5, span, field.Name + "ListOffset" + depthSuffix, indent + 2, depth + 1, isMultClass: true, stringRecordLength);
                                    sb.AppendLine(indentation + "    }");
                                    sb.AppendLine(indentation + "    else");
                                    sb.AppendLine(indentation + "    {");
                                    switch (nestedField3.Type)
                                    {
                                        case LuminFiledType.List:
                                        case LuminFiledType.Array:
                                            sb.AppendLine(indentation + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                            sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                                            break;
                                        case LuminFiledType.String:
                                            sb.AppendLine(indentation + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                            sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += " + stringRecordLength + ";");
                                            break;
                                        default:
                                            sb.AppendLine(indentation + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                            sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 1;");
                                            break;
                                    }
                                    sb.AppendLine(indentation + "    }");
                                    continue;
                                }
                                if (IsMergeableField(nestedField3))
                                {
                                    int mergeEndIndex3 = FindNextMergeableField(elementField.ClassFields, j);
                                    if (mergeEndIndex3 != j)
                                    {
                                        if (mergeEndIndex3 - j >= 14)
                                        {
                                            mergeEndIndex3 = j + 14;
                                        }
                                        if (TryGenerateFixedUnmanagedWrite(sb, elementField.ClassFields, j, mergeEndIndex3,
                                            member => elementExpression + "." + member.Identifier, field.Name + "ListOffset" + depthSuffix,
                                            indentation + "    ", advanceWriter: false))
                                        {
                                            j = mergeEndIndex3;
                                            continue;
                                        }
                                        sb.Append(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + depthSuffix);
                                        for (int k = j; k <= mergeEndIndex3; k++)
                                        {
                                            if (elementField.ClassFields[k].IsPrivate)
                                            {
                                                sb.Append(", Get" + field.Name + elementField.ClassFields[k].Name + "(" + elementExpression + ")");
                                            }
                                            else
                                            {
                                                sb.Append(", " + elementExpression + "." + elementField.ClassFields[k].Name);
                                            }
                                        }
                                        sb.Append(");");
                                        sb.AppendLine();
                                        j = mergeEndIndex3;
                                        continue;
                                    }
                                }
                                if (nestedField3.IsPrivate)
                                {
                                    GenerateSerializeCode(sb, nestedField3, "Get" + field.Name + nestedField3.Name + "(" + elementExpression + ")", span, field.Name + "ListOffset" + depthSuffix, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
                                }
                                else
                                {
                                    GenerateSerializeCode(sb, nestedField3, elementExpression + "." + nestedField3.Identifier, span, field.Name + "ListOffset" + depthSuffix, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
                                }
                            }
                        }
                        else
                        {
                            type = elementField.Type;
                            if (type - 17 <= LuminFiledType.UInt)
                            {
                                sb.AppendLine(indentation + "    if (v" + depthSuffix + " == null)");
                                sb.AppendLine(indentation + "    {");
                                sb.AppendLine(indentation + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + depthSuffix + ");");
                                sb.AppendLine(indentation + "        " + field.Name + "ListOffset" + depthSuffix + " += 4;");
                                sb.AppendLine(indentation + "        continue;");
                                sb.AppendLine(indentation + "    }");
                            }
                            GenerateSerializeCode(sb, elementField, "v" + depthSuffix, span, field.Name + "ListOffset" + depthSuffix, indent + 1, depth + 1, isMultClass: false, stringRecordLength);
                            type = elementField.Type;
                            if (type - 17 <= LuminFiledType.UInt)
                            {
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " = " + GetSerializeFieldLength(elementField, depth + 1, stringRecordLength) + ";");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "    " + field.Name + "ListOffset" + depthSuffix + " += " + GetSerializeFieldLength(elementField, depth + 1, stringRecordLength) + ";");
                            }
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
                        sb.AppendLine(indentation + "// Unmanaged struct " + field.ClassName + ": write the complete storage representation");
                        sb.AppendLine(indentation + "writer.Advance(writer.WriteUnmanaged(ref " + offset + ", " + fieldPath + "));");
                        break;
                    }
                    sb.AppendLine(indentation + "// Serialize" + field.ClassName);
                    string valueExpression7 = fieldPath;
                    if (field.Type == LuminFiledType.Class && field.ClassFields.Count > 1)
                    {
                        string fieldVariable8 = "serialize" + field.Name + "Value" + depthSuffix;
                        sb.AppendLine(indentation + "var " + fieldVariable8 + " = " + fieldPath + ";");
                        valueExpression7 = fieldVariable8;
                    }
                    sb.AppendLine($"{indentation}writer.WriteObjectHeader(ref {offset}, {field.ClassFields.Count});");
                    sb.AppendLine(indentation + offset + " += 1;");
                    sb.AppendLine();
                    for (int fieldIndex7 = 0; fieldIndex7 < field.ClassFields.Count; fieldIndex7++)
                    {
                        LuminDataField nestedField7 = field.ClassFields[fieldIndex7];
                        if (nestedField7.FieldType == LuminDataType.Reference && nestedField7.Type != LuminFiledType.Other)
                        {
                            string fieldVariable9 = "serialize" + nestedField7.Name + "Field" + depthSuffix;
                            if (nestedField7.IsPrivate)
                            {
                                sb.AppendLine(indentation + "if (Get" + field.Name + nestedField7.Name + "(" + valueExpression7 + ") is { } " + fieldVariable9 + ")");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "if (" + valueExpression7 + "." + nestedField7.Identifier + " is { } " + fieldVariable9 + ")");
                            }
                            sb.AppendLine(indentation + "{");
                            GenerateSerializeCode(sb, nestedField7, fieldVariable9, span, offset, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
                            LuminFiledType type = nestedField7.Type;
                            if (type - 17 <= LuminFiledType.UInt)
                            {
                                string elementTypeName = $"_{depth + 1}";
                                sb.AppendLine(indentation + "    writer.FlushCurrentIndex(" + nestedField7.Name + "ListOffset" + elementTypeName + ");");
                            }
                            sb.AppendLine(indentation + "}");
                            switch (nestedField7.Type)
                            {
                                case LuminFiledType.List:
                                case LuminFiledType.Array:
                                    sb.AppendLine(indentation + "else");
                                    sb.AppendLine(indentation + "{");
                                    sb.AppendLine(indentation + "    writer.WriteNullCollectionHeader(ref " + offset + ");");
                                    sb.AppendLine(indentation + "    " + offset + " += 4;");
                                    sb.AppendLine(indentation + "}");
                                    break;
                                case LuminFiledType.String:
                                    sb.AppendLine(indentation + "else");
                                    sb.AppendLine(indentation + "{");
                                    sb.AppendLine(indentation + "    writer.WriteNullStringHeader(ref " + offset + ");");
                                    sb.AppendLine(indentation + "    " + offset + " += " + stringRecordLength + ";");
                                    sb.AppendLine(indentation + "}");
                                    break;
                                default:
                                    sb.AppendLine(indentation + "else");
                                    sb.AppendLine(indentation + "{");
                                    sb.AppendLine(indentation + "    writer.WriteNullObjectHeader(ref " + offset + ");");
                                    sb.AppendLine(indentation + "    " + offset + " += 1;");
                                    sb.AppendLine(indentation + "}");
                                    break;
                            }
                            continue;
                        }
                        if (IsMergeableField(nestedField7))
                        {
                            int mergeEndIndex8 = FindNextMergeableField(field.ClassFields, fieldIndex7);
                            if (mergeEndIndex8 != fieldIndex7)
                            {
                                if (mergeEndIndex8 - fieldIndex7 >= 14)
                                {
                                    mergeEndIndex8 = fieldIndex7 + 14;
                                }
                                if (TryGenerateFixedUnmanagedWrite(sb, field.ClassFields, fieldIndex7, mergeEndIndex8,
                                    member => valueExpression7 + "." + member.Identifier, offset, indentation, advanceWriter: false))
                                {
                                    fieldIndex7 = mergeEndIndex8;
                                    continue;
                                }
                                sb.AppendLine(indentation + "{");
                                sb.Append(indentation + "    int __luminWrittenSize = writer.WriteUnmanaged(ref " + offset);
                                for (int fieldIndex9 = fieldIndex7; fieldIndex9 <= mergeEndIndex8; fieldIndex9++)
                                {
                                    if (field.ClassFields[fieldIndex9].IsPrivate)
                                    {
                                        sb.Append(", Get" + field.Name + field.ClassFields[fieldIndex9].Name + "(" + valueExpression7 + ")");
                                    }
                                    else
                                    {
                                        sb.Append(", " + valueExpression7 + "." + field.ClassFields[fieldIndex9].Name);
                                    }
                                }
                                sb.Append(");");
                                sb.AppendLine();
                                sb.AppendLine(indentation + "    " + offset + " += __luminWrittenSize;");
                                sb.AppendLine(indentation + "}");
                                fieldIndex7 = mergeEndIndex8;
                                continue;
                            }
                        }
                        if (nestedField7.IsPrivate)
                        {
                            GenerateSerializeCode(sb, nestedField7, "Get" + field.Name + nestedField7.Name + "(" + valueExpression7 + ")", span, offset, indent, depth + 1, isMultClass: true, stringRecordLength);
                        }
                        else
                        {
                            GenerateSerializeCode(sb, nestedField7, valueExpression7 + "." + nestedField7.Identifier, span, offset, indent, depth + 1, isMultClass: true, stringRecordLength);
                        }
                    }
                    break;
                }
            case LuminFiledType.Enum:
                if (isMultClass)
                {
                    sb.AppendLine(indentation + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", (" + GetEnumTypeName(field.EnumType) + ")" + fieldPath + ");");
                    sb.AppendLine(indentation + offset + " += " + GetEnumFieldLength(field.EnumType) + ";");
                }
                else
                {
                    sb.AppendLine(indentation + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", (" + GetEnumTypeName(field.EnumType) + ")" + fieldPath + ");");
                }
                break;
            default:
                // Formatter calls operate on the writer's current index, while nested collection
                // layouts may still be accumulating their position in a local offset variable.
                // Publish that position before crossing the formatter boundary and copy its
                // resulting position back so the following inline fields continue at the right byte.
                if (field.Type == LuminFiledType.Other)
                {
                    sb.AppendLine(indentation + "writer.FlushCurrentIndex(" + offset + ");");
                }
                if (field.IsCompress)
                {
                    sb.AppendLine(indentation + "writer.WriteValueWithCompress(" + fieldPath + ");");
                }
                else
                {
                    sb.AppendLine(indentation + "writer.WriteValue(" + fieldPath + ");");
                }
                if (field.Type == LuminFiledType.Other)
                {
                    sb.AppendLine(indentation + offset + " = writer.GetCurrentSpanIndex();");
                }
                break;
        }
    }

    /// <summary>Appends binary serialization statements for the analyzed type.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="extension">The extension used by this operation.</param>
    /// <param name="polymorphism">The polymorphism used by this operation.</param>
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb, bool extension = false, bool polymorphism = false)
    {
        string concreteTypeName = data.classFullName;
        if (!concreteTypeName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            concreteTypeName = "global::" + data.classNameSpace + "." + data.classFullName;
        }
        foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
        }
        if (IsPureUnmanagedStruct(data))
        {
            sb.AppendLine("            ref int offset = ref writer.GetCurrentSpanOffset();");
            sb.AppendLine("            writer.Advance(writer.WriteUnmanaged(ref offset, value));");
            foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerialized))
            {
                sb.AppendLine(item.Item3 ? ("            " + concreteTypeName + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
            }
            sb.AppendLine();
            return;
        }
        if (!data.isValueType && !polymorphism)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullObjectHeader();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        bool isUnmanaged = !data.isValueType && !polymorphism && data.fields.Count > 16;
        if (isUnmanaged)
        {
            sb.AppendLine("            var serializeValue = value;");
        }
        bool fieldIndex = data.fields.Count(ContainsSerializedStringRecord) > 1;
        string stringHeaderLength = (fieldIndex ? "serializeStringRecordLength" : "writer.StringRecordLength()");
        if (fieldIndex)
        {
            sb.AppendLine("            int serializeStringRecordLength = writer.StringRecordLength();");
        }
        if (data.fields.Count((LuminDataField x) => x.IsPrivate || x.isProperty) > 0)
        {
            sb.AppendLine(extension ? ("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref Unsafe.AsRef(in value));") : ("            ref var local = ref LuminPackMarshal.As<" + concreteTypeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);"));
        }
        bool isUnmanaged2 = false;
        foreach (LuminDataField field in data.fields)
        {
            if (IsPureValueTypeStruct(field) || IsUnmanagedFiledType(field.Type))
            {
                isUnmanaged2 = true;
                continue;
            }
            isUnmanaged2 = false;
            break;
        }
        if (!polymorphism || !isUnmanaged2 || data.fields.Count == 1)
        {
            sb.AppendLine("            ref int offset = ref writer.GetCurrentSpanOffset();");
        }
        int fieldIndex2 = 0;
        if (!polymorphism)
        {
            if (data.fields.Count > 0 && IsMergeableField(data.fields[0]))
            {
                int mergeEndIndex3 = Math.Min(FindNextMergeableField(data, 0), 13);
                if (!TryGenerateFixedUnmanagedWrite(sb, data.fields, 0, mergeEndIndex3,
                    field => (isUnmanaged ? "serializeValue" : "value") + "." + field.Identifier,
                    "writer.GetCurrentSpanIndex()", "            ", advanceWriter: true, writeHeader: true))
                {
                    sb.Append($"            writer.Advance(writer.WriteUnmanaged((byte){data.fields.Count}");
                    for (int fieldIndex4 = 0; fieldIndex4 <= mergeEndIndex3; fieldIndex4++)
                    {
                        string fieldVariable4 = ((data.fields[fieldIndex4].IsPrivate || data.fields[fieldIndex4].isProperty) ? "local" : (isUnmanaged ? "serializeValue" : "value"));
                        sb.Append(", " + fieldVariable4 + "." + data.fields[fieldIndex4].Identifier);
                    }
                    sb.AppendLine("));");
                }
                fieldIndex2 = mergeEndIndex3 + 1;
            }
            else
            {
                sb.AppendLine($"            writer.WriteObjectHeader(ref offset, {data.fields.Count});");
                sb.AppendLine("            writer.Advance(1);");
            }
        }
        sb.AppendLine();
        for (int fieldIndex5 = fieldIndex2; fieldIndex5 < data.fields.Count; fieldIndex5++)
        {
            string fieldVariable5 = ((data.fields[fieldIndex5].IsPrivate || data.fields[fieldIndex5].isProperty) ? "local" : (isUnmanaged ? "serializeValue" : "value"));
            LuminFiledType type;
            if (data.fields[fieldIndex5].FieldType == LuminDataType.Reference && data.fields[fieldIndex5].Type != LuminFiledType.Other)
            {
                string fieldVariable6 = "serialize" + data.fields[fieldIndex5].Name + "Field";
                sb.AppendLine("            if (" + fieldVariable5 + "." + data.fields[fieldIndex5].Identifier + " is { } " + fieldVariable6 + ")");
                sb.AppendLine("            {");
                GenerateSerializeCode(sb, data.fields[fieldIndex5], fieldVariable6, "span", "offset", 4, 0, isMultClass: false, stringHeaderLength);
                if (data.fields[fieldIndex5].Type != LuminFiledType.Class)
                {
                    string serializeFieldLength = GetSerializeFieldLength(data.fields[fieldIndex5], 0, stringHeaderLength);
                    type = data.fields[fieldIndex5].Type;
                    if (type - 17 <= LuminFiledType.UInt)
                    {
                        sb.AppendLine("                writer.FlushCurrentIndex(" + serializeFieldLength + ");");
                    }
                    else
                    {
                        sb.AppendLine("                writer.AdvanceSafe(" + serializeFieldLength + ");");
                    }
                }
                sb.AppendLine("            }");
                switch (data.fields[fieldIndex5].Type)
                {
                    case LuminFiledType.List:
                    case LuminFiledType.Array:
                        sb.AppendLine("            else");
                        sb.AppendLine("            {");
                        sb.AppendLine("                writer.WriteNullCollectionHeader(ref offset);");
                        sb.AppendLine("                offset += 4;");
                        sb.AppendLine("            }");
                        break;
                    case LuminFiledType.String:
                        sb.AppendLine("            else");
                        sb.AppendLine("            {");
                        sb.AppendLine("                writer.WriteNullStringHeader(ref offset);");
                        sb.AppendLine("                offset += " + stringHeaderLength + ";");
                        sb.AppendLine("            }");
                        break;
                    default:
                        sb.AppendLine("            else");
                        sb.AppendLine("            {");
                        sb.AppendLine("                writer.WriteNullObjectHeader(ref offset);");
                        sb.AppendLine("                offset += 1;");
                        sb.AppendLine("            }");
                        break;
                }
                continue;
            }
            if (IsMergeableField(data.fields[fieldIndex5]))
            {
                int mergeEndIndex6 = FindNextMergeableField(data, fieldIndex5);
                if (mergeEndIndex6 != fieldIndex5)
                {
                    if (mergeEndIndex6 - fieldIndex5 >= 14)
                    {
                        mergeEndIndex6 = fieldIndex5 + 14;
                    }
                    if (TryGenerateFixedUnmanagedWrite(sb, data.fields, fieldIndex5, mergeEndIndex6,
                        field => (isUnmanaged ? "serializeValue" : "value") + "." + field.Identifier,
                        "writer.GetCurrentSpanIndex()", "            ", advanceWriter: true))
                    {
                        fieldIndex5 = mergeEndIndex6;
                        continue;
                    }
                    sb.Append("            writer.Advance(writer.WriteUnmanaged(");
                    for (int fieldIndex7 = fieldIndex5; fieldIndex7 <= mergeEndIndex6; fieldIndex7++)
                    {
                        fieldVariable5 = (data.fields[fieldIndex7].IsPrivate ? "local" : (isUnmanaged ? "serializeValue" : "value"));
                        if (fieldIndex7 == fieldIndex5)
                        {
                            sb.Append(fieldVariable5 + "." + data.fields[fieldIndex7].Identifier);
                        }
                        else
                        {
                            sb.Append(", " + fieldVariable5 + "." + data.fields[fieldIndex7].Identifier);
                        }
                    }
                    sb.Append("));");
                    sb.AppendLine();
                    fieldIndex5 = mergeEndIndex6;
                    continue;
                }
            }
            GenerateSerializeCode(sb, data.fields[fieldIndex5], fieldVariable5 + "." + data.fields[fieldIndex5].Identifier, "span", "offset", 3, 0, isMultClass: false, stringHeaderLength);
            type = data.fields[fieldIndex5].Type;
            if (type != LuminFiledType.Struct && type != LuminFiledType.Other)
            {
                sb.AppendLine("            writer.Advance(" + GetSerializeFieldLength(data.fields[fieldIndex5], 0, stringHeaderLength) + ");");
            }
        }
        foreach (var item2 in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item2.Item3 ? ("            " + concreteTypeName + "." + item2.Item1 + "();") : ("            value?." + item2.Item1 + "();"));
        }
        sb.AppendLine();
    }
}
