using System.Collections.Generic;
using System.Text;
using System;
using System.Linq;
using LuminPack.SourceGenerator;

#nullable enable
namespace LuminPack.Code.Core
{
    public static class LuminPackJsonCodeGenerator
    {
        private static LuminDataInfo? _dataInfo;
        
        internal static ulong ComputeUtf8Hash(string str)
        {
            var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(str);
            unsafe
            {
                fixed (byte* ptr = utf8Bytes)
                {
                    return ComputeXxHash3(ptr, utf8Bytes.Length);
                }
            }
        }
        
        internal static unsafe ulong ComputeUtf16Hash(string str)
        {
            fixed (char* charPtr = str)
            {
                byte* bytePtr = (byte*)charPtr;
                return ComputeXxHash3(bytePtr, str.Length * 2);
            }
        }
        
        private static unsafe ulong ComputeXxHash3(byte* input, int length)
        {
            const ulong Prime64_1 = 0x9E3779B185EBCA87UL;
            const ulong Prime64_3 = 0x165667B19E3779F9UL;
            
            if (length == 0)
                return Prime64_3 ^ 11400714819323198485UL ^ 2870177450012600261UL;
                
            if (length <= 16)
            {
                if (length > 8)
                {
                    ulong low = *(ulong*)input ^ (11400714819323198549UL);
                    ulong high = *(ulong*)(input + length - 8) ^ (2870177450012600325UL);
                    ulong hash = low + high + (ulong)length;
                    hash ^= hash >> 37;
                    hash *= Prime64_3;
                    hash ^= hash >> 32;
                    return hash;
                }
                
                if (length >= 4)
                {
                    ulong val1 = *(uint*)input;
                    ulong val2 = *(uint*)(input + length - 4);
                    ulong hash = (2870177450012600261UL + val1 + ((val2 << 32) | val2));
                    hash ^= hash >> 37;
                    hash *= Prime64_3;
                    hash ^= hash >> 32;
                    return hash;
                }
                
                byte c1 = input[0];
                byte c2 = input[length >> 1];
                byte c3 = input[length - 1];
                uint combined = ((uint)c1 << 16) | ((uint)c2 << 24) | c3 | ((uint)length << 8);
                ulong hash2 = (11400714819323198485UL + combined);
                hash2 ^= hash2 >> 37;
                hash2 *= Prime64_3;
                hash2 ^= hash2 >> 32;
                return hash2;
            }
            
            ulong acc = (ulong)length * Prime64_1;
            int remaining = length;
            byte* ptr = input;
            
            while (remaining >= 16)
            {
                ulong v1 = *(ulong*)ptr;
                ulong v2 = *(ulong*)(ptr + 8);
                acc += v1 * Prime64_1;
                acc += v2 * Prime64_3;
                ptr += 16;
                remaining -= 16;
            }
            
            if (remaining > 0)
            {
                if (remaining >= 8)
                {
                    acc += *(ulong*)ptr * Prime64_1;
                    ptr += 8;
                    remaining -= 8;
                }
                if (remaining >= 4)
                {
                    acc += *(uint*)ptr * Prime64_3;
                    ptr += 4;
                    remaining -= 4;
                }
                while (remaining > 0)
                {
                    acc += *ptr * Prime64_1;
                    ptr++;
                    remaining--;
                }
            }
            
            acc ^= acc >> 37;
            acc *= Prime64_3;
            acc ^= acc >> 32;
            return acc;
        }
        
        public static void GenerateStaticUtf8Fields(StringBuilder sb, LuminDataInfo data)
        {
            foreach (var field in data.fields)
            {
                string utf8FieldName = $"_utf8_{field.Name}";
                string utf16FieldName = $"_utf16_{field.Name}";
                string utf8HashFieldName = $"_utf8Hash_{field.Name}";
                string utf16HashFieldName = $"_utf16Hash_{field.Name}";
                
                var utf8Bytes = System.Text.Encoding.UTF8.GetBytes(field.Name);
                var bytesStr = string.Join(", ", utf8Bytes.Select(b => $"(byte){b}"));
                
                sb.AppendLine($"        private static readonly byte[] {utf8FieldName} = new byte[] {{ {bytesStr} }};");
                
                var charsStr = string.Join(", ", field.Name.Select(c => $"'{EscapeChar(c)}'"));
                sb.AppendLine($"        private static readonly char[] {utf16FieldName} = new char[] {{ {charsStr} }};");
                
                ulong utf8Hash = ComputeUtf8Hash(field.Name);
                sb.AppendLine($"        private const ulong {utf8HashFieldName} = {utf8Hash}UL;");
                
                ulong utf16Hash = ComputeUtf16Hash(field.Name);
                sb.AppendLine($"        private const ulong {utf16HashFieldName} = {utf16Hash}UL;");
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
                _ => c.ToString()
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
            // 预生成JSON片段常量（通用优化）
            for (int i = 0; i < data.fields.Count; i++)
            {
                var field = data.fields[i];
                
                if (i == 0)
                {
                    // 第一个字段：{"fieldName":
                    sb.AppendLine($"        private static readonly byte[] _jsonPrefixUtf8_{field.Name} = new byte[] {{ (byte)'{{', (byte)'\"', {string.Join(", ", field.Name.Select(c => $"(byte)'{c}'"))}, (byte)'\"', (byte)':' }};");
                    sb.AppendLine($"        private static readonly char[] _jsonPrefixUtf16_{field.Name} = new char[] {{ (char)'{{', (char)'\"', {string.Join(", ", field.Name.Select(c => $"(char)'{c}'"))}, (char)'\"', (char)':' }};");
                }
                else
                {
                    // 后续字段：,"fieldName":
                    sb.AppendLine($"        private static readonly byte[] _jsonSepUtf8_{field.Name} = new byte[] {{ (byte)',', (byte)'\"', {string.Join(", ", field.Name.Select(c => $"(byte)'{c}'"))}, (byte)'\"', (byte)':' }};");
                    sb.AppendLine($"        private static readonly char[] _jsonSepUtf16_{field.Name} = new char[] {{ (char)',', (char)'\"', {string.Join(", ", field.Name.Select(c => $"(char)'{c}'"))}, (char)'\"', (char)':' }};");
                }
            }
            sb.AppendLine();
            
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");

            if (data.isValueType)
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref {classGlobalName} value)"
                    : $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref {classGlobalName} value)");
            }
            else
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref {classGlobalName}? value)"
                    : $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref {classGlobalName}? value)");
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
            
            // 如果有 private/protected 字段，生成 As 转换
            if (data.fields.Count(x => x.IsPrivate || x.isProperty) > 0)
            {
                if (data.isValueType)
                {
                    sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
                }
                else
                {
                    sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
                }
                sb.AppendLine();
            }
            
            // 特殊处理：如果没有字段，使用标准方法
            if (data.fields.Count == 0)
            {
                sb.AppendLine("            writer.WriteObjectStart();");
                sb.AppendLine("            writer.WriteObjectEnd();");
                sb.AppendLine("        }");
                return;
            }
            
            // 不调用WriteObjectStart，使用预生成片段
            sb.AppendLine();
            sb.AppendLine("            bool isUtf8 = writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8;");
            sb.AppendLine();
            
            for (int i = 0; i < data.fields.Count; i++)
            {
                var field = data.fields[i];
                // 决定使用 local 还是 value 访问字段
                var access = field.IsPrivate || field.isProperty ? "local" : "value";
                string prefix = i == 0 ? "Prefix" : "Sep";
                
                sb.AppendLine($"            // 字段: {field.Name}");
                sb.AppendLine($"            if (isUtf8)");
                sb.AppendLine($"                writer.WriteRaw(_json{prefix}Utf8_{field.Name});");
                sb.AppendLine($"            else");
                sb.AppendLine($"                writer.WriteRaw(_json{prefix}Utf16_{field.Name});");
                sb.AppendLine($"            writer.SetFirstElement(true);");
                
                GenerateJsonSerializeField(sb, field, $"{access}.{field.Name}", "            ");
                
                sb.AppendLine();
            }
            
            sb.AppendLine("            writer.WriteByteRaw((byte)'}');");
            sb.AppendLine("        }");
        }
        
        private static void GenerateJsonSerializeField(StringBuilder sb, LuminDataField field, string valueExpr, string indent)
        {
            if (field.FieldType == LuminDataType.Reference)
            {
                sb.AppendLine($"{indent}if ({valueExpr} == null)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    writer.WriteNull();");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}else");
                sb.AppendLine($"{indent}{{");
                GenerateJsonSerializeFieldCore(sb, field, valueExpr, indent + "    ");
                sb.AppendLine($"{indent}}}");
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
                    sb.AppendLine($"{indent}writer.WriteInt({valueExpr});");
                    break;
                case LuminFiledType.UInt:
                    sb.AppendLine($"{indent}writer.WriteUInt({valueExpr});");
                    break;
                case LuminFiledType.Byte:
                    sb.AppendLine($"{indent}writer.WriteByte({valueExpr});");
                    break;
                case LuminFiledType.SByte:
                    sb.AppendLine($"{indent}writer.WriteSByte({valueExpr});");
                    break;
                case LuminFiledType.Short:
                    sb.AppendLine($"{indent}writer.WriteShort({valueExpr});");
                    break;
                case LuminFiledType.UShort:
                    sb.AppendLine($"{indent}writer.WriteUShort({valueExpr});");
                    break;
                case LuminFiledType.Long:
                    sb.AppendLine($"{indent}writer.WriteLong({valueExpr});");
                    break;
                case LuminFiledType.ULong:
                    sb.AppendLine($"{indent}writer.WriteULong({valueExpr});");
                    break;
                case LuminFiledType.Float:
                    sb.AppendLine($"{indent}writer.WriteFloat({valueExpr});");
                    break;
                case LuminFiledType.Double:
                    sb.AppendLine($"{indent}writer.WriteDouble({valueExpr});");
                    break;
                case LuminFiledType.Decimal:
                    sb.AppendLine($"{indent}writer.WriteDecimal({valueExpr});");
                    break;
                case LuminFiledType.Char:
                    sb.AppendLine($"{indent}writer.WriteChar({valueExpr});");
                    break;
                case LuminFiledType.String:
                    sb.AppendLine($"{indent}writer.WriteString({valueExpr});");
                    break;
                case LuminFiledType.Bool:
                    sb.AppendLine($"{indent}writer.WriteBool({valueExpr});");
                    break;
                case LuminFiledType.Enum:
                    sb.AppendLine($"{indent}writer.WriteInt((int){valueExpr});");
                    break;
                case LuminFiledType.Array:
                case LuminFiledType.List:
                case LuminFiledType.Class:
                case LuminFiledType.Struct:
                case LuminFiledType.Other:
                    string typeFullName = field.FullTypeName ?? field.Name;
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    var temp = {valueExpr};");
                    sb.AppendLine($"{indent}    global::LuminPack.LuminPackParseProvider.Cache<{typeFullName}>.Parser?.SerializeJson(ref writer, ref temp);");
                    sb.AppendLine($"{indent}}}");
                    break;
                default:
                    sb.AppendLine($"{indent}writer.WriteNull();");
                    break;
            }
        }
        
        public static void GenerateJsonDeserialize(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
        {
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            
            if (data.isValueType)
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref {classGlobalName} value)"
                    : $"        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref {classGlobalName} value)");
            }
            else
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref {classGlobalName}? value)"
                    : $"        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref {classGlobalName}? value)");
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
            
            sb.AppendLine("            // Temp vars");
            foreach (var field in data.fields)
            {
                string fieldType = field.FullTypeName ?? field.Name;
                string nullable = field.FieldType is LuminDataType.Reference ? "?" : "";
                sb.AppendLine($"            {fieldType}{nullable} {field.Name}Temp = default;");
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
            
            foreach (var field in data.fields)
            {
                sb.AppendLine($"                        case _utf8Hash_{field.Name}:");
                sb.AppendLine("                        {");
                GenerateJsonDeserializeField(sb, field, $"{field.Name}Temp", "                            ");
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
            
            foreach (var field in data.fields)
            {
                sb.AppendLine($"                        case _utf16Hash_{field.Name}:");
                sb.AppendLine("                        {");
                GenerateJsonDeserializeField(sb, field, $"{field.Name}Temp", "                            ");
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
            
            GenerateObjectConstruction(sb, data, classGlobalName);
            
            sb.AppendLine("        }");
        }
        
        private static void GenerateJsonDeserializeField(StringBuilder sb, LuminDataField field, string targetVar, string indent)
        {
            if (field.FieldType is LuminDataType.Reference)
            {
                sb.AppendLine($"{indent}if (reader.IsNull())");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    {targetVar} = null;");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}else");
                sb.AppendLine($"{indent}{{");
                GenerateJsonDeserializeFieldCore(sb, field, targetVar, indent + "    ");
                sb.AppendLine($"{indent}}}");
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
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadInt();");
                    break;
                case LuminFiledType.UInt:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadUInt();");
                    break;
                case LuminFiledType.Byte:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadByte();");
                    break;
                case LuminFiledType.SByte:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadSByte();");
                    break;
                case LuminFiledType.Short:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadShort();");
                    break;
                case LuminFiledType.UShort:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadUShort();");
                    break;
                case LuminFiledType.Long:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadLong();");
                    break;
                case LuminFiledType.ULong:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadULong();");
                    break;
                case LuminFiledType.Float:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadFloat();");
                    break;
                case LuminFiledType.Double:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadDouble();");
                    break;
                case LuminFiledType.Decimal:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadDecimal();");
                    break;
                case LuminFiledType.Char:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadChar();");
                    break;
                case LuminFiledType.String:
                    sb.AppendLine($"{indent}{targetVar} = reader.ReadString();");
                    break;
                case LuminFiledType.Bool:
                    sb.AppendLine($"{indent}{targetVar} = reader.GetBoolean();");
                    break;
                case LuminFiledType.Enum:
                    string enumType = field.FullTypeName ?? field.Name;
                    sb.AppendLine($"{indent}{targetVar} = ({enumType})reader.ReadInt();");
                    break;
                case LuminFiledType.Array:
                case LuminFiledType.List:
                case LuminFiledType.Class:
                case LuminFiledType.Struct:
                case LuminFiledType.Other:
                    string typeFullName = field.FullTypeName ?? field.Name;
                    sb.AppendLine($"{indent}global::LuminPack.LuminPackParseProvider.Cache<{typeFullName}>.Parser?.DeserializeJson(ref reader, ref {targetVar});");
                    break;
                default:
                    sb.AppendLine($"{indent}reader.Skip();");
                    break;
            }
        }
        
        private static void GenerateObjectConstruction(StringBuilder sb, LuminDataInfo data, string classGlobalName)
        {
            // 收集构造函数参数
            var constructorParams = new List<string>();
            if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
            {
                foreach (var param in data.SelectedConstructor.Parameters)
                {
                    var matchingField = data.fields.FirstOrDefault(f => f.Name == param.MatchingFieldName);
                    if (matchingField != null)
                    {
                        constructorParams.Add($"{matchingField.Name}Temp!");
                    }
                    else
                    {
                        constructorParams.Add("default");
                    }
                }
            }

            string constructorArgs = string.Join(", ", constructorParams);

            // 收集需要对象初始化器赋值的 public 字段
            var initializerFields = data.fields.Where(f => 
                (data.SelectedConstructor == null || 
                 !data.SelectedConstructor.Parameters.Any(p => p.MatchingFieldName == f.Name)) &&
                !f.IsPrivate && !f.isProperty
            ).ToList();

            // 收集需要单独设置的 private 字段
            var privateFields = data.fields.Where(f => 
                (data.SelectedConstructor == null || 
                 !data.SelectedConstructor.Parameters.Any(p => p.MatchingFieldName == f.Name)) &&
                (f.IsPrivate || f.isProperty)
            ).ToList();

            // 处理对象池分配
            if (data.RentPoolMethod != null)
            {
                // 使用对象池分配
                if (data.RentPoolMethod.ReturnsByRef)
                    sb.AppendLine($"            value = ref {classGlobalName}.{data.RentPoolMethod.Name}()!;");
                else
                {
                    sb.AppendLine($"            value = {classGlobalName}.{data.RentPoolMethod.Name}();");
                }
    
                // 对于对象池分配的对象，直接设置所有字段，不依赖构造函数
                sb.AppendLine($"            // 设置所有字段（对象池分配）");
    
                // 通过 Local 类设置所有字段（包括 public 和 private）
                if (data.isValueType)
                {
                    sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
                }
                else
                {
                    sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
                }
    
                // 设置所有字段
                foreach (var field in data.fields)
                {
                    sb.AppendLine($"            local.{field.Name} = {field.Name}Temp!;");
                }
            }
            else
            {
                // 使用对象初始化器创建对象
                if (initializerFields.Count > 0)
                {
                    // 有对象初始化器的情况
                    if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                    {
                        sb.AppendLine($"            value = new {classGlobalName}({constructorArgs})");
                    }
                    else
                    {
                        sb.AppendLine($"            value = new {classGlobalName}()");
                    }
                    sb.AppendLine("            {");
                    foreach (var field in initializerFields)
                    {
                        sb.AppendLine($"                {field.Name} = {field.Name}Temp!,");
                    }
                    sb.AppendLine("            };");
                }
                else
                {
                    // 没有对象初始化器的情况
                    if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                    {
                        sb.AppendLine($"            value = new {classGlobalName}({constructorArgs});");
                    }
                    else
                    {
                        sb.AppendLine($"            value = new {classGlobalName}();");
                    }
                }

                // 设置 private 字段（通过 Local 类）
                if (privateFields.Count > 0)
                {
                    sb.AppendLine($"            // 设置 private 字段");
                    if (data.isValueType)
                    {
                        sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
                    }
                    else
                    {
                        sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
                    }
        
                    foreach (var field in privateFields)
                    {
                        sb.AppendLine($"            local.{field.Name} = {field.Name}Temp!;");
                    }
                }
            }
        }
    }
    
    public static class LuminPackJsonCircleReferenceCodeGenerator
    {
        // JSON 循环引用的特殊字段名
        private const string JSON_ID_FIELD = "$id";
        private const string JSON_REF_FIELD = "$ref";
        
        /// <summary>
        /// 生成循环引用特殊字段的静态UTF-8和UTF-16字段及哈希常量
        /// </summary>
        public static void GenerateStaticUtf8FieldsForCircleReference(StringBuilder sb)
        {
            // 生成 $id 的 UTF8 静态字段
            var idBytes = System.Text.Encoding.UTF8.GetBytes(JSON_ID_FIELD);
            var idBytesStr = string.Join(", ", idBytes.Select(b => $"(byte){b}"));
            sb.AppendLine($"        private static readonly byte[] _utf8_id = new byte[] {{ {idBytesStr} }};");
    
            // 生成 $id 的 UTF16 静态字段
            var idCharsStr = string.Join(", ", JSON_ID_FIELD.Select(c => $"'{EscapeChar(c)}'"));
            sb.AppendLine($"        private static readonly char[] _utf16_id = new char[] {{ {idCharsStr} }};");
    
            // 生成 $id 的哈希值
            ulong idUtf8Hash = LuminPackJsonCodeGenerator.ComputeUtf8Hash(JSON_ID_FIELD);
            sb.AppendLine($"        private const ulong _utf8Hash_id = {idUtf8Hash}UL;");
    
            ulong idUtf16Hash = LuminPackJsonCodeGenerator.ComputeUtf16Hash(JSON_ID_FIELD);
            sb.AppendLine($"        private const ulong _utf16Hash_id = {idUtf16Hash}UL;");
            sb.AppendLine();
    
            // 生成 $ref 的 UTF8 静态字段
            var refBytes = System.Text.Encoding.UTF8.GetBytes(JSON_REF_FIELD);
            var refBytesStr = string.Join(", ", refBytes.Select(b => $"(byte){b}"));
            sb.AppendLine($"        private static readonly byte[] _utf8_ref = new byte[] {{ {refBytesStr} }};");
    
            // 生成 $ref 的 UTF16 静态字段
            var refCharsStr = string.Join(", ", JSON_REF_FIELD.Select(c => $"'{EscapeChar(c)}'"));
            sb.AppendLine($"        private static readonly char[] _utf16_ref = new char[] {{ {refCharsStr} }};");
    
            // 生成 $ref 的哈希值
            ulong refUtf8Hash = LuminPackJsonCodeGenerator.ComputeUtf8Hash(JSON_REF_FIELD);
            sb.AppendLine($"        private const ulong _utf8Hash_ref = {refUtf8Hash}UL;");
    
            ulong refUtf16Hash = LuminPackJsonCodeGenerator.ComputeUtf16Hash(JSON_REF_FIELD);
            sb.AppendLine($"        private const ulong _utf16Hash_ref = {refUtf16Hash}UL;");
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
                _ => c.ToString()
            };
        }
        
        /// <summary>
        /// 生成支持循环引用的 JSON 序列化方法
        /// </summary>
        public static void GenerateJsonSerializeWithCircleReference(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
        {
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");

            if (data.isValueType)
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref {classGlobalName} value)"
                    : $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref {classGlobalName} value)");
            }
            else
            {
                sb.AppendLine(metaInfo.IsNet8 
                    ? $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref {classGlobalName}? value)"
                    : $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref {classGlobalName}? value)");
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
                
                // 检查循环引用
                sb.AppendLine("            var (existsReference, id) = writer.OptionState.GetOrAddReference(value);");
                sb.AppendLine("            if (existsReference)");
                sb.AppendLine("            {");
                sb.AppendLine("                writer.WriteObjectStart();");
                sb.AppendLine($"                if (isUtf8)");
                sb.AppendLine($"                    writer.WritePropertyName(_utf8_ref);");
                sb.AppendLine($"                else");
                sb.AppendLine($"                    writer.WritePropertyName(_utf16_ref);");
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
            
            // 如果有 private/protected 字段，生成 As 转换
            if (data.fields.Count(x => x.IsPrivate || x.isProperty) > 0)
            {
                if (data.isValueType)
                {
                    sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value);");
                }
                else
                {
                    sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref value!);");
                }
                sb.AppendLine();
            }
            
            sb.AppendLine("            writer.WriteObjectStart();");
            
            // 写入 $id 字段（仅对引用类型）
            if (!data.isValueType)
            {
                sb.AppendLine();
                sb.AppendLine("            // 写入对象ID");
                sb.AppendLine($"            if (isUtf8)");
                sb.AppendLine($"                writer.WritePropertyName(_utf8_id);");
                sb.AppendLine($"            else");
                sb.AppendLine($"                writer.WritePropertyName(_utf16_id);");
                sb.AppendLine("             writer.WriteUInt(id);");
            }
            
            sb.AppendLine();
            
            foreach (var field in data.fields)
            {
                // 决定使用 local 还是 value 访问字段
                bool useLocal = field.IsPrivate || field.isProperty;
                string accessor = useLocal ? "local" : "value";
                
                sb.AppendLine($"            // 字段: {field.Name}");
                sb.AppendLine($"            if (isUtf8)");
                sb.AppendLine($"                writer.WritePropertyName(_utf8_{field.Name});");
                sb.AppendLine($"            else");
                sb.AppendLine($"                writer.WritePropertyName(_utf16_{field.Name});");
                
                GenerateJsonSerializeFieldWithCircleReference(sb, field, accessor, "            ");
                sb.AppendLine();
            }
            
            sb.AppendLine("            writer.WriteObjectEnd();");
            sb.AppendLine("        }");
        }
        
        /// <summary>
        /// 生成单个字段的序列化代码（支持循环引用）
        /// </summary>
        private static void GenerateJsonSerializeFieldWithCircleReference(StringBuilder sb, LuminDataField field, string accessor, string indent)
        {
            bool isNullable = (field.FieldType == LuminDataType.Reference);
            string fieldAccess = $"{accessor}.{field.Name}";
            
            if (isNullable)
            {
                sb.AppendLine($"{indent}if ({fieldAccess} == null)");
                sb.AppendLine($"{indent}{{");
                sb.AppendLine($"{indent}    writer.WriteNull();");
                sb.AppendLine($"{indent}}}");
                sb.AppendLine($"{indent}else");
                sb.AppendLine($"{indent}{{");
                indent += "    ";
            }
            
            switch (field.Type)
            {
                case LuminFiledType.Int:
                    sb.AppendLine($"{indent}writer.WriteInt({fieldAccess});");
                    break;
                case LuminFiledType.UInt:
                    sb.AppendLine($"{indent}writer.WriteUInt({fieldAccess});");
                    break;
                case LuminFiledType.Byte:
                    sb.AppendLine($"{indent}writer.WriteByte({fieldAccess});");
                    break;
                case LuminFiledType.SByte:
                    sb.AppendLine($"{indent}writer.WriteSByte({fieldAccess});");
                    break;
                case LuminFiledType.Short:
                    sb.AppendLine($"{indent}writer.WriteShort({fieldAccess});");
                    break;
                case LuminFiledType.UShort:
                    sb.AppendLine($"{indent}writer.WriteUShort({fieldAccess});");
                    break;
                case LuminFiledType.Long:
                    sb.AppendLine($"{indent}writer.WriteLong({fieldAccess});");
                    break;
                case LuminFiledType.ULong:
                    sb.AppendLine($"{indent}writer.WriteULong({fieldAccess});");
                    break;
                case LuminFiledType.Float:
                    sb.AppendLine($"{indent}writer.WriteFloat({fieldAccess});");
                    break;
                case LuminFiledType.Double:
                    sb.AppendLine($"{indent}writer.WriteDouble({fieldAccess});");
                    break;
                case LuminFiledType.Decimal:
                    sb.AppendLine($"{indent}writer.WriteDecimal({fieldAccess});");
                    break;
                case LuminFiledType.Char:
                    sb.AppendLine($"{indent}writer.WriteChar({fieldAccess});");
                    break;
                case LuminFiledType.String:
                    sb.AppendLine($"{indent}writer.WriteString({fieldAccess});");
                    break;
                case LuminFiledType.Bool:
                    sb.AppendLine($"{indent}writer.WriteBool({fieldAccess});");
                    break;
                case LuminFiledType.Enum:
                    sb.AppendLine($"{indent}writer.WriteInt((int){fieldAccess});");
                    break;
                case LuminFiledType.Array:
                case LuminFiledType.List:
                case LuminFiledType.Class:
                case LuminFiledType.Struct:
                case LuminFiledType.Other:
                    string typeFullName = field.FullTypeName ?? field.Name;
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    var temp = {fieldAccess};");
                    sb.AppendLine($"{indent}    global::LuminPack.LuminPackParseProvider.Cache<{typeFullName}>.Parser!.SerializeJson(ref writer, ref temp);");
                    sb.AppendLine($"{indent}}}");
                    break;
                default:
                    sb.AppendLine($"{indent}writer.WriteNull();");
                    break;
            }
            
            if (isNullable)
            {
                sb.AppendLine($"{indent.Substring(0, indent.Length - 4)}}}");
            }
        }
        
        /// <summary>
        /// 生成支持循环引用的 JSON 反序列化方法
        /// </summary>
        public static void GenerateJsonDeserializeWithCircleReference(StringBuilder sb, LuminDataInfo data, string classGlobalName, MetaInfo metaInfo)
        {
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
            
            string valueTypeParam = data.isValueType ? "" : "?";
            string scopedKeyword = metaInfo.IsNet8 ? "scoped " : "";
            
            sb.AppendLine($"        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, {scopedKeyword}ref {classGlobalName}{valueTypeParam} value)");
            
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
                sb.AppendLine($"            {classGlobalName} preCreatedInstance;");
                
                // 对象池优先
                if (data.RentPoolMethod != null)
                {
                    if (data.RentPoolMethod.ReturnsByRef)
                        sb.AppendLine($"            preCreatedInstance = ref {classGlobalName}.{data.RentPoolMethod.Name}()!;");
                    else
                        sb.AppendLine($"            preCreatedInstance = {classGlobalName}.{data.RentPoolMethod.Name}();");
                }
                else
                {
                    // 构造函数参数处理（使用默认值）
                    var constructorParams = new List<string>();
                    if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                    {
                        foreach (var param in data.SelectedConstructor.Parameters)
                        {
                            constructorParams.Add("default!");
                        }
                    }
                    
                    string constructorArgs = string.Join(", ", constructorParams);
                    
                    if (data.SelectedConstructor != null && data.SelectedConstructor.Parameters.Count > 0)
                    {
                        sb.AppendLine($"            preCreatedInstance = new {classGlobalName}({constructorArgs});");
                    }
                    else
                    {
                        sb.AppendLine($"            preCreatedInstance = new {classGlobalName}();");
                    }
                }
                
                sb.AppendLine("            value = preCreatedInstance;");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("            value = default;");
            }
            
            foreach (var field in data.localFields)
            {
                var nullable = field.IsValue ? "" : "?";
                sb.AppendLine($"            {field.TypeName}{nullable} {field.Name}Temp = default!;");
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
                sb.AppendLine($"                if (propHash == _utf8Hash_id || propHash == _utf16Hash_id)");
                sb.AppendLine("                {");
                sb.AppendLine("                    var id = reader.ReadUInt();");
                sb.AppendLine("                    objectId = id;");
                sb.AppendLine("                    reader.OptionState.AddObjectReference(id, preCreatedInstance);");
                sb.AppendLine("                    continue;");
                sb.AppendLine("                }");
                sb.AppendLine();
                sb.AppendLine($"                if (propHash == _utf8Hash_ref || propHash == _utf16Hash_ref)");
                sb.AppendLine("                {");
                sb.AppendLine("                    var rId = reader.ReadUInt();");
                sb.AppendLine("                    refId = rId;");
                sb.AppendLine($"                    value = ({classGlobalName})reader.OptionState.GetObjectReference(rId);");
                sb.AppendLine("                    while (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) {}");
                sb.AppendLine("                    return;");
                sb.AppendLine("                }");
                sb.AppendLine();
            }
            
            sb.AppendLine("                switch (propHash)");
            sb.AppendLine("                {");
            
            foreach (var field in data.fields)
            {
                sb.AppendLine($"                    case _utf8Hash_{field.Name}:");
                sb.AppendLine($"                    case _utf16Hash_{field.Name}:");
                sb.AppendLine("                    {");
                GenerateJsonDeserializeFieldWithCircleReference(sb, field, $"{field.Name}Temp", "                        ");
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
            
            
            var publicFields = data.fields.Where(f => !f.IsPrivate && !f.isProperty).ToList();
            var privatePropFields = data.fields.Where(f => f.IsPrivate || f.isProperty).ToList();
            
            // 对于对象池分配的对象，所有字段都通过 Local 设置
            if (data.RentPoolMethod != null)
            {
                sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref {(data.isValueType ? "value" : "value!")});");
                
                foreach (var field in data.fields)
                {
                    sb.AppendLine($"            local.{field.Name} = {field.Name}Temp!;");
                }
            }
            else
            {
                // Public 字段直接设置
                if (publicFields.Count > 0)
                {
                    foreach (var field in publicFields)
                    {
                        sb.AppendLine($"            value.{field.Name} = {field.Name}Temp!;");
                    }
                    sb.AppendLine();
                }
                
                // Private/property 字段通过 Local 设置
                if (privatePropFields.Count > 0)
                {
                    sb.AppendLine($"            ref var local = ref global::LuminPack.Code.LuminPackMarshal.As<{classGlobalName}, {TypeMetaChecker.BuildLocalClassName(data)}>(ref {(data.isValueType ? "value" : "value!")});");
                    
                    foreach (var field in privatePropFields)
                    {
                        sb.AppendLine($"            local.{field.Name} = {field.Name}Temp!;");
                    }
                }
            }
            
            sb.AppendLine("        }");
        }
        
        /// <summary>
        /// 生成单个字段的反序列化代码（支持循环引用）
        /// </summary>
        private static void GenerateJsonDeserializeFieldWithCircleReference(StringBuilder sb, LuminDataField field, string targetVar, string indent)
        {
            switch (field.Type)
            {
                case LuminFiledType.Int:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadInt();");
                    break;
                case LuminFiledType.UInt:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadUInt();");
                    break;
                case LuminFiledType.Byte:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadByte();");
                    break;
                case LuminFiledType.SByte:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadSByte();");
                    break;
                case LuminFiledType.Short:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadShort();");
                    break;
                case LuminFiledType.UShort:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadUShort();");
                    break;
                case LuminFiledType.Long:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadLong();");
                    break;
                case LuminFiledType.ULong:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadULong();");
                    break;
                case LuminFiledType.Float:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadFloat();");
                    break;
                case LuminFiledType.Double:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadDouble();");
                    break;
                case LuminFiledType.Decimal:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadDecimal();");
                    break;
                case LuminFiledType.Char:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadChar();");
                    break;
                case LuminFiledType.String:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    {targetVar} = null;");
                    sb.AppendLine($"{indent}}}");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    {targetVar} = reader.ReadString();");
                    sb.AppendLine($"{indent}}}");
                    break;
                case LuminFiledType.Bool:
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = reader.GetBoolean();");
                    break;
                case LuminFiledType.Enum:
                    string enumType = field.FullTypeName ?? field.Name;
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}    {targetVar} = default;");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}    {targetVar} = ({enumType})reader.ReadInt();");
                    break;
                case LuminFiledType.Array:
                case LuminFiledType.List:
                case LuminFiledType.Class:
                case LuminFiledType.Struct:
                case LuminFiledType.Other:
                    string typeFullName = field.FullTypeName ?? field.Name;
                    sb.AppendLine($"{indent}if (reader.IsNull())");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    {targetVar} = null;");
                    sb.AppendLine($"{indent}}}");
                    sb.AppendLine($"{indent}else");
                    sb.AppendLine($"{indent}{{");
                    sb.AppendLine($"{indent}    global::LuminPack.LuminPackParseProvider.Cache<{typeFullName}>.Parser!.DeserializeJson(ref reader, ref {targetVar});");
                    sb.AppendLine($"{indent}}}");
                    break;
                default:
                    sb.AppendLine($"{indent}reader.Skip();");
                    break;
            }
        }
        
        /// <summary>
        /// 获取解析器类名
        /// </summary>
        private static string GetParserClassName(string typeFullName)
        {
            var cleanName = typeFullName
                .Replace("global::", "")
                .Replace(".", "_")
                .Replace("<", "_")
                .Replace(">", "")
                .Replace(",", "")
                .Replace(" ", "");
            
            return cleanName + "Parser";
        }
    }

    
}