using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

public static class LuminPackJsonCodeGenerator
{
	private static LuminDataInfo? _dataInfo;

	internal unsafe static ulong ComputeUtf8Hash(string str)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(str);
		fixed (byte* input = bytes)
		{
			return ComputeXxHash3(input, bytes.Length);
		}
	}

	internal unsafe static ulong ComputeUtf16Hash(string str)
	{
		fixed (char* ptr = str)
		{
			byte* input = (byte*)ptr;
			return ComputeXxHash3(input, str.Length * 2);
		}
	}

	private unsafe static ulong ComputeXxHash3(byte* input, int length)
	{
		if (length == 0)
		{
			return 12661295479421362729uL;
		}
		if (length <= 16)
		{
			if (length > 8)
			{
				long num = *(long*)input ^ -7046029254386353067L;
				ulong num2 = (ulong)(*((long*)(input + length) - 1) ^ 0x27D4EB2F16566805L);
				long num3 = num + (long)num2 + length;
				long num4 = (num3 ^ (num3 >>> 37)) * 1609587929392839161L;
				return (ulong)(num4 ^ (num4 >>> 32));
			}
			if (length >= 4)
			{
				ulong num5 = *(uint*)input;
				ulong num6 = *((uint*)(input + length) - 1);
				ulong num7 = 2870177450012600261L + num5 + ((num6 << 32) | num6);
				ulong num8 = (num7 ^ (num7 >> 37)) * 1609587929392839161L;
				return num8 ^ (num8 >> 32);
			}
			byte num9 = *input;
			byte b = input[length >> 1];
			byte b2 = input[length - 1];
			uint num10 = (uint)((num9 << 16) | (b << 24) | b2 | (length << 8));
			long num11 = -7046029254386353131L + num10;
			long num12 = (num11 ^ (num11 >>> 37)) * 1609587929392839161L;
			return (ulong)(num12 ^ (num12 >>> 32));
		}
		ulong num13 = (ulong)(length * -7046029288634856825L);
		int num14 = length;
		byte* ptr = input;
		while (num14 >= 16)
		{
			ulong num15 = *(ulong*)ptr;
			ulong num16 = ((ulong*)ptr)[1];
			num13 += (ulong)((long)num15 * -7046029288634856825L);
			num13 += num16 * 1609587929392839161L;
			ptr += 16;
			num14 -= 16;
		}
		if (num14 > 0)
		{
			if (num14 >= 8)
			{
				num13 += (ulong)(*(long*)ptr * -7046029288634856825L);
				ptr += 8;
				num14 -= 8;
			}
			if (num14 >= 4)
			{
				num13 += (ulong)((long)(uint)(*(int*)ptr) * 1609587929392839161L);
				ptr += 4;
				num14 -= 4;
			}
			while (num14 > 0)
			{
				num13 += (ulong)(*ptr * -7046029288634856825L);
				ptr++;
				num14--;
			}
		}
		num13 ^= num13 >> 37;
		num13 *= 1609587929392839161L;
		return num13 ^ (num13 >> 32);
	}

	public static void GenerateStaticUtf8Fields(StringBuilder sb, LuminDataInfo data)
	{
		foreach (LuminDataField field in data.fields)
		{
			string text = "_utf8_" + field.Name;
			string text2 = "_utf16_" + field.Name;
			string arg = "_utf8Hash_" + field.Name;
			string arg2 = "_utf16Hash_" + field.Name;
			byte[] bytes = Encoding.UTF8.GetBytes(field.Name);
			string text3 = string.Join(", ", bytes.Select((byte b) => $"(byte){b}"));
			sb.AppendLine("        private static readonly byte[] " + text + " = new byte[] { " + text3 + " };");
			string text4 = string.Join(", ", field.Name.Select((char c) => "'" + EscapeChar(c) + "'"));
			sb.AppendLine("        private static readonly char[] " + text2 + " = new char[] { " + text4 + " };");
			ulong num = ComputeUtf8Hash(field.Name);
			sb.AppendLine($"        private const ulong {arg} = {num}UL;");
			ulong num2 = ComputeUtf16Hash(field.Name);
			sb.AppendLine($"        private const ulong {arg2} = {num2}UL;");
		}
	}

	private static string EscapeChar(char c)
	{
		return c switch
		{
			'\'' => "\\'",
			'\\' => "\\\\",
			'\n' => "\\n",
			'\r' => "\\r",
			'\t' => "\\t",
			_ => c.ToString(),
		};
	}

	public static void GenerateJsonMethods(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
	{
		_dataInfo = data;
		GenerateJsonSerialize(sb, data, classGlobalName, metaInfo);
		sb.AppendLine();
		GenerateJsonDeserialize(sb, data, classGlobalName, metaInfo);
	}

	public static void GenerateJsonSerialize(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
	{
		for (int i = 0; i < data.fields.Count; i++)
		{
			LuminDataField luminDataField = data.fields[i];
			string text = string.Join(", ", from b in Encoding.UTF8.GetBytes(luminDataField.Name)
				select $"(byte){b}");
			string text2 = string.Join(", ", luminDataField.Name.Select((char c) => "(char)'" + EscapeChar(c) + "'"));
			if (i == 0)
			{
				sb.AppendLine("        private static readonly byte[] _jsonPrefixUtf8_" + luminDataField.Name + " = new byte[] { (byte)'{', (byte)'\"', " + text + ", (byte)'\"', (byte)':' };");
				sb.AppendLine("        private static readonly char[] _jsonPrefixUtf16_" + luminDataField.Name + " = new char[] { (char)'{', (char)'\"', " + text2 + ", (char)'\"', (char)':' };");
			}
			else
			{
				sb.AppendLine("        private static readonly byte[] _jsonSepUtf8_" + luminDataField.Name + " = new byte[] { (byte)',', (byte)'\"', " + text + ", (byte)'\"', (byte)':' };");
				sb.AppendLine("        private static readonly char[] _jsonSepUtf16_" + luminDataField.Name + " = new char[] { (char)',', (char)'\"', " + text2 + ", (char)'\"', (char)':' };");
			}
		}
		sb.AppendLine();
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
		if (data.isValueType)
		{
			sb.AppendLine(metaInfo.IsNet8 ? ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + classGlobalName + " value)") : ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + classGlobalName + " value)"));
		}
		else
		{
			sb.AppendLine(metaInfo.IsNet8 ? ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + classGlobalName + "? value)") : ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + classGlobalName + "? value)"));
		}
		sb.AppendLine("        {");
		if (!data.isValueType)
		{
			sb.AppendLine("            if (value == null)");
			sb.AppendLine("            {");
			sb.AppendLine("                writer.WriteNull();");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
			sb.AppendLine();
		}
		if (data.fields.Count((LuminDataField x) => x.IsPrivate || x.isProperty) > 0)
		{
			if (data.isValueType)
			{
				sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
			}
			else
			{
				sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
			}
			sb.AppendLine();
		}
		if (data.fields.Count == 0)
		{
			sb.AppendLine("            writer.WriteObjectStart();");
			sb.AppendLine("            writer.WriteObjectEnd();");
			sb.AppendLine("        }");
			return;
		}
		sb.AppendLine();
		sb.AppendLine("            bool isUtf8 = writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8;");
		sb.AppendLine();
		for (int num = 0; num < data.fields.Count; num++)
		{
			LuminDataField luminDataField2 = data.fields[num];
			string text3 = ((luminDataField2.IsPrivate || luminDataField2.isProperty) ? "local" : "value");
			string text4 = ((num == 0) ? "Prefix" : "Sep");
			sb.AppendLine("            // 字段: " + luminDataField2.Name);
			sb.AppendLine("            if (isUtf8)");
			sb.AppendLine("                writer.WriteRaw(_json" + text4 + "Utf8_" + luminDataField2.Name + ");");
			sb.AppendLine("            else");
			sb.AppendLine("                writer.WriteRaw(_json" + text4 + "Utf16_" + luminDataField2.Name + ");");
			sb.AppendLine("            writer.SetFirstElement(true);");
			GenerateJsonSerializeField(sb, luminDataField2, text3 + "." + luminDataField2.Identifier, "            ");
			sb.AppendLine();
		}
		sb.AppendLine("            writer.WriteByteRaw((byte)'}');");
		sb.AppendLine("        }");
	}

	private static void GenerateJsonSerializeField(StringBuilder sb, LuminDataField field, string valueExpr, string indent)
	{
		if (field.FieldType == LuminDataType.Reference)
		{
			sb.AppendLine(indent + "if (" + valueExpr + " == null)");
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    writer.WriteNull();");
			sb.AppendLine(indent + "}");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "{");
			GenerateJsonSerializeFieldCore(sb, field, valueExpr, indent + "    ");
			sb.AppendLine(indent + "}");
		}
		else
		{
			GenerateJsonSerializeFieldCore(sb, field, valueExpr, indent);
		}
	}

	private static void GenerateJsonSerializeFieldCore(StringBuilder sb, LuminDataField field, string valueExpr, string indent)
	{
		switch (field.Type)
		{
		case LuminFiledType.Int:
			sb.AppendLine(indent + "writer.WriteInt(" + valueExpr + ");");
			break;
		case LuminFiledType.UInt:
			sb.AppendLine(indent + "writer.WriteUInt(" + valueExpr + ");");
			break;
		case LuminFiledType.Byte:
			sb.AppendLine(indent + "writer.WriteByte(" + valueExpr + ");");
			break;
		case LuminFiledType.SByte:
			sb.AppendLine(indent + "writer.WriteSByte(" + valueExpr + ");");
			break;
		case LuminFiledType.Short:
			sb.AppendLine(indent + "writer.WriteShort(" + valueExpr + ");");
			break;
		case LuminFiledType.UShort:
			sb.AppendLine(indent + "writer.WriteUShort(" + valueExpr + ");");
			break;
		case LuminFiledType.Long:
			sb.AppendLine(indent + "writer.WriteLong(" + valueExpr + ");");
			break;
		case LuminFiledType.ULong:
			sb.AppendLine(indent + "writer.WriteULong(" + valueExpr + ");");
			break;
		case LuminFiledType.Float:
			sb.AppendLine(indent + "writer.WriteFloat(" + valueExpr + ");");
			break;
		case LuminFiledType.Double:
			sb.AppendLine(indent + "writer.WriteDouble(" + valueExpr + ");");
			break;
		case LuminFiledType.Decimal:
			sb.AppendLine(indent + "writer.WriteDecimal(" + valueExpr + ");");
			break;
		case LuminFiledType.Char:
			sb.AppendLine(indent + "writer.WriteChar(" + valueExpr + ");");
			break;
		case LuminFiledType.String:
			sb.AppendLine(indent + "writer.WriteString(" + valueExpr + ");");
			break;
		case LuminFiledType.Bool:
			sb.AppendLine(indent + "writer.WriteBool(" + valueExpr + ");");
			break;
		case LuminFiledType.Enum:
			sb.AppendLine(indent + "writer.WriteInt((int)" + valueExpr + ");");
			break;
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
		case LuminFiledType.List:
		case LuminFiledType.Array:
		case LuminFiledType.Other:
		{
			string text = field.FullTypeName ?? field.Name;
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    var temp = " + valueExpr + ";");
			sb.AppendLine(indent + "    global::LuminPack.LuminPackParseProvider.Cache<" + text + ">.Parser?.SerializeJson(ref writer, ref temp);");
			sb.AppendLine(indent + "}");
			break;
		}
		default:
			sb.AppendLine(indent + "writer.WriteNull();");
			break;
		}
	}

	public static void GenerateJsonDeserialize(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
	{
		bool flag = CanDeserializeDirectlyIntoFreshResult(data);
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
		if (data.isValueType)
		{
			sb.AppendLine(metaInfo.IsNet8 ? ("        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + classGlobalName + " value)") : ("        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + classGlobalName + " value)"));
		}
		else
		{
			sb.AppendLine(metaInfo.IsNet8 ? ("        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + classGlobalName + "? value)") : ("        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + classGlobalName + "? value)"));
		}
		sb.AppendLine("        {");
		if (!data.isValueType)
		{
			sb.AppendLine("            if (reader.IsNull())");
			sb.AppendLine("            {");
			sb.AppendLine("                value = null;");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
			sb.AppendLine();
		}
		if (flag)
		{
			sb.AppendLine("            var result = new " + classGlobalName + "();");
		}
		else
		{
			sb.AppendLine("            // Temp vars");
			foreach (LuminDataField field in data.fields)
			{
				string text = field.FullTypeName ?? field.Name;
				string text2 = ((field.FieldType == LuminDataType.Reference) ? "?" : "");
				sb.AppendLine("            " + text + text2 + " " + field.Name + "Temp = default;");
			}
		}
		sb.AppendLine();
		sb.AppendLine("            reader.TryConsumeObjectStart();");
		sb.AppendLine();
		sb.AppendLine("            bool isUtf8 = reader.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8;");
		sb.AppendLine();
		sb.AppendLine("            while (reader.Read())");
		sb.AppendLine("            {");
		sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
		sb.AppendLine("                    break;");
		sb.AppendLine();
		sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
		sb.AppendLine("                    continue;");
		sb.AppendLine();
		sb.AppendLine("                ulong propHash;");
		sb.AppendLine("                if (isUtf8)");
		sb.AppendLine("                {");
		sb.AppendLine("                    var propNameUtf8 = reader.ReadStringUtf8();");
		sb.AppendLine("                    propHash = global::LuminPack.Internal.XxHash3.Hash64Utf8(propNameUtf8);");
		sb.AppendLine("                }");
		sb.AppendLine("                else");
		sb.AppendLine("                {");
		sb.AppendLine("                    var propNameUtf16 = reader.ReadStringUtf16();");
		sb.AppendLine("                    propHash = global::LuminPack.Internal.XxHash3.Hash64Utf16(propNameUtf16);");
		sb.AppendLine("                }");
		sb.AppendLine();
		sb.AppendLine("                if (!reader.Read())");
		sb.AppendLine("                    break;");
		sb.AppendLine();
		sb.AppendLine("                if (isUtf8)");
		sb.AppendLine("                {");
		sb.AppendLine("                    switch (propHash)");
		sb.AppendLine("                    {");
		foreach (LuminDataField field2 in data.fields)
		{
			sb.AppendLine("                        case _utf8Hash_" + field2.Name + ":");
			sb.AppendLine("                        {");
			GenerateJsonDeserializeField(sb, field2, flag ? ("result." + field2.Identifier) : (field2.Name + "Temp"), "                            ");
			sb.AppendLine("                            break;");
			sb.AppendLine("                        }");
		}
		sb.AppendLine("                        default:");
		sb.AppendLine("                        {");
		sb.AppendLine("                            reader.Skip();");
		sb.AppendLine("                            break;");
		sb.AppendLine("                        }");
		sb.AppendLine("                    }");
		sb.AppendLine("                }");
		sb.AppendLine("                else");
		sb.AppendLine("                {");
		sb.AppendLine("                    switch (propHash)");
		sb.AppendLine("                    {");
		foreach (LuminDataField field3 in data.fields)
		{
			sb.AppendLine("                        case _utf16Hash_" + field3.Name + ":");
			sb.AppendLine("                        {");
			GenerateJsonDeserializeField(sb, field3, flag ? ("result." + field3.Identifier) : (field3.Name + "Temp"), "                            ");
			sb.AppendLine("                            break;");
			sb.AppendLine("                        }");
		}
		sb.AppendLine("                        default:");
		sb.AppendLine("                        {");
		sb.AppendLine("                            reader.Skip();");
		sb.AppendLine("                            break;");
		sb.AppendLine("                        }");
		sb.AppendLine("                    }");
		sb.AppendLine("                }");
		sb.AppendLine("            }");
		sb.AppendLine();
		if (flag)
		{
			sb.AppendLine("            value = result;");
		}
		else
		{
			GenerateObjectConstruction(sb, data, classGlobalName);
		}
		sb.AppendLine("        }");
	}

	private static bool CanDeserializeDirectlyIntoFreshResult(LuminDataInfo data)
	{
		//IL_015a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0160: Invalid comparison between Unknown and I4
		//IL_0060: Unknown result type (might be due to invalid IL or missing references)
		//IL_0066: Invalid comparison between Unknown and I4
		if (data.isValueType || data.fields.Count == 0 || data.RentPoolMethod != null || data.TypeSymbol == null || data.fields.Any((LuminDataField field) => field.IsPrivate || field.isProperty))
		{
			return false;
		}
		INamedTypeSymbol val = data.TypeSymbol;
		while (val != null && (int)((ITypeSymbol)val).SpecialType != 1)
		{
			if ((int)((ITypeSymbol)val).TypeKind != 2 || ((ISymbol)val).DeclaringSyntaxReferences.Length == 0)
			{
				return false;
			}
			ImmutableArray<IMethodSymbol> instanceConstructors = val.InstanceConstructors;
			if (instanceConstructors.Length != 1 || !((ISymbol)instanceConstructors[0]).IsImplicitlyDeclared || instanceConstructors[0].Parameters.Length != 0)
			{
				return false;
			}
			ImmutableArray<ISymbol>.Enumerator enumerator = ((INamespaceOrTypeSymbol)val).GetMembers().GetEnumerator();
			while (enumerator.MoveNext())
			{
				ISymbol current = enumerator.Current;
				if (current.IsStatic || current.IsImplicitlyDeclared)
				{
					continue;
				}
				ImmutableArray<SyntaxReference>.Enumerator enumerator2 = current.DeclaringSyntaxReferences.GetEnumerator();
				while (enumerator2.MoveNext())
				{
					SyntaxNode syntax = enumerator2.Current.GetSyntax(default(CancellationToken));
					VariableDeclaratorSyntax val2 = (VariableDeclaratorSyntax)(object)((syntax is VariableDeclaratorSyntax) ? syntax : null);
					if (val2 == null || val2.Initializer == null)
					{
						PropertyDeclarationSyntax val3 = (PropertyDeclarationSyntax)(object)((syntax is PropertyDeclarationSyntax) ? syntax : null);
						if (val3 == null || val3.Initializer == null)
						{
							continue;
						}
					}
					return false;
				}
			}
			val = ((ITypeSymbol)val).BaseType;
		}
		return true;
	}

	private static void GenerateJsonDeserializeField(StringBuilder sb, LuminDataField field, string targetVar, string indent)
	{
		if (field.FieldType == LuminDataType.Reference)
		{
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    " + targetVar + " = null;");
			sb.AppendLine(indent + "}");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "{");
			GenerateJsonDeserializeFieldCore(sb, field, targetVar, indent + "    ");
			sb.AppendLine(indent + "}");
		}
		else
		{
			GenerateJsonDeserializeFieldCore(sb, field, targetVar, indent);
		}
	}

	private static void GenerateJsonDeserializeFieldCore(StringBuilder sb, LuminDataField field, string targetVar, string indent)
	{
		switch (field.Type)
		{
		case LuminFiledType.Int:
			sb.AppendLine(indent + targetVar + " = reader.ReadInt();");
			break;
		case LuminFiledType.UInt:
			sb.AppendLine(indent + targetVar + " = reader.ReadUInt();");
			break;
		case LuminFiledType.Byte:
			sb.AppendLine(indent + targetVar + " = reader.ReadByte();");
			break;
		case LuminFiledType.SByte:
			sb.AppendLine(indent + targetVar + " = reader.ReadSByte();");
			break;
		case LuminFiledType.Short:
			sb.AppendLine(indent + targetVar + " = reader.ReadShort();");
			break;
		case LuminFiledType.UShort:
			sb.AppendLine(indent + targetVar + " = reader.ReadUShort();");
			break;
		case LuminFiledType.Long:
			sb.AppendLine(indent + targetVar + " = reader.ReadLong();");
			break;
		case LuminFiledType.ULong:
			sb.AppendLine(indent + targetVar + " = reader.ReadULong();");
			break;
		case LuminFiledType.Float:
			sb.AppendLine(indent + targetVar + " = reader.ReadFloat();");
			break;
		case LuminFiledType.Double:
			sb.AppendLine(indent + targetVar + " = reader.ReadDouble();");
			break;
		case LuminFiledType.Decimal:
			sb.AppendLine(indent + targetVar + " = reader.ReadDecimal();");
			break;
		case LuminFiledType.Char:
			sb.AppendLine(indent + targetVar + " = reader.ReadChar();");
			break;
		case LuminFiledType.String:
			sb.AppendLine(indent + targetVar + " = reader.ReadString();");
			break;
		case LuminFiledType.Bool:
			sb.AppendLine(indent + targetVar + " = reader.GetBoolean();");
			break;
		case LuminFiledType.Enum:
		{
			string text2 = field.FullTypeName ?? field.Name;
			sb.AppendLine(indent + targetVar + " = (" + text2 + ")reader.ReadInt();");
			break;
		}
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
		case LuminFiledType.List:
		case LuminFiledType.Array:
		case LuminFiledType.Other:
		{
			string text = field.FullTypeName ?? field.Name;
			sb.AppendLine(indent + "global::LuminPack.LuminPackParseProvider.Cache<" + text + ">.Parser?.DeserializeJson(ref reader, ref " + targetVar + ");");
			break;
		}
		default:
			sb.AppendLine(indent + "reader.Skip();");
			break;
		}
	}

	private static void GenerateObjectConstruction(StringBuilder sb, LuminDataInfo data, string classGlobalName)
	{
		List<string> list = new List<string>();
		if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
		{
			foreach (ConstructorParameter param in data.SelectedConstructor.Parameters)
			{
				LuminDataField luminDataField = data.fields.FirstOrDefault((LuminDataField f) => f.Name == param.MatchingFieldName);
				if (luminDataField != null)
				{
					list.Add(luminDataField.Name + "Temp!");
				}
				else
				{
					list.Add("default");
				}
			}
		}
		string text = string.Join(", ", list);
		List<LuminDataField> list2 = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && !f.IsPrivate && !f.isProperty).ToList();
		List<LuminDataField> list3 = data.fields.Where((LuminDataField f) => (data.SelectedConstructor == null || !data.SelectedConstructor.Parameters.Any((ConstructorParameter p) => p.MatchingFieldName == f.Name)) && (f.IsPrivate || f.isProperty)).ToList();
		if (data.RentPoolMethod != null)
		{
			if (data.RentPoolMethod.ReturnsByRef)
			{
				sb.AppendLine("            value = ref " + classGlobalName + "." + ((ISymbol)data.RentPoolMethod).Name + "()!;");
			}
			else
			{
				sb.AppendLine("            value = " + classGlobalName + "." + ((ISymbol)data.RentPoolMethod).Name + "();");
			}
			sb.AppendLine("            // 设置所有字段（对象池分配）");
			if (data.isValueType)
			{
				sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
			}
			else
			{
				sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
			}
			{
				foreach (LuminDataField field in data.fields)
				{
					sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
				}
				return;
			}
		}
		if (list2.Count > 0)
		{
			if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
			{
				sb.AppendLine("            value = new " + classGlobalName + "(" + text + ")");
			}
			else
			{
				sb.AppendLine("            value = new " + classGlobalName + "()");
			}
			sb.AppendLine("            {");
			foreach (LuminDataField item in list2)
			{
				sb.AppendLine("                " + item.Identifier + " = " + item.Name + "Temp!,");
			}
			sb.AppendLine("            };");
		}
		else if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
		{
			sb.AppendLine("            value = new " + classGlobalName + "(" + text + ");");
		}
		else
		{
			sb.AppendLine("            value = new " + classGlobalName + "();");
		}
		if (list3.Count <= 0)
		{
			return;
		}
		sb.AppendLine("            // 设置 private 字段");
		if (data.isValueType)
		{
			sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
		}
		else
		{
			sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
		}
		foreach (LuminDataField item2 in list3)
		{
			sb.AppendLine("            local." + item2.Identifier + " = " + item2.Name + "Temp!;");
		}
	}
}

public static class LuminPackJsonCircleReferenceCodeGenerator
{
	private const string JSON_ID_FIELD = "$id";

	private const string JSON_REF_FIELD = "$ref";

	public static void GenerateStaticUtf8FieldsForCircleReference(StringBuilder sb)
	{
		byte[] bytes = Encoding.UTF8.GetBytes("$id");
		string text = string.Join(", ", bytes.Select((byte b) => $"(byte){b}"));
		sb.AppendLine("        private static readonly byte[] _utf8_id = new byte[] { " + text + " };");
		string text2 = string.Join(", ", "$id".Select((char c) => "'" + EscapeChar(c) + "'"));
		sb.AppendLine("        private static readonly char[] _utf16_id = new char[] { " + text2 + " };");
		ulong num = LuminPackJsonCodeGenerator.ComputeUtf8Hash("$id");
		sb.AppendLine($"        private const ulong _utf8Hash_id = {num}UL;");
		ulong num2 = LuminPackJsonCodeGenerator.ComputeUtf16Hash("$id");
		sb.AppendLine($"        private const ulong _utf16Hash_id = {num2}UL;");
		sb.AppendLine();
		byte[] bytes2 = Encoding.UTF8.GetBytes("$ref");
		string text3 = string.Join(", ", bytes2.Select((byte b) => $"(byte){b}"));
		sb.AppendLine("        private static readonly byte[] _utf8_ref = new byte[] { " + text3 + " };");
		string text4 = string.Join(", ", "$ref".Select((char c) => "'" + EscapeChar(c) + "'"));
		sb.AppendLine("        private static readonly char[] _utf16_ref = new char[] { " + text4 + " };");
		ulong num3 = LuminPackJsonCodeGenerator.ComputeUtf8Hash("$ref");
		sb.AppendLine($"        private const ulong _utf8Hash_ref = {num3}UL;");
		ulong num4 = LuminPackJsonCodeGenerator.ComputeUtf16Hash("$ref");
		sb.AppendLine($"        private const ulong _utf16Hash_ref = {num4}UL;");
	}

	private static string EscapeChar(char c)
	{
		return c switch
		{
			'\'' => "\\'",
			'\\' => "\\\\",
			'\n' => "\\n",
			'\r' => "\\r",
			'\t' => "\\t",
			_ => c.ToString(),
		};
	}

	public static void GenerateJsonSerializeWithCircleReference(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
		if (data.isValueType)
		{
			sb.AppendLine(metaInfo.IsNet8 ? ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + classGlobalName + " value)") : ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + classGlobalName + " value)"));
		}
		else
		{
			sb.AppendLine(metaInfo.IsNet8 ? ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref " + classGlobalName + "? value)") : ("        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref " + classGlobalName + "? value)"));
		}
		sb.AppendLine("        {");
		if (!data.isValueType)
		{
			sb.AppendLine("            if (value == null)");
			sb.AppendLine("            {");
			sb.AppendLine("                writer.WriteNull();");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
			sb.AppendLine();
			sb.AppendLine("            bool isUtf8 = writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8;");
			sb.AppendLine();
			sb.AppendLine("            var (existsReference, id) = writer.OptionState.GetOrAddReference(value);");
			sb.AppendLine("            if (existsReference)");
			sb.AppendLine("            {");
			sb.AppendLine("                writer.WriteObjectStart();");
			sb.AppendLine("                if (isUtf8)");
			sb.AppendLine("                    writer.WritePropertyName(_utf8_ref);");
			sb.AppendLine("                else");
			sb.AppendLine("                    writer.WritePropertyName(_utf16_ref);");
			sb.AppendLine("                writer.WriteUInt(id);");
			sb.AppendLine("                writer.WriteObjectEnd();");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
			sb.AppendLine();
		}
		else
		{
			sb.AppendLine();
			sb.AppendLine("            bool isUtf8 = writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8;");
			sb.AppendLine();
		}
		if (data.fields.Count((LuminDataField x) => x.IsPrivate || x.isProperty) > 0)
		{
			if (data.isValueType)
			{
				sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
			}
			else
			{
				sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
			}
			sb.AppendLine();
		}
		sb.AppendLine("            writer.WriteObjectStart();");
		if (!data.isValueType)
		{
			sb.AppendLine();
			sb.AppendLine("            // 写入对象ID");
			sb.AppendLine("            if (isUtf8)");
			sb.AppendLine("                writer.WritePropertyName(_utf8_id);");
			sb.AppendLine("            else");
			sb.AppendLine("                writer.WritePropertyName(_utf16_id);");
			sb.AppendLine("             writer.WriteUInt(id);");
		}
		sb.AppendLine();
		foreach (LuminDataField field in data.fields)
		{
			string accessor = ((field.IsPrivate || field.isProperty) ? "local" : "value");
			sb.AppendLine("            // 字段: " + field.Name);
			sb.AppendLine("            if (isUtf8)");
			sb.AppendLine("                writer.WritePropertyName(_utf8_" + field.Name + ");");
			sb.AppendLine("            else");
			sb.AppendLine("                writer.WritePropertyName(_utf16_" + field.Name + ");");
			GenerateJsonSerializeFieldWithCircleReference(sb, field, accessor, "            ");
			sb.AppendLine();
		}
		sb.AppendLine("            writer.WriteObjectEnd();");
		sb.AppendLine("        }");
	}

	private static void GenerateJsonSerializeFieldWithCircleReference(StringBuilder sb, LuminDataField field, string accessor, string indent)
	{
		bool flag = field.FieldType == LuminDataType.Reference;
		string text = accessor + "." + field.Identifier;
		if (flag)
		{
			sb.AppendLine(indent + "if (" + text + " == null)");
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    writer.WriteNull();");
			sb.AppendLine(indent + "}");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "{");
			indent += "    ";
		}
		switch (field.Type)
		{
		case LuminFiledType.Int:
			sb.AppendLine(indent + "writer.WriteInt(" + text + ");");
			break;
		case LuminFiledType.UInt:
			sb.AppendLine(indent + "writer.WriteUInt(" + text + ");");
			break;
		case LuminFiledType.Byte:
			sb.AppendLine(indent + "writer.WriteByte(" + text + ");");
			break;
		case LuminFiledType.SByte:
			sb.AppendLine(indent + "writer.WriteSByte(" + text + ");");
			break;
		case LuminFiledType.Short:
			sb.AppendLine(indent + "writer.WriteShort(" + text + ");");
			break;
		case LuminFiledType.UShort:
			sb.AppendLine(indent + "writer.WriteUShort(" + text + ");");
			break;
		case LuminFiledType.Long:
			sb.AppendLine(indent + "writer.WriteLong(" + text + ");");
			break;
		case LuminFiledType.ULong:
			sb.AppendLine(indent + "writer.WriteULong(" + text + ");");
			break;
		case LuminFiledType.Float:
			sb.AppendLine(indent + "writer.WriteFloat(" + text + ");");
			break;
		case LuminFiledType.Double:
			sb.AppendLine(indent + "writer.WriteDouble(" + text + ");");
			break;
		case LuminFiledType.Decimal:
			sb.AppendLine(indent + "writer.WriteDecimal(" + text + ");");
			break;
		case LuminFiledType.Char:
			sb.AppendLine(indent + "writer.WriteChar(" + text + ");");
			break;
		case LuminFiledType.String:
			sb.AppendLine(indent + "writer.WriteString(" + text + ");");
			break;
		case LuminFiledType.Bool:
			sb.AppendLine(indent + "writer.WriteBool(" + text + ");");
			break;
		case LuminFiledType.Enum:
			sb.AppendLine(indent + "writer.WriteInt((int)" + text + ");");
			break;
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
		case LuminFiledType.List:
		case LuminFiledType.Array:
		case LuminFiledType.Other:
		{
			string text2 = field.FullTypeName ?? field.Name;
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    var temp = " + text + ";");
			sb.AppendLine(indent + "    global::LuminPack.LuminPackParseProvider.Cache<" + text2 + ">.Parser!.SerializeJson(ref writer, ref temp);");
			sb.AppendLine(indent + "}");
			break;
		}
		default:
			sb.AppendLine(indent + "writer.WriteNull();");
			break;
		}
		if (flag)
		{
			sb.AppendLine(indent.Substring(0, indent.Length - 4) + "}");
		}
	}

	public static void GenerateJsonDeserializeWithCircleReference(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
	{
		sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
		string text = (data.isValueType ? "" : "?");
		string text2 = (metaInfo.IsNet8 ? "scoped " : "");
		sb.AppendLine("        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, " + text2 + "ref " + classGlobalName + text + " value)");
		sb.AppendLine("        {");
		if (!data.isValueType)
		{
			sb.AppendLine("            if (reader.IsNull())");
			sb.AppendLine("            {");
			sb.AppendLine("                value = null;");
			sb.AppendLine("                return;");
			sb.AppendLine("            }");
			sb.AppendLine();
			sb.AppendLine("            uint? objectId = null;");
			sb.AppendLine("            uint? refId = null;");
			sb.AppendLine();
			sb.AppendLine("            " + classGlobalName + " preCreatedInstance;");
			if (data.RentPoolMethod != null)
			{
				if (data.RentPoolMethod.ReturnsByRef)
				{
					sb.AppendLine("            preCreatedInstance = ref " + classGlobalName + "." + ((ISymbol)data.RentPoolMethod).Name + "()!;");
				}
				else
				{
					sb.AppendLine("            preCreatedInstance = " + classGlobalName + "." + ((ISymbol)data.RentPoolMethod).Name + "();");
				}
			}
			else
			{
				List<string> list = new List<string>();
				if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
				{
					foreach (ConstructorParameter parameter in data.SelectedConstructor.Parameters)
					{
						_ = parameter;
						list.Add("default!");
					}
				}
				string text3 = string.Join(", ", list);
				if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
				{
					sb.AppendLine("            preCreatedInstance = new " + classGlobalName + "(" + text3 + ");");
				}
				else
				{
					sb.AppendLine("            preCreatedInstance = new " + classGlobalName + "();");
				}
			}
			sb.AppendLine("            value = preCreatedInstance;");
			sb.AppendLine();
		}
		else
		{
			sb.AppendLine("            value = default;");
		}
		foreach (LuminLocalFieldData localField in data.localFields)
		{
			string text4 = (localField.IsValue ? "" : "?");
			sb.AppendLine("            " + localField.TypeName + text4 + " " + localField.Name + "Temp = default!;");
		}
		sb.AppendLine();
		sb.AppendLine("            reader.TryConsumeObjectStart();");
		sb.AppendLine();
		sb.AppendLine("            bool isUtf8 = reader.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8;");
		sb.AppendLine();
		sb.AppendLine("            while (reader.Read())");
		sb.AppendLine("            {");
		sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
		sb.AppendLine("                    break;");
		sb.AppendLine();
		sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
		sb.AppendLine("                    continue;");
		sb.AppendLine();
		sb.AppendLine("                ulong propHash;");
		sb.AppendLine("                if (isUtf8)");
		sb.AppendLine("                {");
		sb.AppendLine("                    var propNameUtf8 = reader.ReadStringUtf8();");
		sb.AppendLine("                    propHash = global::LuminPack.Internal.XxHash3.Hash64Utf8(propNameUtf8);");
		sb.AppendLine("                }");
		sb.AppendLine("                else");
		sb.AppendLine("                {");
		sb.AppendLine("                    var propNameUtf16 = reader.ReadStringUtf16();");
		sb.AppendLine("                    propHash = global::LuminPack.Internal.XxHash3.Hash64Utf16(propNameUtf16);");
		sb.AppendLine("                }");
		sb.AppendLine();
		sb.AppendLine("                if (!reader.Read())");
		sb.AppendLine("                    break;");
		sb.AppendLine();
		if (!data.isValueType)
		{
			sb.AppendLine("                if (propHash == _utf8Hash_id || propHash == _utf16Hash_id)");
			sb.AppendLine("                {");
			sb.AppendLine("                    var id = reader.ReadUInt();");
			sb.AppendLine("                    objectId = id;");
			sb.AppendLine("                    reader.OptionState.AddObjectReference(id, preCreatedInstance);");
			sb.AppendLine("                    continue;");
			sb.AppendLine("                }");
			sb.AppendLine();
			sb.AppendLine("                if (propHash == _utf8Hash_ref || propHash == _utf16Hash_ref)");
			sb.AppendLine("                {");
			sb.AppendLine("                    var rId = reader.ReadUInt();");
			sb.AppendLine("                    refId = rId;");
			sb.AppendLine("                    value = (" + classGlobalName + ")reader.OptionState.GetObjectReference(rId);");
			sb.AppendLine("                    while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) {}");
			sb.AppendLine("                    return;");
			sb.AppendLine("                }");
			sb.AppendLine();
		}
		sb.AppendLine("                switch (propHash)");
		sb.AppendLine("                {");
		foreach (LuminDataField field in data.fields)
		{
			sb.AppendLine("                    case _utf8Hash_" + field.Name + ":");
			sb.AppendLine("                    case _utf16Hash_" + field.Name + ":");
			sb.AppendLine("                    {");
			GenerateJsonDeserializeFieldWithCircleReference(sb, field, field.Name + "Temp", "                        ");
			sb.AppendLine("                        break;");
			sb.AppendLine("                    }");
		}
		sb.AppendLine("                    default:");
		sb.AppendLine("                    {");
		sb.AppendLine("                        reader.Skip();");
		sb.AppendLine("                        break;");
		sb.AppendLine("                    }");
		sb.AppendLine("                }");
		sb.AppendLine("            }");
		sb.AppendLine();
		List<LuminDataField> list2 = data.fields.Where((LuminDataField f) => !f.IsPrivate && !f.isProperty).ToList();
		List<LuminDataField> list3 = data.fields.Where((LuminDataField f) => f.IsPrivate || f.isProperty).ToList();
		if (data.RentPoolMethod != null)
		{
			sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref " + (data.isValueType ? "value" : "value!") + ");");
			foreach (LuminDataField field2 in data.fields)
			{
				sb.AppendLine("            local." + field2.Identifier + " = " + field2.Name + "Temp!;");
			}
		}
		else
		{
			if (list2.Count > 0)
			{
				foreach (LuminDataField item in list2)
				{
					sb.AppendLine("            value." + item.Identifier + " = " + item.Name + "Temp!;");
				}
				sb.AppendLine();
			}
			if (list3.Count > 0)
			{
				sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref " + (data.isValueType ? "value" : "value!") + ");");
				foreach (LuminDataField item2 in list3)
				{
					sb.AppendLine("            local." + item2.Identifier + " = " + item2.Name + "Temp!;");
				}
			}
		}
		sb.AppendLine("        }");
	}

	private static void GenerateJsonDeserializeFieldWithCircleReference(StringBuilder sb, LuminDataField field, string targetVar, string indent)
	{
		switch (field.Type)
		{
		case LuminFiledType.Int:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadInt();");
			break;
		case LuminFiledType.UInt:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadUInt();");
			break;
		case LuminFiledType.Byte:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadByte();");
			break;
		case LuminFiledType.SByte:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadSByte();");
			break;
		case LuminFiledType.Short:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadShort();");
			break;
		case LuminFiledType.UShort:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadUShort();");
			break;
		case LuminFiledType.Long:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadLong();");
			break;
		case LuminFiledType.ULong:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadULong();");
			break;
		case LuminFiledType.Float:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadFloat();");
			break;
		case LuminFiledType.Double:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadDouble();");
			break;
		case LuminFiledType.Decimal:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadDecimal();");
			break;
		case LuminFiledType.Char:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadChar();");
			break;
		case LuminFiledType.String:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    " + targetVar + " = null;");
			sb.AppendLine(indent + "}");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    " + targetVar + " = reader.ReadString();");
			sb.AppendLine(indent + "}");
			break;
		case LuminFiledType.Bool:
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = reader.GetBoolean();");
			break;
		case LuminFiledType.Enum:
		{
			string text2 = field.FullTypeName ?? field.Name;
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "    " + targetVar + " = default;");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "    " + targetVar + " = (" + text2 + ")reader.ReadInt();");
			break;
		}
		case LuminFiledType.Struct:
		case LuminFiledType.Class:
		case LuminFiledType.List:
		case LuminFiledType.Array:
		case LuminFiledType.Other:
		{
			string text = field.FullTypeName ?? field.Name;
			sb.AppendLine(indent + "if (reader.IsNull())");
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    " + targetVar + " = null;");
			sb.AppendLine(indent + "}");
			sb.AppendLine(indent + "else");
			sb.AppendLine(indent + "{");
			sb.AppendLine(indent + "    global::LuminPack.LuminPackParseProvider.Cache<" + text + ">.Parser!.DeserializeJson(ref reader, ref " + targetVar + ");");
			sb.AppendLine(indent + "}");
			break;
		}
		default:
			sb.AppendLine(indent + "reader.Skip();");
			break;
		}
	}

	private static string GetParserClassName(string typeFullName)
	{
		return typeFullName.Replace("global::", "").Replace(".", "_").Replace("<", "_")
			.Replace(">", "")
			.Replace(",", "")
			.Replace(" ", "") + "Parser";
	}
}
