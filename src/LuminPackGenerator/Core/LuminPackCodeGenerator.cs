using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static class LuminPackCodeGenerator
{
	private static HashSet<string> defaultNamespace = new HashSet<string> { "System", "System.Collections.Generic", "System.Runtime.CompilerServices", "System.Runtime.InteropServices", "System.Threading.Tasks", "LuminPack", "LuminPack.Code", "LuminPack.Core", "Your.Data.Namespace" };

	private static void GenerateSerializeLengthCode(StringBuilder sb, LuminDataField field, string fieldPath, int indent, int depth, string multList = "_")
	{
		string text = new string(' ', indent * 4);
		string text2 = $"{multList}{depth}";
		switch (field.Type)
		{
		case LuminFiledType.Byte:
		case LuminFiledType.SByte:
		case LuminFiledType.Bool:
			sb.AppendLine(text + "totalLength += 1;");
			break;
		case LuminFiledType.Short:
		case LuminFiledType.UShort:
		case LuminFiledType.Char:
			sb.AppendLine(text + "totalLength += 2;");
			break;
		case LuminFiledType.Int:
		case LuminFiledType.UInt:
		case LuminFiledType.Float:
			sb.AppendLine(text + "totalLength += 4;");
			break;
		case LuminFiledType.Long:
		case LuminFiledType.ULong:
		case LuminFiledType.Double:
			sb.AppendLine(text + "totalLength += 8;");
			break;
		case LuminFiledType.Decimal:
			sb.AppendLine(text + "totalLength += 16;");
			break;
		case LuminFiledType.String:
			sb.AppendLine(text + "var " + field.Name + text2 + "TempValue = " + fieldPath + ";");
			sb.AppendLine(text + "totalLength += evaluator.GetStringLength(ref " + field.Name + text2 + "TempValue);");
			break;
		case LuminFiledType.List:
		{
			LuminFiledType luminFiledType2 = ConvertGenericsToFieldType(field.GenericType[0]);
			if (luminFiledType2 != LuminFiledType.List && IsFixedLengthType(luminFiledType2))
			{
				string fixedFieldLength2 = GetFixedFieldLength(new LuminDataField
				{
					Type = luminFiledType2
				});
				if (depth > 0)
				{
					sb.AppendLine(text + "if (" + fieldPath + " == null) continue;");
				}
				sb.AppendLine(text + "totalLength += 4 + " + fieldPath + ".Count * " + fixedFieldLength2 + ";");
			}
			else if (luminFiledType2 - 15 <= LuminFiledType.UInt)
			{
				sb.AppendLine(text + "totalLength += 4; // 列表长度前缀");
				if (depth > 0)
				{
					sb.AppendLine(text + "if (" + fieldPath + " == null) continue;");
				}
				if (luminFiledType2 == LuminFiledType.Struct)
				{
					string generatedTypeName = GetGeneratedTypeName(field);
					sb.AppendLine(text + "if (!evaluator.IsReferenceOrContainsReferences<" + generatedTypeName + ">())");
					sb.AppendLine(text + "    totalLength += Unsafe.SizeOf<" + generatedTypeName + ">() * " + fieldPath + ".Count;");
					sb.AppendLine(text + "else");
				}
				sb.AppendLine(text + "for (int i" + text2 + " = 0; i" + text2 + " < " + fieldPath + ".Count; i" + text2 + "++)");
				sb.AppendLine(text + "{");
				sb.AppendLine(text + "    var element = " + fieldPath + "[i" + text2 + "];");
				sb.AppendLine(text + "    totalLength += 1;");
				for (int m = 0; m < field.ClassFields.Count; m++)
				{
					LuminDataField luminDataField3 = field.ClassFields[m];
					if (luminDataField3.FieldType == LuminDataType.Reference)
					{
						if (luminDataField3.IsPrivate)
						{
							sb.AppendLine(text + "    if (Get" + field.Name + luminDataField3.Name + "(element) != null)");
						}
						else
						{
							sb.AppendLine(text + "    if (element." + luminDataField3.Identifier + " != null)");
						}
						sb.AppendLine(text + "    {");
						if (luminDataField3.IsPrivate)
						{
							GenerateSerializeLengthCode(sb, luminDataField3, "Get" + field.Name + luminDataField3.Name + "(element)", indent + 2, depth + 1);
						}
						else
						{
							GenerateSerializeLengthCode(sb, luminDataField3, "element." + luminDataField3.Identifier, indent + 2, depth + 1);
						}
						sb.AppendLine(text + "    }");
						switch (luminDataField3.Type)
						{
						case LuminFiledType.List:
						case LuminFiledType.Array:
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							sb.AppendLine(text + "        totalLength += 4;");
							sb.AppendLine(text + "    }");
							break;
						case LuminFiledType.String:
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							sb.AppendLine(text + "        totalLength += evaluator.StringRecordLength();");
							sb.AppendLine(text + "    }");
							break;
						default:
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							sb.AppendLine(text + "        totalLength += 1;");
							sb.AppendLine(text + "    }");
							break;
						}
						continue;
					}
					if (IsMergeableField(luminDataField3))
					{
						int num5 = FindNextMergeableField(field.ClassFields, m);
						if (num5 != m)
						{
							int num6 = 0;
							for (int n = m; n <= num5; n++)
							{
								int.TryParse(GetFixedFieldLength(field.ClassFields[n]), out var result3);
								num6 += result3;
							}
							sb.Append($"{text}    totalLength += {num6};");
							sb.AppendLine();
							m = num5;
							continue;
						}
					}
					GenerateSerializeLengthCode(sb, luminDataField3, "element." + luminDataField3.Identifier, indent + 1, depth + 1);
				}
				sb.AppendLine(text + "}");
			}
			else
			{
				sb.AppendLine(text + "totalLength += 4;");
				if (depth > 0)
				{
					sb.AppendLine(text + "if (" + fieldPath + " == null) continue;");
				}
				sb.AppendLine((field.FixLength == int.MaxValue) ? (text + "for (int i" + text2 + " = 0; i" + text2 + " < " + fieldPath + ".Count; i" + text2 + "++)") : $"{text}for (int i{text2} = 0; i{text2} < {field.FixLength}; i{text2}++)");
				sb.AppendLine(text + "{");
				LuminDataField field3 = new LuminDataField
				{
					Type = luminFiledType2,
					FieldType = field.FieldType,
					GenericType = field.GenericType.Skip(1).ToList(),
					ClassFields = field.ClassFields,
					ClassName = field.ClassName,
					ClassGenericType = field.ClassGenericType
				};
				if (luminFiledType2 == LuminFiledType.String)
				{
					sb.AppendLine(text + "    if (" + fieldPath + "[i" + text2 + "] == null)");
					sb.AppendLine(text + "    {");
					sb.AppendLine(text + "        totalLength += evaluator.StringRecordLength();");
					sb.AppendLine(text + "        continue;");
					sb.AppendLine(text + "    }");
				}
				GenerateSerializeLengthCode(sb, field3, fieldPath + "[i" + text2 + "]", indent + 1, depth + 1);
				sb.AppendLine(text + "}");
			}
			break;
		}
		case LuminFiledType.Array:
		{
			LuminFiledType luminFiledType = ConvertGenericsToFieldType(field.GenericType[0]);
			if (luminFiledType != LuminFiledType.Array && IsFixedLengthType(luminFiledType))
			{
				string fixedFieldLength = GetFixedFieldLength(new LuminDataField
				{
					Type = luminFiledType
				});
				if (depth > 0)
				{
					sb.AppendLine(text + "if (" + fieldPath + " == null) continue;");
				}
				sb.AppendLine(text + "totalLength += 4 + " + fieldPath + ".Length * " + fixedFieldLength + ";");
			}
			else if (luminFiledType - 15 <= LuminFiledType.UInt)
			{
				sb.AppendLine(text + "totalLength += 4; // 列表长度前缀");
				if (depth > 0 && luminFiledType == LuminFiledType.Class)
				{
					sb.AppendLine(text + "if (" + fieldPath + " == null) continue;");
				}
				if (luminFiledType == LuminFiledType.Struct)
				{
					string generatedTypeName = GetGeneratedTypeName(field);
					sb.AppendLine(text + "if (!evaluator.IsReferenceOrContainsReferences<" + generatedTypeName + ">())");
					sb.AppendLine(text + "    totalLength += Unsafe.SizeOf<" + generatedTypeName + ">() * " + fieldPath + ".Length;");
					sb.AppendLine(text + "else");
				}
				sb.AppendLine(text + "for (int i" + text2 + " = 0; i" + text2 + " < " + fieldPath + ".Length; i" + text2 + "++)");
				sb.AppendLine(text + "{");
				sb.AppendLine(text + "    totalLength += 1;");
				for (int k = 0; k < field.ClassFields.Count; k++)
				{
					LuminDataField luminDataField2 = field.ClassFields[k];
					if (luminDataField2.FieldType == LuminDataType.Reference)
					{
						if (luminDataField2.IsPrivate)
						{
							sb.AppendLine(text + "    if (Get" + field.Name + luminDataField2.Name + "(" + fieldPath + "[i" + text2 + "]) != null)");
						}
						else
						{
							sb.AppendLine(text + "    if (" + fieldPath + "[i" + text2 + "]." + luminDataField2.Identifier + " != null)");
						}
						sb.AppendLine(text + "    {");
						if (luminDataField2.IsPrivate)
						{
							GenerateSerializeLengthCode(sb, luminDataField2, "Get" + field.Name + luminDataField2.Name + "(" + fieldPath + "[i" + text2 + "])", indent + 2, depth + 1);
						}
						else
						{
							GenerateSerializeLengthCode(sb, luminDataField2, fieldPath + "[i" + text2 + "]." + luminDataField2.Identifier, indent + 2, depth + 1);
						}
						sb.AppendLine(text + "    }");
						switch (luminDataField2.Type)
						{
						case LuminFiledType.List:
						case LuminFiledType.Array:
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							sb.AppendLine(text + "        totalLength += 4;");
							sb.AppendLine(text + "    }");
							break;
						case LuminFiledType.String:
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							sb.AppendLine(text + "        totalLength += evaluator.StringRecordLength();");
							sb.AppendLine(text + "    }");
							break;
						default:
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							sb.AppendLine(text + "        totalLength += 1;");
							sb.AppendLine(text + "    }");
							break;
						}
						continue;
					}
					if (IsMergeableField(luminDataField2))
					{
						int num3 = FindNextMergeableField(field.ClassFields, k);
						if (num3 != k)
						{
							int num4 = 0;
							for (int l = k; l <= num3; l++)
							{
								int.TryParse(GetFixedFieldLength(field.ClassFields[l]), out var result2);
								num4 += result2;
							}
							sb.Append($"{text}    totalLength += {num4};");
							sb.AppendLine();
							k = num3;
							continue;
						}
					}
					GenerateSerializeLengthCode(sb, luminDataField2, fieldPath + "[i" + text2 + "]." + luminDataField2.Identifier, indent + 1, depth + 1);
				}
				sb.AppendLine(text + "}");
			}
			else
			{
				sb.AppendLine(text + "totalLength += 4;");
				if (depth > 0)
				{
					sb.AppendLine(text + "if (" + fieldPath + " == null) continue;");
				}
				sb.AppendLine((field.FixLength == int.MaxValue) ? (text + "for (int i" + text2 + " = 0; i" + text2 + " < " + fieldPath + ".Length; i" + text2 + "++)") : $"{text}for (int i{text2} = 0; i{text2} < {field.FixLength}; i{text2}++)");
				sb.AppendLine(text + "{");
				LuminDataField field2 = new LuminDataField
				{
					Type = luminFiledType,
					FieldType = field.FieldType,
					GenericType = field.GenericType.Skip(1).ToList(),
					ClassFields = field.ClassFields,
					ClassName = field.ClassName,
					ClassGenericType = field.ClassGenericType
				};
				if (luminFiledType == LuminFiledType.String)
				{
					sb.AppendLine(text + "    if (" + fieldPath + "[i" + text2 + "] == null)");
					sb.AppendLine(text + "    {");
					sb.AppendLine(text + "        totalLength += evaluator.StringRecordLength();");
					sb.AppendLine(text + "        continue;");
					sb.AppendLine(text + "    }");
				}
				GenerateSerializeLengthCode(sb, field2, fieldPath + "[i" + text2 + "]", indent + 1, depth + 1);
				sb.AppendLine(text + "}");
			}
			break;
		}
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
		{
			if (IsPureValueTypeStruct(field))
			{
				sb.AppendLine(text + "totalLength += Unsafe.SizeOf<" + GetGeneratedTypeName(field) + ">();");
				break;
			}
			sb.AppendLine(text + "// " + field.ClassName + "长度计算");
			sb.AppendLine(text + "totalLength += 1;");
			for (int i = 0; i < field.ClassFields.Count; i++)
			{
				LuminDataField luminDataField = field.ClassFields[i];
				if (luminDataField.FieldType == LuminDataType.Reference)
				{
					if (luminDataField.IsPrivate)
					{
						sb.AppendLine(text + "if (Get" + field.Name + luminDataField.Name + "(" + fieldPath + ") != null)");
					}
					else
					{
						sb.AppendLine(text + "if (" + fieldPath + "." + luminDataField.Identifier + " != null)");
					}
					sb.AppendLine(text + "{");
					LuminFiledType type = luminDataField.Type;
					bool flag = type - 17 <= LuminFiledType.UInt;
					int depth2 = ((!flag) ? (depth + 1) : 0);
					if (luminDataField.IsPrivate)
					{
						GenerateSerializeLengthCode(sb, luminDataField, "Get" + field.Name + luminDataField.Name + "(" + fieldPath + ")", indent + 1, depth2, multList + "_");
					}
					else
					{
						GenerateSerializeLengthCode(sb, luminDataField, fieldPath + "." + luminDataField.Identifier, indent + 1, depth2, multList + "_");
					}
					sb.AppendLine(text + "}");
					switch (luminDataField.Type)
					{
					case LuminFiledType.List:
					case LuminFiledType.Array:
						sb.AppendLine(text + "else");
						sb.AppendLine(text + "{");
						sb.AppendLine(text + "    totalLength += 4;");
						sb.AppendLine(text + "}");
						break;
					case LuminFiledType.String:
						sb.AppendLine(text + "else");
						sb.AppendLine(text + "{");
						sb.AppendLine(text + "    totalLength += evaluator.StringRecordLength();");
						sb.AppendLine(text + "}");
						break;
					default:
						sb.AppendLine(text + "else");
						sb.AppendLine(text + "{");
						sb.AppendLine(text + "    totalLength += 1;");
						sb.AppendLine(text + "}");
						break;
					}
					continue;
				}
				if (IsUnmanagedFiledType(luminDataField.Type))
				{
					int num = FindNextUnmanagedType(field.ClassFields, i);
					if (num != i)
					{
						int num2 = 0;
						for (int j = i; j <= num; j++)
						{
							int.TryParse(GetFixedFieldLength(field.ClassFields[j]), out var result);
							num2 += result;
						}
						sb.Append($"{text}totalLength += {num2};");
						sb.AppendLine();
						i = num;
						continue;
					}
				}
				GenerateSerializeLengthCode(sb, luminDataField, fieldPath + "." + luminDataField.Identifier, indent, depth + 1, multList + "_");
			}
			break;
		}
		case LuminFiledType.Enum:
			sb.AppendLine(text + "totalLength += " + GetEnumFieldLength(field.EnumType) + ";");
			break;
		default:
			// Materialize member/indexer expressions into a writable local. Overload
			// resolution can then select an exact source-generated CalculateOffset
			// overload for closed types, while open generic fields retain the runtime
			// CalculateOffset<T> fallback.
			sb.AppendLine(text + "{");
			sb.AppendLine(text + "    var __luminPackOffsetValue = " + fieldPath + ";");
			sb.AppendLine(text + "    evaluator.CalculateOffset(ref __luminPackOffsetValue);");
			sb.AppendLine(text + "}");
			break;
		}
	}

	private static void GenerateSerializeCode(StringBuilder sb, LuminDataField field, string fieldPath, string span, string offset, int indent, int depth, bool isMultClass = false, string stringRecordLength = "writer.StringRecordLength()")
	{
		string text = new string(' ', indent * 4);
		string text2 = $"_{depth}";
		switch (field.Type)
		{
		case LuminFiledType.Byte:
		case LuminFiledType.SByte:
			sb.AppendLine(text + "writer.GetSpanReference(" + offset + ") = " + fieldPath + ";");
			if (isMultClass)
			{
				sb.AppendLine(text + offset + " += 1;");
			}
			break;
		case LuminFiledType.Bool:
			if (isMultClass)
			{
				sb.AppendLine(text + offset + " += writer.WriteUnmanaged(ref " + offset + ", " + fieldPath + ");");
			}
			else
			{
				sb.AppendLine(text + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
			}
			break;
		case LuminFiledType.Short:
		case LuminFiledType.UShort:
		case LuminFiledType.Char:
			sb.AppendLine(text + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
			if (isMultClass)
			{
				sb.AppendLine(text + offset + " += 2;");
			}
			break;
		case LuminFiledType.Int:
		case LuminFiledType.UInt:
		case LuminFiledType.Float:
			sb.AppendLine(text + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
			if (isMultClass)
			{
				sb.AppendLine(text + offset + " += 4;");
			}
			break;
		case LuminFiledType.Long:
		case LuminFiledType.ULong:
		case LuminFiledType.Double:
			sb.AppendLine(text + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
			if (isMultClass)
			{
				sb.AppendLine(text + offset + " += 8;");
			}
			break;
		case LuminFiledType.Decimal:
			sb.AppendLine(text + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", " + fieldPath + ");");
			if (isMultClass)
			{
				sb.AppendLine(text + offset + " += 16;");
			}
			break;
		case LuminFiledType.String:
			sb.AppendLine(text + "var " + field.Name + "Length = writer.WriteString(ref " + offset + ", " + fieldPath + ");");
			if (isMultClass)
			{
				sb.AppendLine(text + offset + " += " + field.Name + "Length + " + stringRecordLength + ";");
			}
			break;
		case LuminFiledType.List:
		{
			if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
			{
				if (field.IsCompress)
				{
					sb.AppendLine(text + "var " + field.Name + "ListTempValue" + text2 + " = " + fieldPath + ";");
					sb.AppendLine(text + "var " + field.Name + "ListTempSpan" + text2 + " = LuminPackMarshal.GetListSpan(" + field.Name + "ListTempValue" + text2 + "!);");
					sb.AppendLine(text + "writer.DangerousWriteUnmanagedSpanWithCompress(ref " + offset + ", " + field.Name + "ListTempSpan" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				else
				{
					sb.AppendLine(text + "var " + field.Name + "ListTempValue" + text2 + " = " + fieldPath + ";");
					sb.AppendLine(text + "writer.WriteUnmanagedList(ref " + offset + ", ref " + field.Name + "ListTempValue" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				sb.AppendLine(text + "var " + field.Name + "ListOffset" + text2 + " = " + offset + " + " + field.Name + "TempLength" + text2 + ";");
				if (isMultClass)
				{
					sb.AppendLine(text + offset + " = " + field.Name + "ListOffset" + text2 + ";");
				}
				break;
			}
			if (depth > 0)
			{
				sb.AppendLine(text + "var " + field.Name + "v" + text2 + " = " + fieldPath + ";");
				sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = writer.WriteListHeaderAndGetSpan(ref " + offset + ", " + field.Name + "v" + text2 + "!);");
			}
			else
			{
				sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = writer.WriteListHeaderAndGetSpan(ref " + offset + ", " + fieldPath + "!);");
			}
			sb.AppendLine(text + "int " + field.Name + "Count" + text2 + " = " + field.Name + "TempSpan" + text2 + ".Length;");
			sb.AppendLine(text + "int " + field.Name + "ListOffset" + text2 + " = " + offset + " + 4;");
			LuminDataField luminDataField4 = new LuminDataField
			{
				Type = ConvertGenericsToFieldType(field.GenericType[0]),
				ClassName = field.ClassName,
				ClassFields = field.ClassFields,
				ClassGenericType = field.ClassGenericType,
				GenericType = field.GenericType.Skip(1).ToList(),
				IsCompress = field.IsCompress
			};
			if (IsPureValueTypeStruct(luminDataField4))
			{
				if (field.IsCompress)
				{
					sb.AppendLine(text + "writer.DangerousWriteUnmanagedSpanWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + text2 + ", " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				else
				{
					sb.AppendLine(text + "writer.WriteUnmanagedSpanWithOutHeader(ref " + offset + ", " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				sb.AppendLine(text + field.Name + "ListOffset" + text2 + " += " + field.Name + "TempLength" + text2 + ";");
				break;
			}
			sb.AppendLine(text + "foreach (ref var v" + text2 + " in " + field.Name + "TempSpan" + text2 + ")");
			sb.AppendLine(text + "{");
			if (luminDataField4.Type == LuminFiledType.String)
			{
				sb.AppendLine(text + "    if (v" + text2 + " == null)");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + text2 + ");");
				sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += " + stringRecordLength + ";");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
				sb.AppendLine(text + "    int strLen_" + text2 + " = writer.WriteString(ref " + field.Name + "ListOffset" + text2 + ", v" + text2 + ");");
				sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += strLen_" + text2 + " + " + stringRecordLength + ";");
				if (isMultClass)
				{
					sb.AppendLine(text + "    " + offset + " = " + field.Name + "ListOffset" + text2 + ";");
				}
			}
			else
			{
				LuminFiledType type = luminDataField4.Type;
				if (type - 15 <= LuminFiledType.UInt)
				{
					if (IsPureValueTypeStruct(luminDataField4))
					{
						sb.AppendLine(text + "    // 纯值类型结构体");
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + text2 + ", v" + text2 + ");");
						sb.AppendLine(text + "}");
						break;
					}
					if (luminDataField4.Type == LuminFiledType.Class)
					{
						sb.AppendLine(text + "    var element = v" + text2 + ";");
					}
					else
					{
						sb.AppendLine(text + "    ref var element = ref v" + text2 + ";");
					}
					if (luminDataField4.Type == LuminFiledType.Class)
					{
						sb.AppendLine(text + "    if (element == null)");
						sb.AppendLine(text + "    {");
						sb.AppendLine(text + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + text2 + ");");
						sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 1;");
						sb.AppendLine(text + "        continue;");
						sb.AppendLine(text + "    }");
					}
					int num4 = 0;
					if (luminDataField4.ClassFields.Count > 0 && IsMergeableField(luminDataField4.ClassFields[0]))
					{
						int num5 = Math.Min(FindNextMergeableField(luminDataField4.ClassFields, 0), 13);
						sb.Append($"{text}    {field.Name}ListOffset{text2} += writer.WriteUnmanaged(ref {field.Name}ListOffset{text2}, (byte){luminDataField4.ClassFields.Count}");
						for (int l = 0; l <= num5; l++)
						{
							LuminDataField luminDataField5 = luminDataField4.ClassFields[l];
							if (luminDataField5.IsPrivate)
							{
								sb.Append(", Get" + field.Name + luminDataField5.Name + "(element)");
							}
							else
							{
								sb.Append(", element." + luminDataField5.Name);
							}
						}
						sb.AppendLine(");");
						num4 = num5 + 1;
					}
					else
					{
						sb.AppendLine($"{text}    writer.WriteObjectHeader(ref {field.Name}ListOffset{text2}, {luminDataField4.ClassFields.Count});");
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += 1;");
					}
					sb.AppendLine();
					for (int m = num4; m < luminDataField4.ClassFields.Count; m++)
					{
						LuminDataField luminDataField6 = luminDataField4.ClassFields[m];
						if (luminDataField6.FieldType == LuminDataType.Reference)
						{
							string text6 = "serialize" + luminDataField6.Name + "Field" + text2;
							if (luminDataField6.IsPrivate)
							{
								sb.AppendLine(text + "    if (Get" + field.Name + luminDataField6.Name + "(element) is { } " + text6 + ")");
							}
							else
							{
								sb.AppendLine(text + "    if (element." + luminDataField6.Identifier + " is { } " + text6 + ")");
							}
							sb.AppendLine(text + "    {");
							GenerateSerializeCode(sb, luminDataField6, text6, span, field.Name + "ListOffset" + text2, indent + 2, depth + 1, isMultClass: true, stringRecordLength);
							sb.AppendLine(text + "    }");
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							switch (luminDataField6.Type)
							{
							case LuminFiledType.List:
							case LuminFiledType.Array:
								sb.AppendLine(text + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + text2 + ");");
								sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
								break;
							case LuminFiledType.String:
								sb.AppendLine(text + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + text2 + ");");
								sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += " + stringRecordLength + ";");
								break;
							default:
								sb.AppendLine(text + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + text2 + ");");
								sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 1;");
								break;
							}
							sb.AppendLine(text + "    }");
							continue;
						}
						if (IsMergeableField(luminDataField6))
						{
							int num6 = FindNextMergeableField(luminDataField4.ClassFields, m);
							if (num6 != m)
							{
								if (num6 - m >= 14)
								{
									num6 = m + 14;
								}
								sb.Append(text + "    " + field.Name + "ListOffset" + text2 + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + text2);
								for (int n = m; n <= num6; n++)
								{
									if (luminDataField4.ClassFields[n].IsPrivate)
									{
										sb.Append(", Get" + field.Name + luminDataField4.ClassFields[n].Name + "(element)");
									}
									else
									{
										sb.Append(", element." + luminDataField4.ClassFields[n].Name);
									}
								}
								sb.Append(");");
								sb.AppendLine();
								m = num6;
								continue;
							}
						}
						if (luminDataField6.IsPrivate)
						{
							GenerateSerializeCode(sb, luminDataField6, "Get" + field.Name + luminDataField6.Name + "(element)", span, field.Name + "ListOffset" + text2, indent + 2, depth + 1, isMultClass: true, stringRecordLength);
						}
						else
						{
							GenerateSerializeCode(sb, luminDataField6, "element." + luminDataField6.Identifier, span, field.Name + "ListOffset" + text2, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
						}
					}
				}
				else
				{
					type = luminDataField4.Type;
					if (type - 17 <= LuminFiledType.UInt)
					{
						sb.AppendLine(text + "    if (v" + text2 + " == null)");
						sb.AppendLine(text + "    {");
						sb.AppendLine(text + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + text2 + ");");
						sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
						sb.AppendLine(text + "        continue;");
						sb.AppendLine(text + "    }");
					}
					GenerateSerializeCode(sb, luminDataField4, "v" + text2, span, field.Name + "ListOffset" + text2, indent + 1, depth + 1, isMultClass: false, stringRecordLength);
					type = luminDataField4.Type;
					if (type - 17 <= LuminFiledType.UInt)
					{
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " = " + GetSerializeFieldLength(luminDataField4, depth + 1, stringRecordLength) + ";");
					}
					else
					{
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += " + GetSerializeFieldLength(luminDataField4, depth + 1, stringRecordLength) + ";");
					}
				}
			}
			sb.AppendLine(text + "}");
			break;
		}
		case LuminFiledType.Array:
		{
			if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
			{
				if (field.IsCompress)
				{
					sb.AppendLine(text + "writer.DangerousWriteUnmanagedArrayWithCompress(ref " + offset + ", " + fieldPath + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				else
				{
					sb.AppendLine(text + "writer.EnsureAdditionalCapacity(checked(4 + global::System.Runtime.InteropServices.MemoryMarshal.AsBytes(" + fieldPath + ".AsSpan()).Length));");
					sb.AppendLine(text + "writer.DangerousWriteUnmanagedArray(ref " + offset + ", " + fieldPath + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				sb.AppendLine(text + "var " + field.Name + "ListOffset" + text2 + " = " + offset + " + " + field.Name + "TempLength" + text2 + ";");
				if (isMultClass)
				{
					sb.AppendLine(text + offset + " = " + field.Name + "ListOffset" + text2 + ";");
				}
				break;
			}
			LuminDataField luminDataField = new LuminDataField
			{
				Type = ConvertGenericsToFieldType(field.GenericType[0]),
				ClassName = field.ClassName,
				ClassFields = field.ClassFields,
				ClassGenericType = field.ClassGenericType,
				GenericType = field.GenericType.Skip(1).ToList(),
				IsCompress = field.IsCompress
			};
			sb.AppendLine(text + "int " + field.Name + "Count" + text2 + " = " + fieldPath + ".Length;");
			sb.AppendLine(text + "writer.WriteCollectionHeader(ref " + offset + ", " + field.Name + "Count" + text2 + ");");
			sb.AppendLine(text + "int " + field.Name + "ListOffset" + text2 + " = " + offset + " + 4;");
			if (IsPureValueTypeStruct(luminDataField))
			{
				if (field.IsCompress)
				{
					sb.AppendLine(text + "writer.DangerousWriteUnmanagedArrayWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + text2 + ", " + fieldPath + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				else
				{
					sb.AppendLine(text + "writer.WriteUnmanagedArrayWithOutHeader(ref " + offset + ", " + fieldPath + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				sb.AppendLine(text + field.Name + "ListOffset" + text2 + " += " + field.Name + "TempLength" + text2 + ";");
				break;
			}
			sb.AppendLine(text + "for (int i" + text2 + " = 0; i" + text2 + " < " + field.Name + "Count" + text2 + "; i" + text2 + "++)");
			sb.AppendLine(text + "{");
			sb.AppendLine(text + "    ref var v" + text2 + " = ref " + fieldPath + "[i" + text2 + "];");
			if (luminDataField.Type == LuminFiledType.String)
			{
				sb.AppendLine(text + "    if (v" + text2 + " == null)");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + text2 + ");");
				sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += " + stringRecordLength + ";");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
				sb.AppendLine(text + "    int strLen_" + text2 + " = writer.WriteString(ref " + field.Name + "ListOffset" + text2 + ", v" + text2 + ");");
				sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += strLen_" + text2 + " + " + stringRecordLength + ";");
				if (isMultClass)
				{
					sb.AppendLine(text + "    " + offset + " = " + field.Name + "ListOffset" + text2 + ";");
				}
			}
			else
			{
				LuminFiledType type = luminDataField.Type;
				if (type - 15 <= LuminFiledType.UInt)
				{
					if (IsPureValueTypeStruct(luminDataField))
					{
						sb.AppendLine(text + "    // 纯值类型结构体");
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + text2 + ", v" + text2 + ");");
						sb.AppendLine(text + "}");
						break;
					}
					string text3 = "v" + text2;
					if (luminDataField.Type == LuminFiledType.Class)
					{
						string text4 = "element" + text2;
						sb.AppendLine(text + "    var " + text4 + " = v" + text2 + ";");
						text3 = text4;
						sb.AppendLine(text + "    if (" + text3 + " == null)");
						sb.AppendLine(text + "    {");
						sb.AppendLine(text + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + text2 + ");");
						sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 1;");
						sb.AppendLine(text + "        continue;");
						sb.AppendLine(text + "    }");
					}
					int num = 0;
					if (luminDataField.ClassFields.Count > 0 && IsMergeableField(luminDataField.ClassFields[0]))
					{
						int num2 = Math.Min(FindNextMergeableField(luminDataField.ClassFields, 0), 13);
						sb.Append($"{text}    {field.Name}ListOffset{text2} += writer.WriteUnmanaged(ref {field.Name}ListOffset{text2}, (byte){luminDataField.ClassFields.Count}");
						for (int i = 0; i <= num2; i++)
						{
							LuminDataField luminDataField2 = luminDataField.ClassFields[i];
							if (luminDataField2.IsPrivate)
							{
								sb.Append(", Get" + field.Name + luminDataField2.Name + "(" + text3 + ")");
							}
							else
							{
								sb.Append(", " + text3 + "." + luminDataField2.Name);
							}
						}
						sb.AppendLine(");");
						num = num2 + 1;
					}
					else
					{
						sb.AppendLine($"{text}    writer.WriteObjectHeader(ref {field.Name}ListOffset{text2}, {luminDataField.ClassFields.Count});");
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += 1;");
					}
					sb.AppendLine();
					for (int j = num; j < luminDataField.ClassFields.Count; j++)
					{
						LuminDataField luminDataField3 = luminDataField.ClassFields[j];
						if (luminDataField3.FieldType == LuminDataType.Reference)
						{
							string text5 = "serialize" + luminDataField3.Name + "Field" + text2;
							if (luminDataField3.IsPrivate)
							{
								sb.AppendLine(text + "    if (Get" + field.Name + luminDataField3.Name + "(" + text3 + ") is { } " + text5 + ")");
							}
							else
							{
								sb.AppendLine(text + "    if (" + text3 + "." + luminDataField3.Identifier + " is { } " + text5 + ")");
							}
							sb.AppendLine(text + "    {");
							GenerateSerializeCode(sb, luminDataField3, text5, span, field.Name + "ListOffset" + text2, indent + 2, depth + 1, isMultClass: true, stringRecordLength);
							sb.AppendLine(text + "    }");
							sb.AppendLine(text + "    else");
							sb.AppendLine(text + "    {");
							switch (luminDataField3.Type)
							{
							case LuminFiledType.List:
							case LuminFiledType.Array:
								sb.AppendLine(text + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + text2 + ");");
								sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
								break;
							case LuminFiledType.String:
								sb.AppendLine(text + "        writer.WriteNullStringHeader(ref " + field.Name + "ListOffset" + text2 + ");");
								sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += " + stringRecordLength + ";");
								break;
							default:
								sb.AppendLine(text + "        writer.WriteNullObjectHeader(ref " + field.Name + "ListOffset" + text2 + ");");
								sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 1;");
								break;
							}
							sb.AppendLine(text + "    }");
							continue;
						}
						if (IsMergeableField(luminDataField3))
						{
							int num3 = FindNextMergeableField(luminDataField.ClassFields, j);
							if (num3 != j)
							{
								if (num3 - j >= 14)
								{
									num3 = j + 14;
								}
								sb.Append(text + "    " + field.Name + "ListOffset" + text2 + " += writer.WriteUnmanaged(ref " + field.Name + "ListOffset" + text2);
								for (int k = j; k <= num3; k++)
								{
									if (luminDataField.ClassFields[k].IsPrivate)
									{
										sb.Append(", Get" + field.Name + luminDataField.ClassFields[k].Name + "(" + text3 + ")");
									}
									else
									{
										sb.Append(", " + text3 + "." + luminDataField.ClassFields[k].Name);
									}
								}
								sb.Append(");");
								sb.AppendLine();
								j = num3;
								continue;
							}
						}
						if (luminDataField3.IsPrivate)
						{
							GenerateSerializeCode(sb, luminDataField3, "Get" + field.Name + luminDataField3.Name + "(" + text3 + ")", span, field.Name + "ListOffset" + text2, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
						}
						else
						{
							GenerateSerializeCode(sb, luminDataField3, text3 + "." + luminDataField3.Identifier, span, field.Name + "ListOffset" + text2, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
						}
					}
				}
				else
				{
					type = luminDataField.Type;
					if (type - 17 <= LuminFiledType.UInt)
					{
						sb.AppendLine(text + "    if (v" + text2 + " == null)");
						sb.AppendLine(text + "    {");
						sb.AppendLine(text + "        writer.WriteNullCollectionHeader(ref " + field.Name + "ListOffset" + text2 + ");");
						sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
						sb.AppendLine(text + "        continue;");
						sb.AppendLine(text + "    }");
					}
					GenerateSerializeCode(sb, luminDataField, "v" + text2, span, field.Name + "ListOffset" + text2, indent + 1, depth + 1, isMultClass: false, stringRecordLength);
					type = luminDataField.Type;
					if (type - 17 <= LuminFiledType.UInt)
					{
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " = " + GetSerializeFieldLength(luminDataField, depth + 1, stringRecordLength) + ";");
					}
					else
					{
						sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += " + GetSerializeFieldLength(luminDataField, depth + 1, stringRecordLength) + ";");
					}
				}
			}
			sb.AppendLine(text + "}");
			break;
		}
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
		{
			if (IsPureValueTypeStruct(field))
			{
				sb.AppendLine(text + "// 纯值类型结构体 " + field.ClassName + "，直接整体写入");
				sb.AppendLine(text + "writer.Advance(writer.WriteUnmanaged(ref " + offset + ", " + fieldPath + "));");
				break;
			}
			sb.AppendLine(text + "// 序列化" + field.ClassName);
			string text7 = fieldPath;
			if (field.Type == LuminFiledType.Class && field.ClassFields.Count > 1)
			{
				string text8 = "serialize" + field.Name + "Value" + text2;
				sb.AppendLine(text + "var " + text8 + " = " + fieldPath + ";");
				text7 = text8;
			}
			sb.AppendLine($"{text}writer.WriteObjectHeader(ref {offset}, {field.ClassFields.Count});");
			sb.AppendLine(text + offset + " += 1;");
			sb.AppendLine();
			for (int num7 = 0; num7 < field.ClassFields.Count; num7++)
			{
				LuminDataField luminDataField7 = field.ClassFields[num7];
				if (luminDataField7.FieldType == LuminDataType.Reference)
				{
					string text9 = "serialize" + luminDataField7.Name + "Field" + text2;
					if (luminDataField7.IsPrivate)
					{
						sb.AppendLine(text + "if (Get" + field.Name + luminDataField7.Name + "(" + text7 + ") is { } " + text9 + ")");
					}
					else
					{
						sb.AppendLine(text + "if (" + text7 + "." + luminDataField7.Identifier + " is { } " + text9 + ")");
					}
					sb.AppendLine(text + "{");
					GenerateSerializeCode(sb, luminDataField7, text9, span, offset, indent + 1, depth + 1, isMultClass: true, stringRecordLength);
					LuminFiledType type = luminDataField7.Type;
					if (type - 17 <= LuminFiledType.UInt)
					{
						string text10 = $"_{depth + 1}";
						sb.AppendLine(text + "    writer.FlushCurrentIndex(" + luminDataField7.Name + "ListOffset" + text10 + ");");
					}
					sb.AppendLine(text + "}");
					switch (luminDataField7.Type)
					{
					case LuminFiledType.List:
					case LuminFiledType.Array:
						sb.AppendLine(text + "else");
						sb.AppendLine(text + "{");
						sb.AppendLine(text + "    writer.WriteNullCollectionHeader(ref " + offset + ");");
						sb.AppendLine(text + "    " + offset + " += 4;");
						sb.AppendLine(text + "}");
						break;
					case LuminFiledType.String:
						sb.AppendLine(text + "else");
						sb.AppendLine(text + "{");
						sb.AppendLine(text + "    writer.WriteNullStringHeader(ref " + offset + ");");
						sb.AppendLine(text + "    " + offset + " += " + stringRecordLength + ";");
						sb.AppendLine(text + "}");
						break;
					default:
						sb.AppendLine(text + "else");
						sb.AppendLine(text + "{");
						sb.AppendLine(text + "    writer.WriteNullObjectHeader(ref " + offset + ");");
						sb.AppendLine(text + "    " + offset + " += 1;");
						sb.AppendLine(text + "}");
						break;
					}
					continue;
				}
				if (IsMergeableField(luminDataField7))
				{
					int num8 = FindNextMergeableField(field.ClassFields, num7);
					if (num8 != num7)
					{
						if (num8 - num7 >= 14)
						{
							num8 = num7 + 14;
						}
						sb.Append(text + "writer.Advance(writer.WriteUnmanaged(ref " + offset);
						for (int num9 = num7; num9 <= num8; num9++)
						{
							if (field.ClassFields[num9].IsPrivate)
							{
								sb.Append(", Get" + field.Name + field.ClassFields[num9].Name + "(" + text7 + ")");
							}
							else
							{
								sb.Append(", " + text7 + "." + field.ClassFields[num9].Name);
							}
						}
						sb.Append("));");
						sb.AppendLine();
						num7 = num8;
						continue;
					}
				}
				if (luminDataField7.IsPrivate)
				{
					GenerateSerializeCode(sb, luminDataField7, "Get" + field.Name + luminDataField7.Name + "(" + text7 + ")", span, offset, indent, depth + 1, isMultClass: true, stringRecordLength);
				}
				else
				{
					GenerateSerializeCode(sb, luminDataField7, text7 + "." + luminDataField7.Identifier, span, offset, indent, depth + 1, isMultClass: true, stringRecordLength);
				}
			}
			break;
		}
		case LuminFiledType.Enum:
			if (isMultClass)
			{
				sb.AppendLine(text + "writer.Advance(writer.WriteUnmanaged(ref " + offset + ", (" + GetEnumTypeName(field.EnumType) + ")" + fieldPath + "));");
			}
			else
			{
				sb.AppendLine(text + "writer.WriteUnmanagedWithoutSizeReturn(ref " + offset + ", (" + GetEnumTypeName(field.EnumType) + ")" + fieldPath + ");");
			}
			break;
		default:
			if (field.IsCompress)
			{
				sb.AppendLine(text + "writer.WriteValueWithCompress(" + fieldPath + ");");
			}
			else
			{
				sb.AppendLine(text + "writer.WriteValue(" + fieldPath + ");");
			}
			break;
		}
	}

	private static void GenerateDeserializeCode(StringBuilder sb, LuminDataField field, string targetObj, string span, string offset, int indent, int depth, bool isFirst = true, string fieldPath = "", bool isArray = false, bool isList = false, string multList = "_", bool isPrivateFiled = false, string parentName = "", bool collectionHeadAlreadyRead = false, bool targetIsKnownFresh = false)
	{
		string text = new string(' ', indent * 4);
		string text2 = $"{multList}{depth}";
		switch (field.Type)
		{
		case LuminFiledType.Byte:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out byte " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.SByte:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out sbyte " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Bool:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out bool " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Short:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out short " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.UShort:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out ushort " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Int:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out int " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.UInt:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out uint " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Long:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out long " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.ULong:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out ulong " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Float:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out float " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Double:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out double " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Decimal:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out decimal " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.Char:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn(ref " + offset + ", out char " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		case LuminFiledType.String:
			sb.AppendLine(text + targetObj + " = reader.ReadStringAndAdvance(ref " + offset + ")!;");
			break;
		case LuminFiledType.List:
		{
			if (isFirst && depth == 0 && !collectionHeadAlreadyRead)
			{
				sb.AppendLine(text + "reader.TryReadCollectionHead(ref " + offset + ", out int " + field.Name + "Count" + text2 + ");");
			}
			LuminDataField luminDataField = new LuminDataField
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
			string fullGenericTypeName = GetFullGenericTypeName(field.GenericType, luminDataField.ClassName, luminDataField.ClassGenericType);
			bool flag = isFirst && depth == 0;
			if (flag)
			{
				sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = reader.CreateFreshListSpan<" + fullGenericTypeName + ">(" + field.Name + "Count" + text2 + ", out " + targetObj + ");");
			}
			else if (isFirst)
			{
				sb.AppendLine(text + targetObj + " = new List<" + fullGenericTypeName + ">(" + field.Name + "Count" + text2 + ");");
			}
			sb.AppendLine(text + "int " + field.Name + "ListOffset" + text2 + " = " + offset + " + 4;");
			if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
			{
				if (depth > 0)
				{
					sb.AppendLine($"{text}var {field.Name}TempSpan{text2} = LuminPackMarshal.GetListSpan(element__{depth - 1}, {field.Name}Count{text2});");
					if (field.IsCompress)
					{
						sb.AppendLine(text + "reader.DangerousReadUnmanagedSpanWithCompress(ref " + offset + ", ref " + field.Name + "TempSpan" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
						sb.AppendLine($"{text}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{text2});");
						sb.AppendLine(text + field.Name + "ListOffset" + text2 + " = " + offset + " + " + field.Name + "TempLength" + text2 + ";");
					}
					else
					{
						sb.AppendLine(text + "reader.ReadUnmanagedSpan(ref " + field.Name + "ListOffset" + text2 + ", ref " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
						sb.AppendLine($"{text}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{text2});");
						sb.AppendLine(text + field.Name + "ListOffset" + text2 + " += " + field.Name + "TempLength" + text2 + ";");
					}
					break;
				}
				if (!flag)
				{
					sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + text2 + ");");
				}
				if (field.IsCompress)
				{
					sb.AppendLine(text + "reader.DangerousReadUnmanagedSpanWithCompress(ref " + offset + ", ref " + field.Name + "TempSpan" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
					sb.AppendLine(text + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + text2 + ");");
					sb.AppendLine(text + field.Name + "ListOffset" + text2 + " = " + offset + " + " + field.Name + "TempLength" + text2 + ";");
				}
				else
				{
					sb.AppendLine(text + "reader.ReadUnmanagedSpan(ref " + field.Name + "ListOffset" + text2 + ", ref " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
					sb.AppendLine(text + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + text2 + ");");
					sb.AppendLine(text + field.Name + "ListOffset" + text2 + " += " + field.Name + "TempLength" + text2 + ";");
				}
				break;
			}
			if (IsPureValueTypeStruct(luminDataField))
			{
				if (field.IsCompress)
				{
					if (depth > 0)
					{
						sb.AppendLine($"{text}var {field.Name}TempSpan{text2} = LuminPackMarshal.GetListSpan(element__{depth - 1}, {field.Name}Count{text2});");
						sb.AppendLine(text + "reader.DangerousReadUnmanagedSpanWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + text2 + ", ref " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
						sb.AppendLine($"{text}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{text2});");
					}
					else
					{
						if (!flag)
						{
							sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + text2 + ");");
						}
						sb.AppendLine(text + "reader.DangerousReadUnmanagedSpanWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + text2 + ", ref " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
						sb.AppendLine(text + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + text2 + ");");
					}
				}
				else if (depth > 0)
				{
					sb.AppendLine($"{text}var {field.Name}TempSpan{text2} = LuminPackMarshal.GetListSpan(element__{depth - 1}, {field.Name}Count{text2});");
					sb.AppendLine(text + "reader.ReadUnmanagedSpan(ref " + offset + ", ref " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
					sb.AppendLine($"{text}LuminPackMarshal.SetListSize(element__{depth - 1}, {field.Name}Count{text2});");
				}
				else
				{
					if (!flag)
					{
						sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + text2 + ");");
					}
					sb.AppendLine(text + "reader.ReadUnmanagedSpan(ref " + offset + ", ref " + field.Name + "TempSpan" + text2 + ", " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
					sb.AppendLine(text + "LuminPackMarshal.SetListSize(" + targetObj + ", " + field.Name + "Count" + text2 + ");");
				}
				sb.AppendLine(text + field.Name + "ListOffset" + text2 + " += " + field.Name + "TempLength" + text2 + ";");
				break;
			}
			if (depth > 0)
			{
				sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = LuminPackMarshal.GetListSpan(" + fieldPath + "!, " + field.Name + "Count" + text2 + ");");
			}
			else if (!flag)
			{
				sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = LuminPackMarshal.GetListSpan(" + targetObj + "!, " + field.Name + "Count" + text2 + ");");
			}
			sb.AppendLine(text + "for (int i" + text2 + " = 0; i" + text2 + " < " + field.Name + "Count" + text2 + "; i" + text2 + "++)");
			sb.AppendLine(text + "{");
			StringBuilder stringBuilder = new StringBuilder();
			if (isArray)
			{
				stringBuilder.Append($"[i{multList}{depth}]");
			}
			if (luminDataField.Type == LuminFiledType.List)
			{
				sb.AppendLine($"{text}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{text2}, out int {luminDataField.Name}Count_{depth + 1}))");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
				sb.AppendLine(text + "        " + field.Name + "TempSpan" + text2 + "[i" + text2 + "] = default!;");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
				string fullGenericTypeName2 = GetFullGenericTypeName(luminDataField.GenericType, luminDataField.ClassName, luminDataField.ClassGenericType);
				sb.AppendLine($"{text}    var element_{text2} = new List<{fullGenericTypeName2}>({luminDataField.Name}Count_{depth + 1});");
				sb.AppendLine(text + "    " + field.Name + "TempSpan" + text2 + "[i" + text2 + "] = element_" + text2 + ";");
			}
			else if (luminDataField.Type == LuminFiledType.Array)
			{
				LuminGenericsType luminGenericsType = field.GenericType.Last();
				sb.AppendLine($"{text}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{text2}, out int {luminDataField.Name}Count_{depth + 1}))");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
				sb.AppendLine(text + "        " + field.Name + "TempSpan" + text2 + "[i" + text2 + "] = default!;");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
				StringBuilder stringBuilder2 = new StringBuilder();
				for (int i = 1; i < field.GenericType.Count && field.GenericType[i] == LuminGenericsType.Array; i++)
				{
					stringBuilder2.Append("[]");
				}
				if (luminGenericsType != LuminGenericsType.Struct && luminGenericsType != LuminGenericsType.Class)
				{
					sb.AppendLine($"{text}    var element_{text2} = LuminPackMarshal.AllocateUninitializedArray<{GetGenericTypeName(luminGenericsType)}{stringBuilder2}>({luminDataField.Name}Count_{depth + 1});");
				}
				else
				{
					sb.AppendLine($"{text}    var element_{text2} = LuminPackMarshal.AllocateUninitializedArray<{field.ClassName}{stringBuilder2}>({luminDataField.Name}Count_{depth + 1});");
				}
				sb.AppendLine(text + "    " + field.Name + "TempSpan" + text2 + "[i" + text2 + "] = element_" + text2 + ";");
			}
			else if (luminDataField.Type == LuminFiledType.Class)
			{
				sb.AppendLine(text + "    if (reader.PeekIsNullObject(ref " + field.Name + "ListOffset" + text2 + "))");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        " + field.Name + "TempSpan" + text2 + "[i" + text2 + "] = default!;");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
			}
			string text3 = field.Name + "TempSpan" + text2 + "[i" + text2 + "]";
			GenerateDeserializeCode(sb, luminDataField, text3, span, field.Name + "ListOffset" + text2, indent + 1, depth + 1, isFirst: false, text3, isArray: false, isList: true, multList, isPrivateFiled: false, parentName + field.Name);
			LuminFiledType type = luminDataField.Type;
			if (type != LuminFiledType.String && type != LuminFiledType.Class && type != LuminFiledType.Struct)
			{
				type = luminDataField.Type;
				if (type - 17 <= LuminFiledType.UInt)
				{
					sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " = " + GetFieldLength(luminDataField, depth + 1) + ";");
				}
				else
				{
					sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += " + GetFieldLength(luminDataField, depth + 1) + ";");
				}
			}
			sb.AppendLine(text + "}");
			break;
		}
		case LuminFiledType.Array:
		{
			if (isFirst && !collectionHeadAlreadyRead && (IsReferenceGenericType(field.GenericType.FirstOrDefault()) || !field.IsCompress))
			{
				sb.AppendLine(text + "reader.TryReadCollectionHead(ref " + offset + ", out int " + field.Name + "Count" + text2 + ");");
			}
			LuminDataField luminDataField5 = new LuminDataField
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
			for (int num3 = 0; num3 < field.GenericType.Count && field.GenericType[num3] == LuminGenericsType.Array; num3++)
			{
				stringBuilder3.Append("[]");
			}
			if (isFirst && (IsReferenceGenericType(field.GenericType.FirstOrDefault()) || !field.IsCompress))
			{
				LuminGenericsType luminGenericsType2 = field.GenericType.Last();
				if (luminDataField5.Type == LuminFiledType.List)
				{
					string fullGenericTypeName3 = GetFullGenericTypeName(luminDataField5.GenericType, luminDataField5.ClassName, luminDataField5.ClassGenericType);
					sb.AppendLine($"{text}{targetObj} = LuminPackMarshal.AllocateUninitializedArray<List<{fullGenericTypeName3}>{stringBuilder3}>({field.Name}Count{text2});");
				}
				else if (luminGenericsType2 != LuminGenericsType.Struct && luminGenericsType2 != LuminGenericsType.Class)
				{
					sb.AppendLine($"{text}{targetObj} = LuminPackMarshal.AllocateUninitializedArray<{GetGenericTypeName(luminGenericsType2)}{stringBuilder3}>({field.Name}Count{text2});");
				}
				else
				{
					sb.AppendLine($"{text}{targetObj} = LuminPackMarshal.AllocateUninitializedArray<{field.ClassName}{stringBuilder3}>({field.Name}Count{text2});");
				}
			}
			sb.AppendLine(text + "int " + field.Name + "ListOffset" + text2 + " = " + offset + " + 4;");
			if (!IsReferenceGenericType(field.GenericType.FirstOrDefault()))
			{
				if (field.IsCompress)
				{
					if (depth > 0)
					{
						sb.AppendLine($"{text}reader.DangerousReadUnmanagedArrayWithCompress(ref {offset}, ref element__{depth - 1}, out var {field.Name}TempLength{text2});");
					}
					else
					{
						sb.AppendLine(text + "reader.DangerousReadUnmanagedArrayWithCompress(ref " + offset + ", ref " + targetObj + "!, out var " + field.Name + "TempLength" + text2 + ");");
					}
					sb.AppendLine(text + field.Name + "ListOffset" + text2 + " = " + offset + " + " + field.Name + "TempLength" + text2 + ";");
					break;
				}
				if (depth > 0)
				{
					sb.AppendLine($"{text}reader.ReadFreshUnmanagedArray(ref {field.Name}ListOffset{text2}, element__{depth - 1}, {field.Name}Count{text2}, out var {field.Name}TempLength{text2});");
				}
				else
				{
					sb.AppendLine(text + "reader.ReadFreshUnmanagedArray(ref " + field.Name + "ListOffset" + text2 + ", " + targetObj + "!, " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				sb.AppendLine(text + field.Name + "ListOffset" + text2 + " += " + field.Name + "TempLength" + text2 + ";");
				break;
			}
			if (IsPureValueTypeStruct(luminDataField5))
			{
				if (field.IsCompress)
				{
					if (depth > 0)
					{
						sb.AppendLine($"{text}reader.DangerousReadUnmanagedArrayWithOutHeaderWithCompress(ref {field.Name}ListOffset{text2}, ref element__{depth - 1}, {field.Name}Count{text2}, out var {field.Name}TempLength{text2});");
					}
					else
					{
						sb.AppendLine(text + "reader.DangerousReadUnmanagedArrayWithOutHeaderWithCompress(ref " + field.Name + "ListOffset" + text2 + ", ref " + targetObj + "!, " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
					}
				}
				else if (depth > 0)
				{
					sb.AppendLine($"{text}reader.ReadFreshUnmanagedArray(ref {offset}, element__{depth - 1}, {field.Name}Count{text2}, out var {field.Name}TempLength{text2});");
				}
				else
				{
					sb.AppendLine(text + "reader.ReadFreshUnmanagedArray(ref " + offset + ", " + targetObj + "!, " + field.Name + "Count" + text2 + ", out var " + field.Name + "TempLength" + text2 + ");");
				}
				sb.AppendLine(text + field.Name + "ListOffset" + text2 + " += " + field.Name + "TempLength" + text2 + ";");
				break;
			}
			string text10;
			if (depth > 0)
			{
				sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = " + fieldPath + ".AsSpan();");
				text10 = field.Name + "Count" + text2;
			}
			else
			{
				sb.AppendLine(text + "var " + field.Name + "TempSpan" + text2 + " = " + targetObj + "!;");
				text10 = field.Name + "Count" + text2;
			}
			sb.AppendLine(text + "for (int i" + text2 + " = 0; i" + text2 + " < " + text10 + "; i" + text2 + "++)");
			sb.AppendLine(text + "{");
			if (luminDataField5.Type == LuminFiledType.List)
			{
				sb.AppendLine($"{text}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{text2}, out int {luminDataField5.Name}Count_{depth + 1}))");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
				string fullGenericTypeName4 = GetFullGenericTypeName(luminDataField5.GenericType, luminDataField5.ClassName, luminDataField5.ClassGenericType);
				sb.AppendLine($"{text}    var element_{text2} = new List<{fullGenericTypeName4}>({luminDataField5.Name}Count_{depth + 1});");
				sb.AppendLine(text + "    " + field.Name + "TempSpan" + text2 + "[i" + text2 + "] = element_" + text2 + ";");
			}
			else if (luminDataField5.Type == LuminFiledType.Array)
			{
				sb.AppendLine($"{text}    if (!reader.TryReadCollectionHead(ref {field.Name}ListOffset{text2}, out int {luminDataField5.Name}Count_{depth + 1}))");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        " + field.Name + "ListOffset" + text2 + " += 4;");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
				StringBuilder stringBuilder4 = new StringBuilder();
				for (int num4 = 1; num4 < field.GenericType.Count && field.GenericType[num4] == LuminGenericsType.Array; num4++)
				{
					stringBuilder4.Append("[]");
				}
				LuminGenericsType luminGenericsType3 = field.GenericType.Last();
				if (luminGenericsType3 != LuminGenericsType.Struct && luminGenericsType3 != LuminGenericsType.Class)
				{
					sb.AppendLine($"{text}    var element_{text2} = LuminPackMarshal.AllocateUninitializedArray<{GetGenericTypeName(luminGenericsType3)}{stringBuilder4}>({luminDataField5.Name}Count_{depth + 1});");
				}
				else
				{
					sb.AppendLine($"{text}    var element_{text2} = LuminPackMarshal.AllocateUninitializedArray<{field.ClassName}{stringBuilder4}>({luminDataField5.Name}Count_{depth + 1});");
				}
				sb.AppendLine(text + "    " + field.Name + "TempSpan" + text2 + "[i" + text2 + "] = element_" + text2 + ";");
			}
			else if (luminDataField5.Type == LuminFiledType.Class)
			{
				sb.AppendLine(text + "    if (reader.PeekIsNullObject(ref " + field.Name + "ListOffset" + text2 + "))");
				sb.AppendLine(text + "    {");
				sb.AppendLine(text + "        continue;");
				sb.AppendLine(text + "    }");
			}
			string text11 = field.Name + "TempSpan" + text2 + "[i" + text2 + "]";
			GenerateDeserializeCode(sb, luminDataField5, text11, span, field.Name + "ListOffset" + text2, indent + 1, depth + 1, isFirst: false, text11, isArray: true, isList: false, multList, isPrivateFiled: false, parentName + field.Name);
			LuminFiledType type = luminDataField5.Type;
			if (type != LuminFiledType.String && type != LuminFiledType.Class && type != LuminFiledType.Struct)
			{
				type = luminDataField5.Type;
				if (type - 17 <= LuminFiledType.UInt)
				{
					sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " = " + GetFieldLength(luminDataField5, depth + 1) + ";");
				}
				else
				{
					sb.AppendLine(text + "    " + field.Name + "ListOffset" + text2 + " += " + GetFieldLength(luminDataField5, depth + 1) + ";");
				}
			}
			sb.AppendLine(text + "}");
			break;
		}
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
		{
			if (IsPureValueTypeStruct(field))
			{
				sb.AppendLine(text + "// 纯值类型结构体");
				sb.AppendLine(text + offset + " += reader.ReadUnmanaged(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "// 反序列化" + field.ClassName);
			sb.AppendLine(text + offset + " += 1;");
			bool flag2 = CanDeserializeDirectlyIntoNestedObject(field);
			string text4 = parentName + field.Name + "Result" + text2;
			if (flag2)
			{
				if (string.IsNullOrWhiteSpace(field.FullTypeName))
				{
					sb.AppendLine(text + "var " + text4 + " = " + targetObj + " = new();");
				}
				else
				{
					sb.AppendLine(text + "var " + text4 + " = new " + field.FullTypeName + "();");
				}
			}
			if (!flag2)
			{
				sb.AppendLine(text + "// 定义嵌套类字段的局部变量");
				foreach (LuminDataField classField in field.ClassFields)
				{
					string text5 = ((classField.FieldType == LuminDataType.Reference) ? "?" : "");
					sb.AppendLine(text + GetFieldLocalVariableType(classField) + text5 + " " + parentName + field.Name + classField.Name + "Temp" + text2 + " = default!;");
				}
				sb.AppendLine();
			}
			for (int j = 0; j < field.ClassFields.Count; j++)
			{
				LuminDataField luminDataField2 = field.ClassFields[j];
				string text6 = (flag2 ? (text4 + "." + luminDataField2.Identifier) : (parentName + field.Name + luminDataField2.Name + "Temp" + text2));
				luminDataField2.belongClassFieldName = field.Name;
				int num = indent;
				if (IsMergeableField(luminDataField2))
				{
					int num2 = FindNextMergeableField(field.ClassFields, j);
					if (num2 != j)
					{
						if (num2 - j >= 14)
						{
							num2 = j + 14;
						}
						sb.Append(text + offset + " += reader.ReadUnmanaged(ref " + offset);
						for (int k = j; k <= num2; k++)
						{
							LuminDataField luminDataField3 = field.ClassFields[k];
							string text7 = (flag2 ? (text4 + "." + luminDataField3.Identifier) : (parentName + field.Name + luminDataField3.Name + "Temp" + text2));
							sb.Append(", out " + text7);
						}
						sb.Append(");");
						sb.AppendLine();
						j = num2;
						continue;
					}
				}
				switch (luminDataField2.Type)
				{
				case LuminFiledType.List:
				case LuminFiledType.Array:
					sb.AppendLine(text + "if (reader.PeekIsNullCollection(ref " + offset + "))");
					sb.AppendLine(text + "{");
					sb.AppendLine(text + "    " + offset + " += 4;");
					sb.AppendLine(text + "    " + text6 + " = default;");
					sb.AppendLine(text + "}");
					sb.AppendLine(text + "else");
					num++;
					break;
				case LuminFiledType.Class:
					sb.AppendLine(text + "if (reader.PeekIsNullObject(ref " + offset + "))");
					sb.AppendLine(text + "{");
					sb.AppendLine(text + "    " + offset + " += 1;");
					sb.AppendLine(text + "    " + text6 + " = default;");
					sb.AppendLine(text + "}");
					sb.AppendLine(text + "else");
					num++;
					break;
				}
				if (num != indent)
				{
					sb.AppendLine(text + "{");
				}
				LuminFiledType type = luminDataField2.Type;
				bool flag3 = type - 17 <= LuminFiledType.UInt;
				int depth2 = ((!flag3) ? (depth + 1) : 0);
				GenerateDeserializeCode(sb, luminDataField2, text6, span, offset, num, depth2, isFirst: true, "", isArray: false, isList: false, multList + "_", isPrivateFiled: true, parentName + field.Name + "_", collectionHeadAlreadyRead: false, flag2);
				string text8 = ((num == indent) ? text : (text + "    "));
				type = field.ClassFields[j].Type;
				if (type != LuminFiledType.String && type != LuminFiledType.Class && type != LuminFiledType.Struct)
				{
					type = field.ClassFields[j].Type;
					if (type - 17 <= LuminFiledType.UInt)
					{
						sb.AppendLine(text8 + offset + " = " + GetFieldLength(luminDataField2, depth2, multList + "_") + ";");
					}
					else
					{
						sb.AppendLine(text8 + offset + " += " + GetFieldLength(luminDataField2, depth2, multList + "_") + ";");
					}
				}
				if (num != indent)
				{
					sb.AppendLine(text + "}");
				}
			}
			if (flag2)
			{
				if (!string.IsNullOrWhiteSpace(field.FullTypeName))
				{
					sb.AppendLine(text + targetObj + " = " + text4 + ";");
				}
				break;
			}
			List<string> list = new List<string>();
			if (field.SelectedConstructor != null && field.SelectedConstructor.Parameters.Count > 0)
			{
				foreach (ConstructorParameter param in field.SelectedConstructor.Parameters)
				{
					LuminDataField luminDataField4 = field.ClassFields.FirstOrDefault((LuminDataField f) => f.Name == param.MatchingFieldName);
					if (luminDataField4 != null)
					{
						list.Add(parentName + field.Name + luminDataField4.Name + "Temp" + text2);
					}
					else
					{
						list.Add("default");
					}
				}
			}
			string text9 = string.Join(", ", list);
			List<LuminDataField> list2 = field.ClassFields.Where((LuminDataField f) => (field.SelectedConstructor == null || !field.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && !f.IsPrivate).ToList();
			List<LuminDataField> list3 = field.ClassFields.Where((LuminDataField f) => (field.SelectedConstructor == null || !field.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && f.IsPrivate).ToList();
			if (field.RentPoolMethod != null)
			{
				if (field.RentPoolMethod.ReturnsByRef)
				{
					sb.AppendLine("            value = ref " + field.FullTypeName + "." + ((ISymbol)field.RentPoolMethod).Name + "();");
				}
				else
				{
					sb.AppendLine(text + targetObj + " = " + field.FullTypeName + "." + ((ISymbol)field.RentPoolMethod).Name + "();");
				}
				sb.AppendLine(text + "// 设置所有字段（对象池分配）");
				{
					foreach (LuminDataField classField2 in field.ClassFields)
					{
						if (classField2.IsPrivate)
						{
							sb.AppendLine(text + "Get" + field.Name + classField2.Name + "(" + targetObj + ") = " + parentName + field.Name + classField2.Name + "Temp" + text2 + ";");
						}
						else
						{
							sb.AppendLine(text + targetObj + "." + classField2.Identifier + " = " + parentName + field.Name + classField2.Name + "Temp" + text2 + ";");
						}
					}
					break;
				}
			}
			if (list2.Count > 0)
			{
				if (field.SelectedConstructor != null && field.SelectedConstructor.Parameters.Count > 0)
				{
					sb.AppendLine(text + targetObj + " = new " + field.FullTypeName + "(" + text9 + ")");
				}
				else
				{
					sb.AppendLine(text + targetObj + " = new " + field.FullTypeName + "()");
				}
				sb.AppendLine(text + "{");
				foreach (LuminDataField item in list2)
				{
					sb.AppendLine(text + "    " + item.Name + " = " + parentName + field.Name + item.Name + "Temp" + text2 + ",");
				}
				sb.AppendLine(text + "};");
			}
			else if (field.SelectedConstructor != null && field.SelectedConstructor.Parameters.Count > 0)
			{
				sb.AppendLine(text + targetObj + " = new " + field.FullTypeName + "(" + text9 + ");");
			}
			else
			{
				sb.AppendLine(text + targetObj + " = new " + field.FullTypeName + "();");
			}
			if (list3.Count <= 0)
			{
				break;
			}
			sb.AppendLine(text + "// 设置private字段");
			{
				foreach (LuminDataField item2 in list3)
				{
					sb.AppendLine(text + "Get" + field.Name + item2.Name + "(" + targetObj + ") = " + parentName + field.Name + item2.Name + "Temp" + text2 + ";");
				}
				break;
			}
		}
		case LuminFiledType.Enum:
			if (isFirst)
			{
				sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn<" + field.TypeName + ">(ref " + offset + ", out " + targetObj + ");");
				break;
			}
			sb.AppendLine(text + "reader.ReadUnmanagedWithoutSizeReturn<" + field.TypeName + ">(ref " + offset + ", out var " + field.Name + "TempValue" + text2 + ");");
			sb.AppendLine(text + targetObj + " = " + field.Name + "TempValue" + text2 + ";");
			break;
		default:
			if (field.IsCompress)
			{
				sb.AppendLine(text + "reader.ReadValueWithCompress(ref " + targetObj + ");");
			}
			else if (targetIsKnownFresh && IsExactFreshCollectionType(field) &&
			         field.TypeSymbol is not null && !ContainsTypeParameter(field.TypeSymbol))
			{
				sb.AppendLine(text + "reader.ReadFreshValue(ref " + targetObj + ");");
			}
			else
			{
				sb.AppendLine(text + "reader.ReadValue(ref " + targetObj + ");");
			}
			break;
		}
	}

	private static string GetEnumTypeName(LuminDataField field)
	{
		return $"{field.EnumType}";
	}

	private static string GetFullTypeName(LuminDataField field)
	{
		LuminFiledType type = field.Type;
		if (type - 15 > LuminFiledType.UInt)
		{
			switch (field.ClassName)
			{
			case "Your Class Name":
			case "":
			case null:
				return field.Type.ToString();
			}
		}
		StringBuilder stringBuilder = new StringBuilder();
		if (!string.IsNullOrEmpty(field.NameSpace) && field.NameSpace != "Your Data NameSpace" && !field.ClassName.Contains("."))
		{
			stringBuilder.Append(field.NameSpace + ".");
		}
		stringBuilder.Append(field.ClassName);
		if (field.ClassName.Contains("<") || field.ClassName.Contains(">"))
		{
			return stringBuilder.ToString();
		}
		if (!string.IsNullOrEmpty(field.ClassGenericType))
		{
			stringBuilder.Append("<");
			stringBuilder.Append(field.ClassGenericType);
			stringBuilder.Append(">");
		}
		return stringBuilder.ToString();
	}

	private static string GetFieldLocalVariableType(LuminDataField field)
	{
		string text = field.TypeName;
		if (string.IsNullOrEmpty(text) || text.EndsWith("?"))
		{
			text = (string.IsNullOrEmpty(field.FullTypeName) ? (text?.TrimEnd(new char[1] { '?' }) ?? "object") : field.FullTypeName);
		}
		return text.TrimEnd(new char[1] { '?' });
	}

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
			throw new InvalidOperationException();
			LuminFiledType result = default(LuminFiledType);
			return result;
		}
		}
	}

	private static LuminFiledType ConvertGenericsToFieldType(List<LuminGenericsType> genericTypes)
	{
		if (genericTypes.Count == 0)
		{
			throw new ArgumentException("Empty generic types");
		}
		switch (genericTypes[0])
		{
		case LuminGenericsType.List:
			if (genericTypes.Count < 2)
			{
				throw new ArgumentException("Insufficient generic parameters for List");
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
			throw new InvalidOperationException();
			LuminFiledType result = default(LuminFiledType);
			return result;
		}
		}
	}

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
			throw new InvalidOperationException();
			string result = default(string);
			return result;
		}
		}
	}

	private static string GetSerializeFieldLength(LuminDataField field, int depth, string stringRecordLength, string multList = "_")
	{
		return GetFieldLength(field, depth, multList, "writer").Replace("writer.StringRecordLength()", stringRecordLength);
	}

	private static string GetFieldLength(LuminDataField field, int depth, string multList = "_", string pattern = "null")
	{
		string text = $"{multList}{depth}";
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
			return field.Name + "ListOffset" + text;
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
			return string.Join(" + ", field.ClassFields.Select((LuminDataField sub) => GetFieldLength(sub, depth + 1)));
		case LuminFiledType.Enum:
			return GetEnumFieldLength(field.EnumType);
		case LuminFiledType.Other:
			return "0";
		default:
		{
			throw new InvalidOperationException();
			string result = default(string);
			return result;
		}
		}
	}

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
			throw new InvalidOperationException();
			string result = default(string);
			return result;
		}
		}
	}

	private static string GetListLength(LuminDataField field, int depth)
	{
		string text = $"_{depth}";
		switch (field.GenericType.Last())
		{
		case LuminGenericsType.Byte:
		case LuminGenericsType.SByte:
			return "4 + " + field.Name + "Count" + text + " * 1";
		case LuminGenericsType.Bool:
			return "4 + (" + field.Name + "Count" + text + " * 1)";
		case LuminGenericsType.Short:
		case LuminGenericsType.UShort:
		case LuminGenericsType.Char:
			return "4 + (" + field.Name + "Count" + text + " * 2)";
		case LuminGenericsType.Int:
		case LuminGenericsType.UInt:
			return "4 + (" + field.Name + "Count" + text + " * 4)";
		case LuminGenericsType.Long:
		case LuminGenericsType.ULong:
			return "4 + (" + field.Name + "Count" + text + " * 8)";
		case LuminGenericsType.Float:
			return "4 + (" + field.Name + "Count" + text + " * 4)";
		case LuminGenericsType.Double:
			return "4 + (" + field.Name + "Count" + text + " * 8)";
		case LuminGenericsType.Decimal:
			return "4 + (" + field.Name + "Count" + text + " * 16)";
		case LuminGenericsType.String:
			return "4 + " + field.Name + "Count" + text + " * (" + field.Name + "Count" + text + " + 1)";
		case LuminGenericsType.Class:
			return "4 + " + field.Name + "Count" + text + " * " + field.Name + "Count" + text;
		case LuminGenericsType.Struct:
			return "4 + " + field.Name + "Count" + text + " * " + field.Name + "Count" + text;
		case LuminGenericsType.List:
			return "4 + " + field.Name + "Count" + text + " * " + field.Name + "Count" + text;
		case LuminGenericsType.Enum:
			return "4 + " + field.Name + "Count" + text + " * " + GetEnumFieldLength(field.EnumType);
		default:
		{
			throw new InvalidOperationException();
			string result = default(string);
			return result;
		}
		}
	}

	private static string GetFullGenericTypeName(List<LuminGenericsType> genericTypes, string classname = "", string genericType = "")
	{
		if (genericTypes.Count == 0)
		{
			throw new ArgumentException("Empty generic types");
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
			throw new InvalidOperationException();
			bool result = default(bool);
			return result;
		}
		}
	}

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
			throw new InvalidOperationException();
			bool result = default(bool);
			return result;
		}
		}
	}

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

	private static bool IsPureValueTypeStruct(LuminDataField field)
	{
		if (field.Type != LuminFiledType.Struct)
		{
			return false;
		}
		if (field.ClassFields.Count == 0)
		{
			return false;
		}
		foreach (LuminDataField classField in field.ClassFields)
		{
			if (classField.FieldType == LuminDataType.Reference)
			{
				return false;
			}
			if (classField.Type == LuminFiledType.Struct)
			{
				if (!IsPureValueTypeStruct(classField))
				{
					return false;
				}
			}
			else if (classField.Type == LuminFiledType.Array || classField.Type == LuminFiledType.List || classField.Type == LuminFiledType.String)
			{
				return false;
			}
		}
		return true;
	}

	private static bool IsPureUnmanagedStruct(LuminDataInfo data)
	{
		return data.isValueType && data.TypeSymbol?.IsUnmanagedType == true;
	}

	private static bool HasContinuousPureValueTypeStructs(List<LuminDataField> fields, int startIndex, out int count)
	{
		count = 0;
		for (int i = startIndex; i < fields.Count; i++)
		{
			LuminDataField luminDataField = fields[i];
			if (luminDataField.Type != LuminFiledType.Struct || !IsPureValueTypeStruct(luminDataField))
			{
				break;
			}
			count++;
		}
		return count > 0;
	}

	private static bool IsMergeableField(LuminDataField field)
	{
		if (!IsUnmanagedFiledType(field.Type) && (field.Type != LuminFiledType.Struct || !IsPureValueTypeStruct(field)) && !FormatterDiscovery.KnownValueTypes.Contains(field.TypeName))
		{
			return FormatterDiscovery.KnownValueTypes.Contains("global::" + field.TypeName);
		}
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNextMergeableField(LuminDataInfo data, int index)
	{
		for (int i = index + 1; i < data.fields.Count && IsMergeableField(data.fields[i]); i++)
		{
			index++;
		}
		return index;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNextMergeableField(List<LuminDataField> data, int index)
	{
		for (int i = index + 1; i < data.Count && IsMergeableField(data[i]); i++)
		{
			index++;
		}
		return index;
	}

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
			_ => "Unsafe.SizeOf<" + GetGeneratedTypeName(field) + ">()>",
		};
	}

	private static string GetGeneratedTypeName(LuminDataField field)
	{
		return field.TypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
			?? field.TypeName;
	}

	private static int GetArrayDepth(List<LuminGenericsType> genericTypes)
	{
		int num = 0;
		using (List<LuminGenericsType>.Enumerator enumerator = genericTypes.GetEnumerator())
		{
			while (enumerator.MoveNext() && enumerator.Current == LuminGenericsType.Array)
			{
				num++;
			}
		}
		return num;
	}

	private static string GetArrayTypeName(List<LuminGenericsType> genericTypes)
	{
		int arrayDepth = GetArrayDepth(genericTypes);
		return genericTypes.Last() switch
		{
			LuminGenericsType.Int => "int",
			LuminGenericsType.String => "string",
			_ => "object",
		} + new string('[', arrayDepth) + new string(']', arrayDepth);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNextUnmanagedType(LuminDataInfo data, int index)
	{
		for (int i = index + 1; i < data.fields.Count && IsUnmanagedFiledType(data.fields[i].Type); i++)
		{
			index++;
		}
		return index;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int FindNextUnmanagedType(List<LuminDataField> data, int index)
	{
		for (int i = index + 1; i < data.Count && IsUnmanagedFiledType(data[i].Type); i++)
		{
			index++;
		}
		return index;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool FindAllUnmanagedType(List<LuminDataField> datas)
	{
		bool flag;
		foreach (LuminDataField data in datas)
		{
			LuminFiledType type = data.Type;
			flag = ((type == LuminFiledType.String || type - 16 <= LuminFiledType.SByte) ? true : false);
			if (flag)
			{
				flag = false;
			}
			else
			{
				if (data.Type != LuminFiledType.Struct || FindAllUnmanagedType(data.ClassFields))
				{
					continue;
				}
				flag = false;
			}
			goto IL_0067;
		}
		return true;
		IL_0067:
		return flag;
	}

	private static string GetClassConstructParameter(LuminDataField field)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < field.ConstructParameterCount; i++)
		{
			if (i == 0)
			{
				stringBuilder.Append("default");
			}
			else
			{
				stringBuilder.Append(", default");
			}
		}
		return stringBuilder.ToString();
	}

	public static void GeneratorUnsafeAccessorMethod(StringBuilder sb, LuminDataField baseField, List<LuminDataField> fileds)
	{
		foreach (LuminDataField filed in fileds)
		{
			if (filed.ClassFields.Count > 0)
			{
				GeneratorUnsafeAccessorMethod(sb, filed, filed.ClassFields);
			}
			if (filed.IsPrivate)
			{
				string text = ((!string.IsNullOrEmpty(filed.belongClassName)) ? filed.belongClassName : GetFullTypeName(baseField));
				sb.AppendLine(filed.isProperty ? ("        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = \"<" + filed.Name + ">k__BackingField\")]") : ("        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = \"" + filed.Name + "\")]"));
				sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
				sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
				sb.AppendLine("        public static extern ref " + filed.TypeName + " Get" + baseField.Name + filed.Name + "(in " + text + " value);");
				sb.AppendLine();
			}
		}
	}

	public static void GeneratorUnsafeAccessorMethod(StringBuilder sb, LuminDataField baseField, List<LuminDataField> fileds, HashSet<string> analyzedTypes)
	{
		foreach (LuminDataField filed in fileds)
		{
			if (filed.ClassFields.Count > 0)
			{
				GeneratorUnsafeAccessorMethod(sb, filed, filed.ClassFields, analyzedTypes);
			}
			if (filed.IsPrivate)
			{
				string text = ((!string.IsNullOrEmpty(filed.belongClassName)) ? filed.belongClassName : GetFullTypeName(baseField));
				string item = "public static extern ref " + filed.TypeName + " Get" + baseField.Name + filed.Name + "(" + text + " value);";
				if (analyzedTypes.Add(item))
				{
					sb.AppendLine(filed.isProperty ? ("        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = \"<" + filed.Name + ">k__BackingField\")]") : ("        [UnsafeAccessor(UnsafeAccessorKind.Field, Name = \"" + filed.Name + "\")]"));
					sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
					sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
					sb.AppendLine("        private static extern ref " + filed.TypeName + " Get" + baseField.Name + filed.Name + "(in " + text + " value);");
					sb.AppendLine();
				}
			}
		}
	}

	public static void GenerateLocalClassStructure(StringBuilder sb, LuminDataInfo dataInfo)
	{
		List<LuminDataInfo> list = new List<LuminDataInfo>();
		for (LuminDataInfo parent = dataInfo.Parent; parent != null; parent = parent.Parent)
		{
			list.Add(parent);
		}
		GenerateSingleLocalClass(sb, dataInfo);
		foreach (LuminDataInfo item in list)
		{
			GenerateParentClass(sb, item, dataInfo.localFields);
		}
	}

	public static void GenerateLocalClassStructure(StringBuilder sb, LuminDataInfo dataInfo, HashSet<string> set)
	{
		GenerateLocalClassStructure(sb, dataInfo, set, static _ => true);
	}

	public static void GenerateLocalClassStructure(
		StringBuilder sb,
		LuminDataInfo dataInfo,
		HashSet<string> set,
		Func<LuminDataInfo, bool> shouldGenerate)
	{
		List<LuminDataInfo> list = new List<LuminDataInfo>();
		for (LuminDataInfo parent = dataInfo.Parent; parent != null; parent = parent.Parent)
		{
			list.Add(parent);
		}
		if (shouldGenerate(dataInfo) && set.Add(GetLocalLayoutKey(dataInfo)))
		{
			GenerateSingleLocalClass(sb, dataInfo);
		}
		foreach (LuminDataInfo item in list)
		{
			if (shouldGenerate(item) && set.Add(GetLocalLayoutKey(item)))
			{
				GenerateParentClass(sb, item, dataInfo.localFields);
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static string GetLocalLayoutKey(LuminDataInfo dataInfo)
	{
		return TypeMetaChecker.BuildLocalMyClassName(dataInfo) + "|arity:" + dataInfo.GenericParameters.Count + "|LocalForLuminPackExtension1782819";
	}

	private static void GenerateParentClass(StringBuilder sb, LuminDataInfo classInfo, List<LuminLocalFieldData> allClassInfos)
	{
		string text = TypeMetaChecker.BuildLocalMyClassName(classInfo);
		string text2 = "";
		if (classInfo.Parent != null)
		{
			text2 = " : " + TypeMetaChecker.BuildLocalClassName(classInfo.Parent);
		}
		string text3 = ((classInfo.GenericParameters.Count == 0) ? string.Empty : ("<" + string.Join(", ", classInfo.GenericParameters) + ">"));
		switch (classInfo.structLayout)
		{
		case StructLayout.Explicit:
			sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]");
			break;
		case StructLayout.Auto:
			sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Auto)]");
			break;
		case StructLayout.Sequential:
			sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]");
			break;
		}
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine(classInfo.isValueType ? ("        private struct " + text + text3 + text2) : ("        private class " + text + text3 + text2));
		foreach (GenericParameterConstraint genericConstraint in classInfo.GenericConstraints)
		{
			if (genericConstraint.IsUnmanaged || genericConstraint.IsClass || genericConstraint.IsStruct || genericConstraint.IsNotNull || genericConstraint.HasDefault || genericConstraint.HasNewConstructor || genericConstraint.Constraints.Count != 0)
			{
				sb.Append("            ");
				sb.Append("where ");
				sb.Append(genericConstraint.ParameterName);
				sb.Append(" : ");
				List<string> list = new List<string>();
				if (genericConstraint.IsUnmanaged)
				{
					list.Add("unmanaged");
				}
				if (genericConstraint.IsClass)
				{
					list.Add("class");
				}
				if (genericConstraint.IsStruct)
				{
					list.Add("struct");
				}
				if (genericConstraint.IsNotNull)
				{
					list.Add("notnull");
				}
				if (genericConstraint.HasNewConstructor)
				{
					list.Add("new()");
				}
				if (genericConstraint.HasDefault)
				{
					list.Add("default");
				}
				list.AddRange(genericConstraint.Constraints);
				sb.Append(string.Join(", ", list));
				sb.AppendLine();
			}
		}
		sb.AppendLine("        {");
		foreach (LuminDataField field in classInfo.fields)
		{
			LuminLocalFieldData luminLocalFieldData = allClassInfos.FirstOrDefault((LuminLocalFieldData f) => f.Name == field.Name);
			if (luminLocalFieldData != null)
			{
				if (classInfo.structLayout == StructLayout.Explicit)
				{
					sb.AppendLine($"            [global::System.Runtime.InteropServices.FieldOffset({luminLocalFieldData.filedOffset})]");
				}
				string text4 = ((IsUnmanagedFiledType(luminLocalFieldData.TypeName) || luminLocalFieldData.IsValue) ? string.Empty : "?");
				sb.AppendLine("            internal " + luminLocalFieldData.TypeName + text4 + " " + luminLocalFieldData.Name + ";");
			}
		}
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static void GenerateSingleLocalClass(StringBuilder sb, LuminDataInfo classInfo)
	{
		string text = TypeMetaChecker.BuildLocalMyClassName(classInfo);
		string text2 = "";
		if (classInfo.Parent != null)
		{
			text2 = " : " + TypeMetaChecker.BuildLocalClassName(classInfo.Parent);
		}
		string text3 = ((classInfo.GenericParameters.Count == 0) ? string.Empty : ("<" + string.Join(", ", classInfo.GenericParameters) + ">"));
		switch (classInfo.structLayout)
		{
		case StructLayout.Explicit:
			sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]");
			break;
		case StructLayout.Auto:
			sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Auto)]");
			break;
		case StructLayout.Sequential:
			sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]");
			break;
		}
		sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
		sb.AppendLine(classInfo.isValueType ? ("        private struct " + text + text3 + text2) : ("        private class " + text + text3 + text2));
		sb.AppendLine("        {");
		foreach (LuminLocalFieldData item in GetMyLocalFiled(classInfo))
		{
			if (classInfo.structLayout == StructLayout.Explicit)
			{
				sb.AppendLine($"            [global::System.Runtime.InteropServices.FieldOffset({item.filedOffset})]");
			}
			string text4 = ((IsUnmanagedFiledType(item.TypeName) || item.IsValue) ? string.Empty : "?");
			sb.AppendLine("            internal " + item.TypeName + text4 + " " + item.Identifier + ";");
		}
		sb.AppendLine("        }");
		sb.AppendLine();
	}

	private static List<LuminLocalFieldData> GetMyLocalFiled(LuminDataInfo dataInfo)
	{
		List<LuminDataInfo> list = new List<LuminDataInfo>();
		for (LuminDataInfo parent = dataInfo.Parent; parent != null; parent = parent.Parent)
		{
			list.Add(parent);
		}
		List<LuminLocalFieldData> list2 = new List<LuminLocalFieldData>(dataInfo.localFields);
		foreach (LuminDataInfo item in list)
		{
			foreach (LuminDataField field in item.fields)
			{
				list2.Remove(list2.FirstOrDefault((LuminLocalFieldData x) => x.Name == field.Name));
			}
		}
		return list2;
	}

	public static void GenerateCalculateOffsetCode(LuminDataInfo data, StringBuilder stringBuilder)
	{
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

	public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb, bool extension = false, bool polymorphism = false)
	{
		string text = TypeMetaChecker.BuildFormatterClassName(data);
		string text2 = data.classFullName;
		_ = data.className + "Formatter";
		if (data.isGeneric)
		{
			text = text + "<" + data.GenericParameters.FirstOrDefault();
			for (int i = 1; i < data.GenericParameters.Count; i++)
			{
				text = text + "," + data.GenericParameters[i];
			}
			text += ">";
		}
		if (!text2.Contains(".") && data.classNameSpace != "<global namespace>")
		{
			text2 = "global::" + data.classNameSpace + "." + data.classFullName;
		}
		foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerializing))
		{
			sb.AppendLine(item.Item3 ? ("            " + text2 + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
		}
		if (IsPureUnmanagedStruct(data))
		{
			sb.AppendLine("            ref int offset = ref writer.GetCurrentSpanOffset();");
			sb.AppendLine("            writer.Advance(writer.WriteUnmanaged(ref offset, value));");
			foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerialized))
			{
				sb.AppendLine(item.Item3 ? ("            " + text2 + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
			}
			sb.AppendLine();
			return;
		}
		if (!data.isValueType && !polymorphism)
		{
			sb.AppendLine("            if (value is null)");
			sb.AppendLine("            {");
			sb.AppendLine("                writer.WriteNullObjectHeader();");
			sb.AppendLine("                writer.Advance(1);");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
		}
		bool flag = !data.isValueType && !polymorphism && data.fields.Count > 16;
		if (flag)
		{
			sb.AppendLine("            var serializeValue = value;");
		}
		bool num = data.fields.Count(ContainsSerializedStringRecord) > 1;
		string text3 = (num ? "serializeStringRecordLength" : "writer.StringRecordLength()");
		if (num)
		{
			sb.AppendLine("            int serializeStringRecordLength = writer.StringRecordLength();");
		}
		if (data.fields.Count((LuminDataField x) => x.IsPrivate || x.isProperty) > 0)
		{
			sb.AppendLine(extension ? ("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref Unsafe.AsRef(in value));") : ("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);"));
		}
		bool flag2 = false;
		foreach (LuminDataField field in data.fields)
		{
			if (IsPureValueTypeStruct(field) || IsUnmanagedFiledType(field.Type))
			{
				flag2 = true;
				continue;
			}
			flag2 = false;
			break;
		}
		if (!polymorphism || !flag2 || data.fields.Count == 1)
		{
			sb.AppendLine("            ref int offset = ref writer.GetCurrentSpanOffset();");
		}
		int num2 = 0;
		if (!polymorphism)
		{
			if (data.fields.Count > 0 && IsMergeableField(data.fields[0]))
			{
				int num3 = Math.Min(FindNextMergeableField(data, 0), 13);
				sb.Append($"            writer.Advance(writer.WriteUnmanaged((byte){data.fields.Count}");
				for (int num4 = 0; num4 <= num3; num4++)
				{
					string text4 = ((data.fields[num4].IsPrivate || data.fields[num4].isProperty) ? "local" : (flag ? "serializeValue" : "value"));
					sb.Append(", " + text4 + "." + data.fields[num4].Identifier);
				}
				sb.AppendLine("));");
				num2 = num3 + 1;
			}
			else
			{
				sb.AppendLine($"            writer.WriteObjectHeader(ref offset, {data.fields.Count});");
				sb.AppendLine("            writer.Advance(1);");
			}
		}
		sb.AppendLine();
		for (int num5 = num2; num5 < data.fields.Count; num5++)
		{
			string text5 = ((data.fields[num5].IsPrivate || data.fields[num5].isProperty) ? "local" : (flag ? "serializeValue" : "value"));
			LuminFiledType type;
			if (data.fields[num5].FieldType == LuminDataType.Reference && data.fields[num5].Type != LuminFiledType.Other)
			{
				string text6 = "serialize" + data.fields[num5].Name + "Field";
				sb.AppendLine("            if (" + text5 + "." + data.fields[num5].Identifier + " is { } " + text6 + ")");
				sb.AppendLine("            {");
				GenerateSerializeCode(sb, data.fields[num5], text6, "span", "offset", 4, 0, isMultClass: false, text3);
				if (data.fields[num5].Type != LuminFiledType.Class)
				{
					string serializeFieldLength = GetSerializeFieldLength(data.fields[num5], 0, text3);
					type = data.fields[num5].Type;
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
				switch (data.fields[num5].Type)
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
					sb.AppendLine("                offset += " + text3 + ";");
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
			if (IsMergeableField(data.fields[num5]))
			{
				int num6 = FindNextMergeableField(data, num5);
				if (num6 != num5)
				{
					if (num6 - num5 >= 14)
					{
						num6 = num5 + 14;
					}
					sb.Append("            writer.Advance(writer.WriteUnmanaged(");
					for (int num7 = num5; num7 <= num6; num7++)
					{
						text5 = (data.fields[num7].IsPrivate ? "local" : (flag ? "serializeValue" : "value"));
						if (num7 == num5)
						{
							sb.Append(text5 + "." + data.fields[num7].Identifier);
						}
						else
						{
							sb.Append(", " + text5 + "." + data.fields[num7].Identifier);
						}
					}
					sb.Append("));");
					sb.AppendLine();
					num5 = num6;
					continue;
				}
			}
			GenerateSerializeCode(sb, data.fields[num5], text5 + "." + data.fields[num5].Identifier, "span", "offset", 3, 0, isMultClass: false, text3);
			type = data.fields[num5].Type;
			if (type != LuminFiledType.Struct && type != LuminFiledType.Other)
			{
				sb.AppendLine("            writer.Advance(" + GetSerializeFieldLength(data.fields[num5], 0, text3) + ");");
			}
		}
		foreach (var item2 in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnSerialized))
		{
			sb.AppendLine(item2.Item3 ? ("            " + text2 + "." + item2.Item1 + "();") : ("            value?." + item2.Item1 + "();"));
		}
		sb.AppendLine();
	}

	public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb, bool polymorphism = false)
	{
		string text = TypeMetaChecker.BuildFormatterClassName(data);
		string text2 = data.classFullName;
		_ = data.className + "Formatter";
		if (data.isGeneric)
		{
			text = text + "<" + data.GenericParameters.FirstOrDefault();
			for (int i = 1; i < data.GenericParameters.Count; i++)
			{
				text = text + "," + data.GenericParameters[i];
			}
			text += ">";
		}
		if (!text2.Contains(".") && data.classNameSpace != "<global namespace>")
		{
			text2 = "global::" + data.classNameSpace + "." + data.classFullName;
		}
		sb.AppendLine();
		foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserializing))
		{
			sb.AppendLine(item.Item3 ? ("            " + text2 + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
		}
		if (IsPureUnmanagedStruct(data))
		{
			sb.AppendLine("            ref int offset = ref reader.GetCurrentSpanOffset();");
			sb.AppendLine("            reader.Advance(reader.ReadUnmanaged(ref offset, out value));");
			foreach (var item in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserialized))
			{
				sb.AppendLine(item.Item3 ? ("            " + text2 + "." + item.Item1 + "();") : ("            value?." + item.Item1 + "();"));
			}
			sb.AppendLine();
			return;
		}
		bool flag = data.RentPoolMethod == null && (data.SelectedConstructor == null || data.SelectedConstructor.Parameters.Count == 0);
		bool flag2 = flag && data.fields.All((LuminDataField field) => !ContainsNestedFormatterCall(field));
		sb.AppendLine();
		sb.AppendLine(flag2 ? "            int offset = reader.GetCurrentSpanIndex();" : "            ref int offset = ref reader.GetCurrentSpanOffset();");
		if (!flag || data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
		{
			sb.AppendLine("            // 定义局部变量存储字段值");
			foreach (LuminLocalFieldData localField in data.localFields)
			{
				if (flag && data.fields.Any(field => field.Name == localField.Name && CanDeserializeDirectlyIntoConstructedObject(field)))
				{
					continue;
				}
				string text3 = (localField.IsValue ? "" : "?");
				sb.AppendLine("            " + localField.TypeName + text3 + " " + localField.Name + "Temp = default!;");
			}
			sb.AppendLine();
		}
		if (!polymorphism && !data.isValueType)
		{
			sb.AppendLine("            if (reader.PeekIsNullObject(ref offset))");
			sb.AppendLine("            {");
			sb.AppendLine("                offset += 1;");
			if (flag2)
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
		if (flag)
		{
			sb.AppendLine("            var result = new " + text2 + "();");
		}
		for (int num = 0; num < data.fields.Count; num++)
		{
			LuminDataField luminDataField = data.fields[num];
			string text4 = (flag && CanDeserializeDirectlyIntoConstructedObject(luminDataField)) ? ("result." + luminDataField.Identifier) : (luminDataField.Name + "Temp");
			if (luminDataField.FieldType == LuminDataType.Reference && luminDataField.Type != LuminFiledType.Other)
			{
				switch (luminDataField.Type)
				{
				case LuminFiledType.List:
				case LuminFiledType.Array:
				{
					if (IsStringList(luminDataField))
					{
						sb.AppendLine("            " + text4 + " = reader.ReadStringListAndAdvance(ref offset)!;");
						break;
					}
					bool flag3 = luminDataField.Type == LuminFiledType.List || IsReferenceGenericType(luminDataField.GenericType.FirstOrDefault()) || !luminDataField.IsCompress;
					if (flag3)
					{
						sb.AppendLine("            if (!reader.TryReadCollectionHead(ref offset, out int " + luminDataField.Name + "Count_0))");
					}
					else
					{
						sb.AppendLine("            if (reader.PeekIsNullCollection(ref offset))");
					}
					sb.AppendLine("            {");
					sb.AppendLine("                offset += 4;");
					sb.AppendLine("                " + text4 + " = default;");
					sb.AppendLine("            }");
					sb.AppendLine("            else");
					sb.AppendLine("            {");
					GenerateDeserializeCode(sb, luminDataField, text4, "span", "offset", 4, 0, isFirst: true, "", isArray: false, isList: false, "_", isPrivateFiled: false, "", flag3);
					if (luminDataField.Type != LuminFiledType.String)
					{
						sb.AppendLine("                offset = " + GetFieldLength(luminDataField, 0, "_", "reader") + ";");
					}
					sb.AppendLine("            }");
					break;
				}
				case LuminFiledType.String:
					GenerateDeserializeCode(sb, luminDataField, text4, "span", "offset", 4, 0);
					break;
				default:
				{
					sb.AppendLine("            if (reader.PeekIsNullObject(ref offset))");
					sb.AppendLine("            {");
					sb.AppendLine("                offset += 1;");
					sb.AppendLine("                " + text4 + " = default;");
					sb.AppendLine("            }");
					sb.AppendLine("            else");
					sb.AppendLine("            {");
					GenerateDeserializeCode(sb, luminDataField, text4, "span", "offset", 4, 0);
					LuminFiledType type = luminDataField.Type;
					if (type != LuminFiledType.String && type != LuminFiledType.Class)
					{
						sb.AppendLine("                offset += " + GetFieldLength(luminDataField, 0, "_", "reader") + ";");
					}
					sb.AppendLine("            }");
					break;
				}
				}
				continue;
			}
			if (IsMergeableField(data.fields[num]))
			{
				int num2 = FindNextMergeableField(data, num);
				if (num2 != num)
				{
					if (num2 - num >= 14)
					{
						num2 = num + 14;
					}
					sb.Append("            offset += reader.ReadUnmanaged(ref offset, ");
					for (int num3 = num; num3 <= num2; num3++)
					{
						LuminDataField luminDataField2 = data.fields[num3];
						string text5 = (flag && CanDeserializeDirectlyIntoConstructedObject(luminDataField2)) ? ("result." + luminDataField2.Identifier) : (luminDataField2.Name + "Temp");
						if (num3 == num)
						{
							sb.Append("out " + text5);
						}
						else
						{
							sb.Append(", out " + text5);
						}
					}
					sb.Append(");");
					sb.AppendLine();
					num = num2;
					continue;
				}
			}
			bool flag4 = num > 0 && data.fields[num - 1].Type == LuminFiledType.Other;
			if (flag2 && luminDataField.Type == LuminFiledType.Other && !flag4)
			{
				sb.AppendLine("            reader.FlushCurrentIndex(offset);");
			}
			GenerateDeserializeCode(sb, luminDataField, text4, "span", "offset", 3, 0, isFirst: true, "", isArray: false, isList: false, "_", isPrivateFiled: false, "", collectionHeadAlreadyRead: false, flag && CanDeserializeDirectlyIntoConstructedObject(luminDataField));
			bool flag5 = num + 1 < data.fields.Count && data.fields[num + 1].Type == LuminFiledType.Other;
			if (flag2 && luminDataField.Type == LuminFiledType.Other && !flag5 && num + 1 < data.fields.Count)
			{
				sb.AppendLine("            offset = reader.GetCurrentSpanIndex();");
			}
			LuminFiledType type2 = luminDataField.Type;
			if (type2 != LuminFiledType.String && type2 != LuminFiledType.Struct && type2 != LuminFiledType.Other)
			{
				sb.AppendLine("            offset += " + GetFieldLength(luminDataField, 0, "_", "reader") + ";");
			}
		}
		sb.AppendLine();
		sb.AppendLine("            // 构造对象并设置字段");
		if (flag2 && (data.fields.Count == 0 || data.fields[data.fields.Count - 1].Type != LuminFiledType.Other))
		{
			sb.AppendLine("            reader.FlushCurrentIndex(offset);");
		}
		if (flag)
		{
			sb.AppendLine("            value = result;");
			if (data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
			{
				if (data.isValueType)
				{
					sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
				}
				else
				{
					sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
				}
				foreach (LuminDataField field in data.fields.Where(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
				{
					sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
				}
			}
		}
		else
		{
			List<string> list = new List<string>();
			if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
			{
				foreach (ConstructorParameter param in data.SelectedConstructor.Parameters)
				{
					LuminDataField luminDataField3 = data.fields.FirstOrDefault((LuminDataField f) => f.Name == param.MatchingFieldName);
					if (luminDataField3 != null)
					{
						list.Add(luminDataField3.Name + "Temp!");
					}
					else
					{
						list.Add("default");
					}
				}
			}
			string text6 = string.Join(", ", list);
			List<LuminDataField> list2 = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && !f.IsPrivate && !f.isProperty).ToList();
			List<LuminDataField> list3 = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && (f.IsPrivate || f.isProperty)).ToList();
			if (data.RentPoolMethod != null)
			{
				if (data.RentPoolMethod.ReturnsByRef)
				{
					sb.AppendLine("            value = ref " + text2 + "." + ((ISymbol)data.RentPoolMethod).Name + "()!;");
				}
				else
				{
					sb.AppendLine("            value = " + text2 + "." + ((ISymbol)data.RentPoolMethod).Name + "();");
				}
				sb.AppendLine("            // 设置所有字段（对象池分配）");
				if (data.isValueType)
				{
					sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
				}
				else
				{
					sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
				}
				foreach (LuminDataField field in data.fields)
				{
					sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
				}
			}
			else
			{
				if (list2.Count > 0)
				{
					if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
					{
						sb.AppendLine("            value = new " + text2 + "(" + text6 + ")");
					}
					else
					{
						sb.AppendLine("            value = new " + text2 + "()");
					}
					sb.AppendLine("            {");
					foreach (LuminDataField item2 in list2)
					{
						sb.AppendLine("                " + item2.Identifier + " = " + item2.Name + "Temp!,");
					}
					sb.AppendLine("            };");
				}
				else if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
				{
					sb.AppendLine("            value = new " + text2 + "(" + text6 + ");");
				}
				else
				{
					sb.AppendLine("            value = new " + text2 + "();");
				}
				if (list3.Count > 0)
				{
					sb.AppendLine("            // 设置private字段");
					if (data.isValueType)
					{
						sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
					}
					else
					{
						sb.AppendLine("            ref var local = ref LuminPackMarshal.As<" + text2 + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
					}
					foreach (LuminDataField item3 in list3)
					{
						sb.AppendLine("            local." + item3.Identifier + " = " + item3.Name + "Temp!;");
					}
				}
			}
		}
		foreach (var item4 in data.callBackMethods.Where(((string, SerializeCallBackType, bool) x) => x.Item2 == SerializeCallBackType.OnDeserialized))
		{
			sb.AppendLine(item4.Item3 ? ("            " + text2 + "." + item4.Item1 + "();") : ("            value?." + item4.Item1 + "();"));
		}
		sb.AppendLine();
	}

	private static bool ContainsNestedFormatterCall(LuminDataField field)
	{
		if (field.Type == LuminFiledType.Other)
		{
			return true;
		}
		foreach (LuminDataField classField in field.ClassFields)
		{
			if (classField.Type == LuminFiledType.Other || ContainsNestedFormatterCall(classField))
			{
				return true;
			}
		}
		return false;
	}

	private static bool ContainsSerializedStringRecord(LuminDataField field)
	{
		if (field.Type != LuminFiledType.String && !field.GenericType.Any((LuminGenericsType type) => type == LuminGenericsType.String))
		{
			return field.ClassFields.Any(ContainsSerializedStringRecord);
		}
		return true;
	}

	private static bool CanDeserializeDirectlyIntoConstructedObject(LuminDataField field)
	{
		return !field.IsPrivate && !field.isProperty;
	}

	private static bool CanDeserializeDirectlyIntoNestedObject(LuminDataField field)
	{
		if (field.Type == LuminFiledType.Class && field.GenericType.Count == 0 && field.ClassFields.Count > 0 && field.RentPoolMethod == null && (field.SelectedConstructor == null || field.SelectedConstructor.Parameters.Count == 0))
		{
			return field.ClassFields.All((LuminDataField subField) => !subField.IsPrivate && !subField.isProperty);
		}
		return false;
	}

	private static bool IsStringList(LuminDataField field)
	{
		if (field.Type == LuminFiledType.List && field.GenericType.Count == 1)
		{
			return field.GenericType[0] == LuminGenericsType.String;
		}
		return false;
	}

	private static bool IsExactFreshCollectionType(LuminDataField field)
	{
		string text = field.FullTypeName ?? field.TypeName ?? string.Empty;
		if (!text.EndsWith(">", StringComparison.Ordinal))
		{
			return false;
		}
		if (!text.StartsWith("global::System.Collections.Generic.List<", StringComparison.Ordinal) && !text.StartsWith("System.Collections.Generic.List<", StringComparison.Ordinal) && !text.StartsWith("global::System.Collections.Generic.Dictionary<", StringComparison.Ordinal))
		{
			return text.StartsWith("System.Collections.Generic.Dictionary<", StringComparison.Ordinal);
		}
		return true;
	}

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
