using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackCodeGenerator
{

    /// <summary>Appends size evaluation statements for a field and its nested values.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="fieldPath">The expression that accesses the field in generated code.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    /// <param name="depth">The nesting depth used to distinguish generated local variables.</param>
    /// <param name="multList">The prefix used to distinguish nested collection variables.</param>
    private static void GenerateSerializeLengthCode(StringBuilder sb, LuminDataField field, string fieldPath, int indent, int depth, string multList = "_")
    {
        using var optionScope = new CompiledOptionFieldScope(sb, field.Options, field.Type == LuminFiledType.Other && field.TypeSymbol != null ? FormatterTypeName.Get(field.TypeSymbol) : null);
        string indentation = new string(' ', indent * 4);
        string depthSuffix = $"{multList}{depth}";
        switch (field.Type)
        {
            case LuminFiledType.Byte:
            case LuminFiledType.SByte:
            case LuminFiledType.Bool:
                sb.AppendLine(indentation + "totalLength += 1;");
                break;
            case LuminFiledType.Short:
            case LuminFiledType.UShort:
            case LuminFiledType.Char:
                sb.AppendLine(indentation + "totalLength += 2;");
                break;
            case LuminFiledType.Int:
            case LuminFiledType.UInt:
            case LuminFiledType.Float:
                sb.AppendLine(indentation + "totalLength += 4;");
                break;
            case LuminFiledType.Long:
            case LuminFiledType.ULong:
            case LuminFiledType.Double:
                sb.AppendLine(indentation + "totalLength += 8;");
                break;
            case LuminFiledType.Decimal:
                sb.AppendLine(indentation + "totalLength += 16;");
                break;
            case LuminFiledType.String:
                sb.AppendLine(indentation + "var " + field.Name + depthSuffix + "TempValue = " + fieldPath + ";");
                sb.AppendLine(indentation + "totalLength += evaluator.GetStringLength(ref " + field.Name + depthSuffix + "TempValue);");
                break;
            case LuminFiledType.List:
                {
                    LuminFiledType elementFieldType2 = ConvertGenericsToFieldType(field.GenericType[0]);
                    if (elementFieldType2 != LuminFiledType.List && IsFixedLengthType(elementFieldType2))
                    {
                        string fixedFieldLength2 = GetFixedFieldLength(new LuminDataField
                        {
                            Type = elementFieldType2
                        });
                        if (depth > 0)
                        {
                            sb.AppendLine(indentation + "if (" + fieldPath + " == null) continue;");
                        }
                        sb.AppendLine(indentation + "totalLength += 4 + " + fieldPath + ".Count * " + fixedFieldLength2 + ";");
                    }
                    else if (elementFieldType2 - 15 <= LuminFiledType.UInt)
                    {
                        sb.AppendLine(indentation + "totalLength += 4; // Collection length prefix.");
                        if (depth > 0)
                        {
                            sb.AppendLine(indentation + "if (" + fieldPath + " == null) continue;");
                        }
                        if (elementFieldType2 == LuminFiledType.Struct)
                        {
                            ITypeSymbol elementType = EmitterTypeTraits.GetCollectionElementType(field.TypeSymbol);
                            string generatedTypeName = elementType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                ?? GetGeneratedTypeName(field);
                            if (EmitterTypeTraits.TryGetIsUnmanaged(elementType, out bool isUnmanaged))
                            {
                                if (isUnmanaged)
                                {
                                    sb.AppendLine(indentation + "totalLength += Unsafe.SizeOf<" + generatedTypeName + ">() * " + fieldPath + ".Count;");
                                    break;
                                }
                            }
                            else
                            {
                                sb.AppendLine(indentation + "if (!evaluator.IsReferenceOrContainsReferences<" + generatedTypeName + ">())");
                                sb.AppendLine(indentation + "    totalLength += Unsafe.SizeOf<" + generatedTypeName + ">() * " + fieldPath + ".Count;");
                                sb.AppendLine(indentation + "else");
                            }
                        }
                        sb.AppendLine(indentation + "for (int i" + depthSuffix + " = 0; i" + depthSuffix + " < " + fieldPath + ".Count; i" + depthSuffix + "++)");
                        sb.AppendLine(indentation + "{");
                        sb.AppendLine(indentation + "    var element = " + fieldPath + "[i" + depthSuffix + "];");
                        sb.AppendLine(indentation + "    totalLength += 1;");
                        for (int m = 0; m < field.ClassFields.Count; m++)
                        {
                            LuminDataField nestedField3 = field.ClassFields[m];
                            if (nestedField3.FieldType == LuminDataType.Reference && nestedField3.Type != LuminFiledType.Other)
                            {
                                if (nestedField3.IsPrivate)
                                {
                                    sb.AppendLine(indentation + "    if (Get" + field.Name + nestedField3.Name + "(element) != null)");
                                }
                                else
                                {
                                    sb.AppendLine(indentation + "    if (element." + nestedField3.Identifier + " != null)");
                                }
                                sb.AppendLine(indentation + "    {");
                                if (nestedField3.IsPrivate)
                                {
                                    GenerateSerializeLengthCode(sb, nestedField3, "Get" + field.Name + nestedField3.Name + "(element)", indent + 2, depth + 1);
                                }
                                else
                                {
                                    GenerateSerializeLengthCode(sb, nestedField3, "element." + nestedField3.Identifier, indent + 2, depth + 1);
                                }
                                sb.AppendLine(indentation + "    }");
                                switch (nestedField3.Type)
                                {
                                    case LuminFiledType.List:
                                    case LuminFiledType.Array:
                                        sb.AppendLine(indentation + "    else");
                                        sb.AppendLine(indentation + "    {");
                                        sb.AppendLine(indentation + "        totalLength += 4;");
                                        sb.AppendLine(indentation + "    }");
                                        break;
                                    case LuminFiledType.String:
                                        sb.AppendLine(indentation + "    else");
                                        sb.AppendLine(indentation + "    {");
                                        sb.AppendLine(indentation + "        totalLength += evaluator.StringRecordLength();");
                                        sb.AppendLine(indentation + "    }");
                                        break;
                                    default:
                                        sb.AppendLine(indentation + "    else");
                                        sb.AppendLine(indentation + "    {");
                                        sb.AppendLine(indentation + "        totalLength += 1;");
                                        sb.AppendLine(indentation + "    }");
                                        break;
                                }
                                continue;
                            }
                            if (IsMergeableField(nestedField3))
                            {
                                int mergeEndIndex5 = FindNextMergeableField(field.ClassFields, m);
                                if (mergeEndIndex5 != m)
                                {
                                    // A merged group mixes numeric literals with types whose size is only
                                    // known to the runtime, so emit expression terms. Parsing them with
                                    // int.TryParse silently read the SizeOf form as zero.
                                    var mergedSizeTerms6 = new List<string>();
                                    for (int n = m; n <= mergeEndIndex5; n++)
                                    {
                                        mergedSizeTerms6.Add(GetFixedFieldLength(field.ClassFields[n]));
                                    }
                                    sb.Append($"{indentation}    totalLength += {string.Join(" + ", mergedSizeTerms6)};");
                                    sb.AppendLine();
                                    m = mergeEndIndex5;
                                    continue;
                                }
                            }
                            GenerateSerializeLengthCode(sb, nestedField3, "element." + nestedField3.Identifier, indent + 1, depth + 1);
                        }
                        sb.AppendLine(indentation + "}");
                    }
                    else
                    {
                        sb.AppendLine(indentation + "totalLength += 4;");
                        if (depth > 0)
                        {
                            sb.AppendLine(indentation + "if (" + fieldPath + " == null) continue;");
                        }
                        sb.AppendLine(indentation + "for (int i" + depthSuffix + " = 0; i" + depthSuffix + " < " + fieldPath + ".Count; i" + depthSuffix + "++)");
                        sb.AppendLine(indentation + "{");
                        LuminDataField field3 = new LuminDataField
                        {
                            Type = elementFieldType2,
                            FieldType = field.FieldType,
                            GenericType = field.GenericType.Skip(1).ToList(),
                            ClassFields = field.ClassFields,
                            ClassName = field.ClassName,
                            ClassGenericType = field.ClassGenericType
                        };
                        if (elementFieldType2 == LuminFiledType.String)
                        {
                            sb.AppendLine(indentation + "    if (" + fieldPath + "[i" + depthSuffix + "] == null)");
                            sb.AppendLine(indentation + "    {");
                            sb.AppendLine(indentation + "        totalLength += evaluator.StringRecordLength();");
                            sb.AppendLine(indentation + "        continue;");
                            sb.AppendLine(indentation + "    }");
                        }
                        GenerateSerializeLengthCode(sb, field3, fieldPath + "[i" + depthSuffix + "]", indent + 1, depth + 1);
                        sb.AppendLine(indentation + "}");
                    }
                    break;
                }
            case LuminFiledType.Array:
                {
                    LuminFiledType elementFieldType = ConvertGenericsToFieldType(field.GenericType[0]);
                    if (elementFieldType != LuminFiledType.Array && IsFixedLengthType(elementFieldType))
                    {
                        string fixedFieldLength = GetFixedFieldLength(new LuminDataField
                        {
                            Type = elementFieldType
                        });
                        if (depth > 0)
                        {
                            sb.AppendLine(indentation + "if (" + fieldPath + " == null) continue;");
                        }
                        sb.AppendLine(indentation + "totalLength += 4 + " + fieldPath + ".Length * " + fixedFieldLength + ";");
                    }
                    else if (elementFieldType - 15 <= LuminFiledType.UInt)
                    {
                        sb.AppendLine(indentation + "totalLength += 4; // Collection length prefix.");
                        if (depth > 0 && elementFieldType == LuminFiledType.Class)
                        {
                            sb.AppendLine(indentation + "if (" + fieldPath + " == null) continue;");
                        }
                        if (elementFieldType == LuminFiledType.Struct)
                        {
                            ITypeSymbol elementType = EmitterTypeTraits.GetCollectionElementType(field.TypeSymbol);
                            string generatedTypeName = elementType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                                ?? GetGeneratedTypeName(field);
                            if (EmitterTypeTraits.TryGetIsUnmanaged(elementType, out bool isUnmanaged))
                            {
                                if (isUnmanaged)
                                {
                                    sb.AppendLine(indentation + "totalLength += Unsafe.SizeOf<" + generatedTypeName + ">() * " + fieldPath + ".Length;");
                                    break;
                                }
                            }
                            else
                            {
                                sb.AppendLine(indentation + "if (!evaluator.IsReferenceOrContainsReferences<" + generatedTypeName + ">())");
                                sb.AppendLine(indentation + "    totalLength += Unsafe.SizeOf<" + generatedTypeName + ">() * " + fieldPath + ".Length;");
                                sb.AppendLine(indentation + "else");
                            }
                        }
                        sb.AppendLine(indentation + "for (int i" + depthSuffix + " = 0; i" + depthSuffix + " < " + fieldPath + ".Length; i" + depthSuffix + "++)");
                        sb.AppendLine(indentation + "{");
                        sb.AppendLine(indentation + "    totalLength += 1;");
                        for (int k = 0; k < field.ClassFields.Count; k++)
                        {
                            LuminDataField nestedField2 = field.ClassFields[k];
                            if (nestedField2.FieldType == LuminDataType.Reference && nestedField2.Type != LuminFiledType.Other)
                            {
                                if (nestedField2.IsPrivate)
                                {
                                    sb.AppendLine(indentation + "    if (Get" + field.Name + nestedField2.Name + "(" + fieldPath + "[i" + depthSuffix + "]) != null)");
                                }
                                else
                                {
                                    sb.AppendLine(indentation + "    if (" + fieldPath + "[i" + depthSuffix + "]." + nestedField2.Identifier + " != null)");
                                }
                                sb.AppendLine(indentation + "    {");
                                if (nestedField2.IsPrivate)
                                {
                                    GenerateSerializeLengthCode(sb, nestedField2, "Get" + field.Name + nestedField2.Name + "(" + fieldPath + "[i" + depthSuffix + "])", indent + 2, depth + 1);
                                }
                                else
                                {
                                    GenerateSerializeLengthCode(sb, nestedField2, fieldPath + "[i" + depthSuffix + "]." + nestedField2.Identifier, indent + 2, depth + 1);
                                }
                                sb.AppendLine(indentation + "    }");
                                switch (nestedField2.Type)
                                {
                                    case LuminFiledType.List:
                                    case LuminFiledType.Array:
                                        sb.AppendLine(indentation + "    else");
                                        sb.AppendLine(indentation + "    {");
                                        sb.AppendLine(indentation + "        totalLength += 4;");
                                        sb.AppendLine(indentation + "    }");
                                        break;
                                    case LuminFiledType.String:
                                        sb.AppendLine(indentation + "    else");
                                        sb.AppendLine(indentation + "    {");
                                        sb.AppendLine(indentation + "        totalLength += evaluator.StringRecordLength();");
                                        sb.AppendLine(indentation + "    }");
                                        break;
                                    default:
                                        sb.AppendLine(indentation + "    else");
                                        sb.AppendLine(indentation + "    {");
                                        sb.AppendLine(indentation + "        totalLength += 1;");
                                        sb.AppendLine(indentation + "    }");
                                        break;
                                }
                                continue;
                            }
                            if (IsMergeableField(nestedField2))
                            {
                                int mergeEndIndex3 = FindNextMergeableField(field.ClassFields, k);
                                if (mergeEndIndex3 != k)
                                {
                                    // See the sibling merge site: emit expression terms so that types
                                    // sized only by Unsafe.SizeOf are not dropped as zero.
                                    var mergedSizeTerms4 = new List<string>();
                                    for (int l = k; l <= mergeEndIndex3; l++)
                                    {
                                        mergedSizeTerms4.Add(GetFixedFieldLength(field.ClassFields[l]));
                                    }
                                    sb.Append($"{indentation}    totalLength += {string.Join(" + ", mergedSizeTerms4)};");
                                    sb.AppendLine();
                                    k = mergeEndIndex3;
                                    continue;
                                }
                            }
                            GenerateSerializeLengthCode(sb, nestedField2, fieldPath + "[i" + depthSuffix + "]." + nestedField2.Identifier, indent + 1, depth + 1);
                        }
                        sb.AppendLine(indentation + "}");
                    }
                    else
                    {
                        sb.AppendLine(indentation + "totalLength += 4;");
                        if (depth > 0)
                        {
                            sb.AppendLine(indentation + "if (" + fieldPath + " == null) continue;");
                        }
                        sb.AppendLine(indentation + "for (int i" + depthSuffix + " = 0; i" + depthSuffix + " < " + fieldPath + ".Length; i" + depthSuffix + "++)");
                        sb.AppendLine(indentation + "{");
                        LuminDataField field2 = new LuminDataField
                        {
                            Type = elementFieldType,
                            FieldType = field.FieldType,
                            GenericType = field.GenericType.Skip(1).ToList(),
                            ClassFields = field.ClassFields,
                            ClassName = field.ClassName,
                            ClassGenericType = field.ClassGenericType
                        };
                        if (elementFieldType == LuminFiledType.String)
                        {
                            sb.AppendLine(indentation + "    if (" + fieldPath + "[i" + depthSuffix + "] == null)");
                            sb.AppendLine(indentation + "    {");
                            sb.AppendLine(indentation + "        totalLength += evaluator.StringRecordLength();");
                            sb.AppendLine(indentation + "        continue;");
                            sb.AppendLine(indentation + "    }");
                        }
                        GenerateSerializeLengthCode(sb, field2, fieldPath + "[i" + depthSuffix + "]", indent + 1, depth + 1);
                        sb.AppendLine(indentation + "}");
                    }
                    break;
                }
            case LuminFiledType.Struct:
            case LuminFiledType.Class:
                {
                    if (IsPureValueTypeStruct(field))
                    {
                        sb.AppendLine(indentation + "totalLength += Unsafe.SizeOf<" + GetGeneratedTypeName(field) + ">();");
                        break;
                    }
                    sb.AppendLine(indentation + "// " + field.ClassName + " payload size evaluation");
                    sb.AppendLine(indentation + "totalLength += 1;");
                    for (int i = 0; i < field.ClassFields.Count; i++)
                    {
                        LuminDataField nestedField = field.ClassFields[i];
                        if (nestedField.FieldType == LuminDataType.Reference && nestedField.Type != LuminFiledType.Other)
                        {
                            if (nestedField.IsPrivate)
                            {
                                sb.AppendLine(indentation + "if (Get" + field.Name + nestedField.Name + "(" + fieldPath + ") != null)");
                            }
                            else
                            {
                                sb.AppendLine(indentation + "if (" + fieldPath + "." + nestedField.Identifier + " != null)");
                            }
                            sb.AppendLine(indentation + "{");
                            LuminFiledType type = nestedField.Type;
                            bool isNestedValueType = type - 17 <= LuminFiledType.UInt;
                            int depth2 = ((!isNestedValueType) ? (depth + 1) : 0);
                            if (nestedField.IsPrivate)
                            {
                                GenerateSerializeLengthCode(sb, nestedField, "Get" + field.Name + nestedField.Name + "(" + fieldPath + ")", indent + 1, depth2, multList + "_");
                            }
                            else
                            {
                                GenerateSerializeLengthCode(sb, nestedField, fieldPath + "." + nestedField.Identifier, indent + 1, depth2, multList + "_");
                            }
                            sb.AppendLine(indentation + "}");
                            switch (nestedField.Type)
                            {
                                case LuminFiledType.List:
                                case LuminFiledType.Array:
                                    sb.AppendLine(indentation + "else");
                                    sb.AppendLine(indentation + "{");
                                    sb.AppendLine(indentation + "    totalLength += 4;");
                                    sb.AppendLine(indentation + "}");
                                    break;
                                case LuminFiledType.String:
                                    sb.AppendLine(indentation + "else");
                                    sb.AppendLine(indentation + "{");
                                    sb.AppendLine(indentation + "    totalLength += evaluator.StringRecordLength();");
                                    sb.AppendLine(indentation + "}");
                                    break;
                                default:
                                    sb.AppendLine(indentation + "else");
                                    sb.AppendLine(indentation + "{");
                                    sb.AppendLine(indentation + "    totalLength += 1;");
                                    sb.AppendLine(indentation + "}");
                                    break;
                            }
                            continue;
                        }
                        if (IsUnmanagedFiledType(nestedField.Type))
                        {
                            int unmanagedEndIndex = FindNextUnmanagedType(field.ClassFields, i);
                            if (unmanagedEndIndex != i)
                            {
                                int mergedByteLength2 = 0;
                                for (int j = i; j <= unmanagedEndIndex; j++)
                                {
                                    int.TryParse(GetFixedFieldLength(field.ClassFields[j]), out var result);
                                    mergedByteLength2 += result;
                                }
                                sb.Append($"{indentation}totalLength += {mergedByteLength2};");
                                sb.AppendLine();
                                i = unmanagedEndIndex;
                                continue;
                            }
                        }
                        GenerateSerializeLengthCode(sb, nestedField, fieldPath + "." + nestedField.Identifier, indent, depth + 1, multList + "_");
                    }
                    break;
                }
            case LuminFiledType.Enum:
                sb.AppendLine(indentation + "totalLength += " + GetEnumFieldLength(field.EnumType) + ";");
                break;
            default:
                // Materialize member/indexer expressions into a writable local. Overload
                // resolution can then select an exact source-generated CalculateOffset
                // overload for closed types, while open generic fields retain the runtime
                // CalculateOffset<T> fallback.
                sb.AppendLine(indentation + "{");
                sb.AppendLine(indentation + "    var __luminPackOffsetValue = " + fieldPath + ";");
                sb.AppendLine(indentation + "    evaluator.CalculateOffset<" + (field.TypeSymbol == null ? field.FullTypeName : FormatterTypeName.Get(field.TypeSymbol)) + ">(ref __luminPackOffsetValue);");
                sb.AppendLine(indentation + "}");
                break;
        }
    }

    /// <summary>Appends statements that calculate the binary payload size.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="stringBuilder">The builder receiving generated source code.</param>
    public static void GenerateCalculateOffsetCode(LuminDataInfo data, StringBuilder stringBuilder)
    {
        if (PortableStructPlan.RequiresClosedDispatch(data.TypeSymbol))
        {
            stringBuilder.AppendLine("            global::LuminPack.LuminPackSerializer.FormatterDispatch.Sizeof<" + FormatterTypeName.Get(data.TypeSymbol) + ">(ref evaluator, ref value);");
            return;
        }
        var portable = PortableStructPlan.TryCreate(data.TypeSymbol);
        if (portable != null)
        {
            stringBuilder.AppendLine("            evaluator += " + portable.WireSize + ";");
            return;
        }
        string typeName = data.classFullName;
        if (!typeName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            typeName = "global::" + data.classNameSpace + "." + data.classFullName;
        }
        if (IsPureUnmanagedStruct(data))
        {
            stringBuilder.AppendLine("            evaluator += global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + typeName + ">();");
            return;
        }

        if (!data.isValueType)
        {
            stringBuilder.AppendLine("            if (value is null)");
            stringBuilder.AppendLine("            {");
            stringBuilder.AppendLine("                evaluator += 1;");
            stringBuilder.AppendLine("                return;");
            stringBuilder.AppendLine("            }");
        }
        stringBuilder.AppendLine();
        if (data.fields.Count(static x => x.IsPrivate || x.isProperty) > 0)
        {
            stringBuilder.AppendLine("            ref var local = ref LuminPackMarshal.As<" + typeName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
        }
        stringBuilder.AppendLine("            int totalLength = 1;");
        stringBuilder.AppendLine();
        for (int index = 0; index < data.fields.Count; index++)
        {
            using var optionScope = new CompiledOptionFieldScope(stringBuilder, data.fields[index].Options,
                data.fields[index].Type == LuminFiledType.Other && data.fields[index].TypeSymbol != null ? FormatterTypeName.Get(data.fields[index].TypeSymbol) : null);
            string owner = (data.fields[index].IsPrivate || data.fields[index].isProperty) ? "local" : "value";
            if (data.fields[index].FieldType == LuminDataType.Reference && data.fields[index].Type != LuminFiledType.Other)
            {
                if (data.fields[index].Type == LuminFiledType.String)
                {
                    stringBuilder.AppendLine("            var " + data.fields[index].Name + "Length = 0;");
                    stringBuilder.AppendLine("            if (" + owner + "." + data.fields[index].Identifier + " != null)");
                    stringBuilder.AppendLine("            {");
                    stringBuilder.AppendLine("                var " + data.fields[index].Name + "TempValue = " + owner + "." + data.fields[index].Identifier + ";");
                    stringBuilder.AppendLine("                totalLength += evaluator.GetStringLength(ref " + data.fields[index].Name + "TempValue);");
                    stringBuilder.AppendLine("            }");
                    stringBuilder.AppendLine("            else");
                    stringBuilder.AppendLine("            {");
                    stringBuilder.AppendLine("                totalLength += evaluator.StringRecordLength();");
                    stringBuilder.AppendLine("            }");
                }
                else
                {
                    stringBuilder.AppendLine("            if (" + owner + "." + data.fields[index].Identifier + " != null)");
                    stringBuilder.AppendLine("            {");
                    GenerateSerializeLengthCode(stringBuilder, data.fields[index], owner + "." + data.fields[index].Identifier, 4, 0);
                    stringBuilder.AppendLine("            }");
                    LuminFiledType fieldType = data.fields[index].Type;
                    if (fieldType - 17 <= LuminFiledType.UInt)
                    {
                        stringBuilder.AppendLine("            else");
                        stringBuilder.AppendLine("            {");
                        stringBuilder.AppendLine("                totalLength += 4;");
                        stringBuilder.AppendLine("            }");
                    }
                    else
                    {
                        stringBuilder.AppendLine("            else");
                        stringBuilder.AppendLine("            {");
                        stringBuilder.AppendLine("                totalLength += 1;");
                        stringBuilder.AppendLine("            }");
                    }
                }
                continue;
            }
            if (IsUnmanagedFiledType(data.fields[index].Type))
            {
                int next = FindNextUnmanagedType(data, index);
                if (next != index)
                {
                    int fixedLength = 0;
                    for (int current = index; current <= next; current++)
                    {
                        int.TryParse(GetFixedFieldLength(data.fields[current]), out int length);
                        fixedLength += length;
                    }
                    stringBuilder.Append("            totalLength += " + fixedLength + ";");
                    stringBuilder.AppendLine();
                    index = next;
                    continue;
                }
            }
            GenerateSerializeLengthCode(stringBuilder, data.fields[index], owner + "." + data.fields[index].Identifier, 3, 0);
        }
        stringBuilder.AppendLine();
        stringBuilder.AppendLine("            evaluator += totalLength;");
    }
}
