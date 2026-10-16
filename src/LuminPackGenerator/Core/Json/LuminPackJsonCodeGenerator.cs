using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using LuminPack.SourceGenerator;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Provides lumin pack json code generator functionality.</summary>
public static class LuminPackJsonCodeGenerator
{
    /// <summary>Performs the compute utf8 hash step for the current operation.</summary>
    /// <param name="str">The str used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal unsafe static ulong ComputeUtf8Hash(string str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        fixed (byte* input = bytes)
        {
            return ComputeXxHash3(input, bytes.Length);
        }
    }

    /// <summary>Performs the compute utf16 hash step for the current operation.</summary>
    /// <param name="str">The str used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    internal unsafe static ulong ComputeUtf16Hash(string str)
    {
        fixed (char* ptr = str)
        {
            byte* input = (byte*)ptr;
            return ComputeXxHash3(input, str.Length * 2);
        }
    }

    /// <summary>Performs the compute xx hash3 step for the current operation.</summary>
    /// <param name="input">The input data to process.</param>
    /// <param name="length">The number of bytes or elements to process.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Builds the static utf8 fields source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
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
            sb.AppendLine("        internal static readonly byte[] " + text + " = new byte[] { " + text3 + " };");
            string text4 = string.Join(", ", field.Name.Select((char c) => "'" + EscapeChar(c) + "'"));
            sb.AppendLine("        internal static readonly char[] " + text2 + " = new char[] { " + text4 + " };");
            ulong num = ComputeUtf8Hash(field.Name);
            sb.AppendLine($"        internal const ulong {arg} = {num}UL;");
            ulong num2 = ComputeUtf16Hash(field.Name);
            sb.AppendLine($"        internal const ulong {arg2} = {num2}UL;");
        }
        GenerateJsonPropertyLookup(sb, data, true);
        GenerateJsonPropertyLookup(sb, data, false);
        GenerateOrderedJsonPropertyLookup(sb, data);
    }

    /// <summary>Builds the ordered json property lookup source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    private static void GenerateOrderedJsonPropertyLookup(StringBuilder sb, LuminDataInfo data)
    {
        sb.AppendLine("        internal static bool TryReadOrderedJsonProperty(ref global::LuminPack.Core.LuminPackJsonReader reader, int index)");
        sb.AppendLine("        {");
        sb.AppendLine("            switch (index)");
        sb.AppendLine("            {");
        for (int i = 0; i < data.fields.Count; i++)
        {
            string name = data.fields[i].Name;
            sb.AppendLine("                case " + i + ": return reader.TryReadPropertyName(_utf8_" + name + ", _utf16_" + name + ");");
        }
        sb.AppendLine("                default: return false;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
    }

    /// <summary>Builds the json property lookup source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="utf8">The utf8 used by this operation.</param>
    private static void GenerateJsonPropertyLookup(StringBuilder sb, LuminDataInfo data, bool utf8)
    {
        string encoding = utf8 ? "Utf8" : "Utf16";
        string element = utf8 ? "byte" : "char";
        string prefix = utf8 ? "_utf8_" : "_utf16_";
        var names = data.fields.Select((field, index) => new { field.Name, Index = index }).ToList();
        if (data.generatorType == GeneratorType.CircleReference && !data.isValueType)
        {
            names.Add(new { Name = "$id", Index = -2 });
            names.Add(new { Name = "$ref", Index = -3 });
        }
        sb.AppendLine("        internal static int GetJsonPropertyIndex" + encoding + "(global::System.ReadOnlySpan<" + element + "> name)");
        sb.AppendLine("        {");
        bool small = data.fields.Count <= 8;
        sb.AppendLine(small
            ? "            switch (name.Length)"
            : "            switch (global::LuminPack.Internal.XxHash3.Hash64" + encoding + "(name))");
        sb.AppendLine("            {");
        // Group equal keys so even a compile-time hash collision produces valid C#.
        var groups = names.GroupBy(item => small
            ? (ulong)(utf8 ? Encoding.UTF8.GetByteCount(item.Name) : item.Name.Length)
            : (utf8 ? ComputeUtf8Hash(item.Name) : ComputeUtf16Hash(item.Name)));
        foreach (var group in groups)
        {
            sb.AppendLine("                case " + group.Key + (small ? ":" : "UL:"));
            foreach (var item in group)
            {
                string fieldName = item.Index == -2 ? "id" : item.Index == -3 ? "ref" : item.Name;
                sb.AppendLine("                    if (global::System.MemoryExtensions.SequenceEqual<" + element + ">(name, " + prefix + fieldName + ")) return " + item.Index + ";");
            }
            sb.AppendLine("                    break;");
        }
        sb.AppendLine("            }");
        sb.AppendLine("            return -1;");
        sb.AppendLine("        }");
    }

    /// <summary>Performs the escape char step for the current operation.</summary>
    /// <param name="c">The c used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Builds the json methods source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="classGlobalName">The class global name used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    public static void GenerateJsonMethods(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
    {
        GenerateJsonSerialize(sb, data, classGlobalName, metaInfo);
        sb.AppendLine();
        GenerateJsonDeserialize(sb, data, classGlobalName, metaInfo);
    }

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="classGlobalName">The class global name used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    public static void GenerateJsonSerialize(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
    {
        for (int i = 0; i < data.fields.Count; i++)
        {
            LuminDataField luminDataField = data.fields[i];
            if (metaInfo.IsNet8 && metaInfo.CSharpVersion > Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp10)
            {
                string marker = i == 0 ? "Prefix" : "Sep";
                string literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(
                    (i == 0 ? "{" : ",") + "\"" + luminDataField.Name + "\":", true);
                sb.AppendLine("        internal static global::System.ReadOnlySpan<byte> _json" + marker + "Utf8_" + luminDataField.Name + " { [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] get => " + literal + "u8; }");
                sb.AppendLine("        internal static global::System.ReadOnlySpan<char> _json" + marker + "Utf16_" + luminDataField.Name + " { [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)] get => global::System.MemoryExtensions.AsSpan(" + literal + "); }");
                continue;
            }
            string text = string.Join(", ", from b in Encoding.UTF8.GetBytes(luminDataField.Name)
                                            select $"(byte){b}");
            string text2 = string.Join(", ", luminDataField.Name.Select((char c) => "(char)'" + EscapeChar(c) + "'"));
            if (i == 0)
            {
                sb.AppendLine("        internal static readonly byte[] _jsonPrefixUtf8_" + luminDataField.Name + " = new byte[] { (byte)'{', (byte)'\"', " + text + ", (byte)'\"', (byte)':' };");
                sb.AppendLine("        internal static readonly char[] _jsonPrefixUtf16_" + luminDataField.Name + " = new char[] { (char)'{', (char)'\"', " + text2 + ", (char)'\"', (char)':' };");
            }
            else
            {
                sb.AppendLine("        internal static readonly byte[] _jsonSepUtf8_" + luminDataField.Name + " = new byte[] { (byte)',', (byte)'\"', " + text + ", (byte)'\"', (byte)':' };");
                sb.AppendLine("        internal static readonly char[] _jsonSepUtf16_" + luminDataField.Name + " = new char[] { (char)',', (char)'\"', " + text2 + ", (char)'\"', (char)':' };");
            }
        }
        sb.AppendLine();
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        if (data.isValueType)
        {
            sb.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped in " + classGlobalName + " input)") : ("        public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, in " + classGlobalName + " input)"));
        }
        else
        {
            sb.AppendLine(metaInfo.IsNet8 ? ("        public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped in " + classGlobalName + "? input)") : ("        public static void WriteValue(ref global::LuminPack.Core.LuminPackJsonWriter writer, in " + classGlobalName + "? input)"));
        }
        sb.AppendLine("        {");
        sb.AppendLine("            var value = input;");
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
            sb.AppendLine("            // Field: " + luminDataField2.Name);
            if (TryGenerateJsonIntegerProperty(sb, luminDataField2, text4, text3 + "." + luminDataField2.Identifier, metaInfo))
            {
                sb.AppendLine();
                continue;
            }
            sb.AppendLine("            if (isUtf8)");
            GenerateJsonLiteralWrite(sb, luminDataField2.Name, text4, utf8: true, metaInfo);
            sb.AppendLine("            else");
            GenerateJsonLiteralWrite(sb, luminDataField2.Name, text4, utf8: false, metaInfo);
            sb.AppendLine("            writer.SetFirstElement(true);");
            GenerateJsonSerializeField(sb, luminDataField2, text3 + "." + luminDataField2.Identifier, "            ");
            sb.AppendLine();
        }
        // WriteObjectEnd preserves the selected JSON encoding. A raw byte leaves an
        // incomplete UTF-16 token and makes the top-level end check fail.
        sb.AppendLine("            writer.WriteObjectEnd();");
        sb.AppendLine("        }");
    }

    /// <summary>Attempts to generate json integer property and reports whether it succeeded.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="marker">The marker used by this operation.</param>
    /// <param name="valueExpr">The value expr used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool TryGenerateJsonIntegerProperty(StringBuilder sb, LuminDataField field,
        string marker, string valueExpr, MetaInfo metaInfo)
    {
        if (!metaInfo.IsNet8 || field.IsPrivate || field.isProperty ||
            field.TypeSymbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } ||
            field.Type is not (LuminFiledType.Int or LuminFiledType.UInt or LuminFiledType.Long or LuminFiledType.ULong))
            return false;
        byte[] bytes = Encoding.UTF8.GetBytes((marker == "Prefix" ? "{" : ",") + "\"" + field.Name + "\":");
        if (bytes.Length < 8 || bytes.Length > 16)
            return false;
        ulong first = 0, last = 0;
        for (int i = 0; i < 8; i++)
        {
            first |= (ulong)bytes[i] << (i * 8);
            last |= (ulong)bytes[bytes.Length - 8 + i] << (i * 8);
        }
        sb.AppendLine("            if (isUtf8 && global::System.BitConverter.IsLittleEndian)");
        sb.AppendLine($"                writer.WriteGeneratedInteger(0x{first:X16}UL, 0x{last:X16}UL, {bytes.Length}, {valueExpr});");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                if (isUtf8)");
        GenerateJsonLiteralWrite(sb, field.Name, marker, utf8: true, metaInfo);
        sb.AppendLine("                else");
        GenerateJsonLiteralWrite(sb, field.Name, marker, utf8: false, metaInfo);
        sb.AppendLine("                writer.SetFirstElement(true);");
        GenerateJsonSerializeField(sb, field, valueExpr, "                ");
        sb.AppendLine("            }");
        return true;
    }

    /// <summary>Builds the json literal write source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="name">The name used by this operation.</param>
    /// <param name="marker">The marker used by this operation.</param>
    /// <param name="utf8">The utf8 used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    private static void GenerateJsonLiteralWrite(StringBuilder sb, string name, string marker, bool utf8, MetaInfo metaInfo)
    {
        string field = "_json" + marker + (utf8 ? "Utf8_" : "Utf16_") + name;
        string literal = (marker == "Prefix" ? "{" : ",") + "\"" + name + "\":";
        byte[] bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(literal);
        if (metaInfo.IsNet8 && bytes.Length >= 8 && bytes.Length <= 16)
        {
            ulong first = 0, last = 0;
            for (int i = 0; i < 8; i++)
            {
                first |= (ulong)bytes[i] << (i * 8);
                last |= (ulong)bytes[bytes.Length - 8 + i] << (i * 8);
            }
            sb.AppendLine("            {");
            sb.AppendLine("                if (global::System.BitConverter.IsLittleEndian)");
            sb.AppendLine($"                    writer.WriteGeneratedLiteral(0x{first:X16}UL, 0x{last:X16}UL, {bytes.Length});");
            sb.AppendLine("                else");
            sb.AppendLine("                    writer.WriteRaw(" + field + ");");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("                writer.WriteRaw(" + field + ");");
        }
    }

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="valueExpr">The value expr used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void GenerateJsonSerializeField(StringBuilder sb, LuminDataField field, string valueExpr, string indent)
    {
        if (field.FieldType == LuminDataType.Reference && field.Type != LuminFiledType.Other)
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

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="valueExpr">The value expr used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
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
                {
                    var enumNames = EnumEmitter.GetJsonEnumTypeNames(field.TypeSymbol);
                    sb.AppendLine(indent + "writer." + enumNames.WriteMethod + "((" + enumNames.Keyword + ")" + valueExpr + ");");
                    break;
                }
            case LuminFiledType.Struct:
            case LuminFiledType.Class:
            case LuminFiledType.List:
            case LuminFiledType.Array:
            case LuminFiledType.Other:
                {
                    string text = field.FullTypeName ?? field.Name;
                    sb.AppendLine(indent + "{");
                    sb.AppendLine(indent + "    var temp = " + valueExpr + ";");
                    sb.AppendLine(indent + "    global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in temp);");
                    sb.AppendLine(indent + "}");
                    break;
                }
            default:
                sb.AppendLine(indent + "writer.WriteNull();");
                break;
        }
    }

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="classGlobalName">The class global name used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
    public static void GenerateJsonDeserialize(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
    {
        bool flag = CanDeserializeDirectlyIntoFreshResult(data);
        sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        if (data.isValueType)
        {
            sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + classGlobalName + " value)") : ("        public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + classGlobalName + " value)"));
        }
        else
        {
            sb.AppendLine(metaInfo.IsNet8 ? ("        public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref " + classGlobalName + "? value)") : ("        public static void ReadValue(ref global::LuminPack.Core.LuminPackJsonReader reader, ref " + classGlobalName + "? value)"));
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
        if (!flag || data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
        {
            sb.AppendLine("            // Temp vars");
            foreach (LuminDataField field in data.fields)
            {
                if (flag && CanDeserializeDirectlyIntoConstructedObject(field))
                {
                    continue;
                }
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
        sb.AppendLine("            int nextPropertyIndex = 0;");
        sb.AppendLine("            while (true)");
        sb.AppendLine("            {");
        sb.AppendLine("                int propertyIndex = nextPropertyIndex;");
        sb.AppendLine("                if (!TryReadOrderedJsonProperty(ref reader, propertyIndex))");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (!reader.Read() || reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine();
        sb.AppendLine("                    if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("                    {");
        sb.AppendLine("                        reader.Skip();");
        sb.AppendLine("                        continue;");
        sb.AppendLine("                    }");
        sb.AppendLine();
        sb.AppendLine("                    propertyIndex = isUtf8");
        sb.AppendLine("                        ? GetJsonPropertyIndexUtf8(reader.ReadStringUtf8())");
        sb.AppendLine("                        : GetJsonPropertyIndexUtf16(reader.ReadStringUtf16());");
        sb.AppendLine("                    if (!reader.Read()) break;");
        sb.AppendLine("                }");
        sb.AppendLine("                nextPropertyIndex = propertyIndex + 1;");
        sb.AppendLine("                switch (propertyIndex)");
        sb.AppendLine("                {");
        for (int fieldIndex = 0; fieldIndex < data.fields.Count; fieldIndex++)
        {
            var field = data.fields[fieldIndex];
            sb.AppendLine("                    case " + fieldIndex + ":");
            sb.AppendLine("                    {");
            GenerateJsonDeserializeField(sb, field, flag && CanDeserializeDirectlyIntoConstructedObject(field) ? ("result." + field.Identifier) : (field.Name + "Temp"), "                        ");
            sb.AppendLine("                        break;");
            sb.AppendLine("                    }");
        }
        sb.AppendLine("                    default:");
        sb.AppendLine("                        reader.Skip();");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (flag)
        {
            sb.AppendLine("            value = result;");
            if (data.fields.Any(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
            {
                if (data.isValueType)
                {
                    sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value);");
                }
                else
                {
                    sb.AppendLine("            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<" + classGlobalName + ", " + TypeMetaChecker.BuildLocalClassName(data) + ">(ref value!);");
                }
                foreach (LuminDataField field in data.fields.Where(static field => !CanDeserializeDirectlyIntoConstructedObject(field)))
                {
                    sb.AppendLine("            local." + field.Identifier + " = " + field.Name + "Temp!;");
                }
            }
        }
        else
        {
            GenerateObjectConstruction(sb, data, classGlobalName);
        }
        sb.AppendLine("        }");
    }

    /// <summary>Performs the can deserialize directly into fresh result step for the current operation.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool CanDeserializeDirectlyIntoFreshResult(LuminDataInfo data)
    {
        // JSON can omit members. Classes therefore retain the existing constructor and
        // initializer proof; otherwise assigning delayed private members would overwrite
        // values intentionally left by the constructor. Value types have no null/default
        // object state to preserve and can use the binary generator's direct path.
        if (data.isValueType)
        {
            return data.RentPoolMethod == null &&
                (data.SelectedConstructor == null || data.SelectedConstructor.Parameters.Count == 0);
        }

        if (data.fields.Count == 0 || data.RentPoolMethod != null || data.TypeSymbol == null || data.fields.Any(static field => field.IsPrivate || field.isProperty))
        {
            return false;
        }

        INamedTypeSymbol type = data.TypeSymbol;
        while (type != null && type.SpecialType != SpecialType.System_Object)
        {
            if (type.TypeKind != TypeKind.Class || type.DeclaringSyntaxReferences.Length == 0)
            {
                return false;
            }

            ImmutableArray<IMethodSymbol> constructors = type.InstanceConstructors;
            if (constructors.Length != 1 || !constructors[0].IsImplicitlyDeclared || constructors[0].Parameters.Length != 0)
            {
                return false;
            }

            foreach (ISymbol member in type.GetMembers())
            {
                if (member.IsStatic || member.IsImplicitlyDeclared)
                {
                    continue;
                }

                foreach (SyntaxReference syntaxReference in member.DeclaringSyntaxReferences)
                {
                    SyntaxNode syntax = syntaxReference.GetSyntax(default(CancellationToken));
                    if (syntax is VariableDeclaratorSyntax { Initializer: not null } ||
                        syntax is PropertyDeclarationSyntax { Initializer: not null })
                    {
                        return false;
                    }
                }
            }

            type = type.BaseType;
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

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="targetVar">The target var used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void GenerateJsonDeserializeField(StringBuilder sb, LuminDataField field, string targetVar, string indent)
    {
        if (field.FieldType == LuminDataType.Reference && field.Type != LuminFiledType.Other)
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

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="targetVar">The target var used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
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
                    var enumNames = EnumEmitter.GetJsonEnumTypeNames(field.TypeSymbol);
                    sb.AppendLine(indent + targetVar + " = (" + text2 + ")reader." + enumNames.ReadMethod + "();");
                    break;
                }
            case LuminFiledType.Struct:
            case LuminFiledType.Class:
            case LuminFiledType.List:
            case LuminFiledType.Array:
            case LuminFiledType.Other:
                {
                    string text = field.FullTypeName ?? field.Name;
                    sb.AppendLine(indent + "global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref " + targetVar + ");");
                    break;
                }
            default:
                sb.AppendLine(indent + "reader.Skip();");
                break;
        }
    }

    /// <summary>Builds the object construction source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="classGlobalName">The class global name used by this operation.</param>
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
            sb.AppendLine("            // Populate every field on the pooled instance.");
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
        sb.AppendLine("            // Assign private fields.");
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

/// <summary>Provides lumin pack json circle reference code generator functionality.</summary>
public static class LuminPackJsonCircleReferenceCodeGenerator
{
    private const string JSON_ID_FIELD = "$id";

    private const string JSON_REF_FIELD = "$ref";

    /// <summary>Builds the static utf8 fields for circle reference source from the supplied metadata.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateStaticUtf8FieldsForCircleReference(StringBuilder sb)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("$id");
        string text = string.Join(", ", bytes.Select((byte b) => $"(byte){b}"));
        sb.AppendLine("        internal static readonly byte[] _utf8_id = new byte[] { " + text + " };");
        string text2 = string.Join(", ", "$id".Select((char c) => "'" + EscapeChar(c) + "'"));
        sb.AppendLine("        internal static readonly char[] _utf16_id = new char[] { " + text2 + " };");
        ulong num = LuminPackJsonCodeGenerator.ComputeUtf8Hash("$id");
        sb.AppendLine($"        internal const ulong _utf8Hash_id = {num}UL;");
        ulong num2 = LuminPackJsonCodeGenerator.ComputeUtf16Hash("$id");
        sb.AppendLine($"        internal const ulong _utf16Hash_id = {num2}UL;");
        sb.AppendLine();
        byte[] bytes2 = Encoding.UTF8.GetBytes("$ref");
        string text3 = string.Join(", ", bytes2.Select((byte b) => $"(byte){b}"));
        sb.AppendLine("        internal static readonly byte[] _utf8_ref = new byte[] { " + text3 + " };");
        string text4 = string.Join(", ", "$ref".Select((char c) => "'" + EscapeChar(c) + "'"));
        sb.AppendLine("        internal static readonly char[] _utf16_ref = new char[] { " + text4 + " };");
        ulong num3 = LuminPackJsonCodeGenerator.ComputeUtf8Hash("$ref");
        sb.AppendLine($"        internal const ulong _utf8Hash_ref = {num3}UL;");
        ulong num4 = LuminPackJsonCodeGenerator.ComputeUtf16Hash("$ref");
        sb.AppendLine($"        internal const ulong _utf16Hash_ref = {num4}UL;");
    }

    /// <summary>Performs the escape char step for the current operation.</summary>
    /// <param name="c">The c used by this operation.</param>
    /// <returns>The result of the operation.</returns>
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

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="classGlobalName">The class global name used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
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
            sb.AppendLine("            // Write the object reference identifier.");
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
            sb.AppendLine("            // Field: " + field.Name);
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

    /// <summary>Appends JSON serialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="accessor">The accessor used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
    private static void GenerateJsonSerializeFieldWithCircleReference(StringBuilder sb, LuminDataField field, string accessor, string indent)
    {
        bool flag = field.FieldType == LuminDataType.Reference && field.Type != LuminFiledType.Other;
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
                {
                    var enumNames = EnumEmitter.GetJsonEnumTypeNames(field.TypeSymbol);
                    sb.AppendLine(indent + "writer." + enumNames.WriteMethod + "((" + enumNames.Keyword + ")" + text + ");");
                    break;
                }
            case LuminFiledType.Struct:
            case LuminFiledType.Class:
            case LuminFiledType.List:
            case LuminFiledType.Array:
            case LuminFiledType.Other:
                {
                    string text2 = field.FullTypeName ?? field.Name;
                    sb.AppendLine(indent + "{");
                    sb.AppendLine(indent + "    var temp = " + text + ";");
                    sb.AppendLine(indent + "    global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in temp);");
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

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="classGlobalName">The class global name used by this operation.</param>
    /// <param name="metaInfo">The language, framework, and generation settings.</param>
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
        sb.AppendLine("                {");
        sb.AppendLine("                    reader.Skip();");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                int propertyIndex;");
        sb.AppendLine("                if (isUtf8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var propNameUtf8 = reader.ReadStringUtf8();");
        sb.AppendLine("                    propertyIndex = GetJsonPropertyIndexUtf8(propNameUtf8);");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    var propNameUtf16 = reader.ReadStringUtf16();");
        sb.AppendLine("                    propertyIndex = GetJsonPropertyIndexUtf16(propNameUtf16);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (!reader.Read())");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        if (!data.isValueType)
        {
            sb.AppendLine("                if (propertyIndex == -2)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var id = reader.ReadUInt();");
            sb.AppendLine("                    objectId = id;");
            sb.AppendLine("                    reader.OptionState.AddObjectReference(id, preCreatedInstance);");
            sb.AppendLine("                    continue;");
            sb.AppendLine("                }");
            sb.AppendLine();
            sb.AppendLine("                if (propertyIndex == -3)");
            sb.AppendLine("                {");
            sb.AppendLine("                    var rId = reader.ReadUInt();");
            sb.AppendLine("                    refId = rId;");
            sb.AppendLine("                    value = (" + classGlobalName + ")reader.OptionState.GetObjectReference(rId);");
            sb.AppendLine("                    while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) {}");
            sb.AppendLine("                    return;");
            sb.AppendLine("                }");
            sb.AppendLine();
        }
        sb.AppendLine("                switch (propertyIndex)");
        sb.AppendLine("                {");
        foreach (LuminDataField field in data.fields)
        {
            sb.AppendLine("                    case " + data.fields.IndexOf(field) + ":");
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

    /// <summary>Appends JSON deserialization statements for the analyzed type.</summary>
    /// <param name="sb">The builder receiving generated source code.</param>
    /// <param name="field">The metadata describing the field being processed.</param>
    /// <param name="targetVar">The target var used by this operation.</param>
    /// <param name="indent">The indentation level of the generated statements.</param>
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
                    var enumNames = EnumEmitter.GetJsonEnumTypeNames(field.TypeSymbol);
                    sb.AppendLine(indent + "if (reader.IsNull())");
                    sb.AppendLine(indent + "    " + targetVar + " = default;");
                    sb.AppendLine(indent + "else");
                    sb.AppendLine(indent + "    " + targetVar + " = (" + text2 + ")reader." + enumNames.ReadMethod + "();");
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
                    sb.AppendLine(indent + "    global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref " + targetVar + ");");
                    sb.AppendLine(indent + "}");
                    break;
                }
            default:
                sb.AppendLine(indent + "reader.Skip();");
                break;
        }
    }

}
