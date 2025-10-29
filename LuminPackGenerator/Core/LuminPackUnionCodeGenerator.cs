using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using SymbolDisplayFormat = Microsoft.CodeAnalysis.SymbolDisplayFormat;

namespace LuminPack.Code.Core;

public static class LuminPackUnionCodeGenerator
{
    public static void UnionCodeGenerator(StringBuilder sb, LuminDataInfo data, MetaInfo metaInfo)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = data.className + "Parser";
        string classGlobalName = data.classFullName;
        string parserName = data.className + "Parser";

        if (data.isGeneric)
        {
            classFullName += $"<{data.GenericParameters.FirstOrDefault()}";
            for (var i = 1; i < data.GenericParameters.Count; i++)
            {
                classFullName += "," + data.GenericParameters[i];
            }

            classFullName += ">";
        }

        if (!classGlobalName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            classGlobalName = "global::" + data.classNameSpace + "." + data.classFullName;
        }

        ushort maxTag = data.UnionMembers.Count > 0
            ? data.UnionMembers.Max(m => m.Id)
            : (ushort)0;
        int directTableSize = maxTag + 1;

        // 定义新的 HashEntry 结构体
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(
            "        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]");
        sb.AppendLine("        public unsafe struct HashEntry");
        sb.AppendLine("        {");
        sb.AppendLine("            public IntPtr MethodTable;");
        sb.AppendLine("            public ushort Tag;");
        sb.AppendLine("            public delegate* managed<ref LuminPackWriter, ref " + classGlobalName +
                      ", void> WriteDelegate;");
        sb.AppendLine("            public delegate* managed<ref LuminPackReader, ref " + classGlobalName +
                      ", void> ReadDelegate;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // 使用 LuminUnionMap 作为注册表
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(
            $"        internal static readonly global::LuminPack.Utility.LuminUnionMap<HashEntry> _unionMap = new global::LuminPack.Utility.LuminUnionMap<HashEntry>({data.UnionMembers.Count});");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        internal static HashEntry[] _directTable {{ get; private set; }} = new HashEntry[{directTableSize}];");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        static object _registerLock = new object();");
        sb.AppendLine();

        // 生成静态序列化方法
        GenerateStaticSerializeMethods(data, sb, classGlobalName);
        sb.AppendLine();

        // 生成静态反序列化方法  
        GenerateStaticDeserializeMethods(data, sb, classGlobalName);
        sb.AppendLine();

        sb.AppendLine($"        static {parserName}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new {classFullName}());");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new ArrayParser<{classGlobalName}>());");
        sb.AppendLine();
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");

        // 注册所有联合成员
        for (int i = 0; i < data.UnionMembers.Count; i++)
        {
            var member = data.UnionMembers[i];
            string fullName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string methodName = GetMethodName(member.Type);

            sb.AppendLine(
                $"                Register(typeof({fullName}), {member.Id}, &Write{methodName}, &Read{methodName});");
        }

        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        // TryGetEntry 方法现在使用 LuminUnionMap
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(
            "        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine("        public static unsafe bool TryGetEntry(IntPtr mt, out HashEntry entry)");
        sb.AppendLine("        {");
        sb.AppendLine("            return _unionMap.TryGetValue(mt, out entry);");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Serialize 方法
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8
            ? $"        public override void Serialize(ref LuminPackWriter writer, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void Serialize(ref LuminPackWriter writer, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        GenerateSerializeCode(data, sb);
        sb.AppendLine("        }");
        sb.AppendLine();

        // Deserialize 方法
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8
            ? $"        public override void Deserialize(ref LuminPackReader reader, scoped ref {data.classFullName}{paraNullable} value)"
            : $"        public override void Deserialize(ref LuminPackReader reader, ref {data.classFullName}{paraNullable} value)");
        sb.AppendLine("        {");
        GenerateDeserializeCode(data, sb);
        sb.AppendLine("        }");
        sb.AppendLine();

        // CalculateOffset 方法
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8
            ? $"        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void CalculateOffset(ref LuminPackEvaluator evaluator, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        GenerateCalculateOffsetCode(data, sb, classGlobalName);
        sb.AppendLine("        }");
        sb.AppendLine();

        // Register 方法
        sb.AppendLine(
            "        public static unsafe void Register(System.Type type, ushort tag, delegate*<ref LuminPackWriter, ref " +
            classGlobalName + ", void> writeDelegate, delegate*<ref LuminPackReader, ref " + classGlobalName +
            ", void> readDelegate)");
        sb.AppendLine("        {");
        sb.AppendLine("            lock (_registerLock)");
        sb.AppendLine("            {");
        sb.AppendLine("                var mt = LuminPackMarshal.GetMethodTable(type);");
        sb.AppendLine("                var entry = new HashEntry");
        sb.AppendLine("                {");
        sb.AppendLine("                    MethodTable = mt,");
        sb.AppendLine("                    Tag = tag,");
        sb.AppendLine("                    WriteDelegate = writeDelegate,");
        sb.AppendLine("                    ReadDelegate = readDelegate");
        sb.AppendLine("                };");
        sb.AppendLine();
        sb.AppendLine("                if (!_unionMap.TryRegister(mt, entry))");
        sb.AppendLine("                {");
        sb.AppendLine(
            "                    throw new System.ArgumentException($\"An entry with the same method table already exists. Type: {type}\");");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (tag >= _directTable.Length)");
        sb.AppendLine("                {");
        sb.AppendLine("                    var oldDirectTable = _directTable;");
        sb.AppendLine("                    var newDirectSize = tag + 1;");
        sb.AppendLine("                    _directTable = new HashEntry[newDirectSize];");
        sb.AppendLine("                    System.Array.Copy(oldDirectTable, _directTable, oldDirectTable.Length);");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                _directTable[tag] = entry;");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    static void GenerateStaticSerializeMethods(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        foreach (var member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            string methodName = GetMethodName(member.Type);
            
            sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        private static unsafe void Write{methodName}(ref LuminPackWriter writer, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            // 静态方法中去掉 offset 相关代码
            sb.AppendLine($"            writer.WriteUnionHeader({member.Id});");
            sb.AppendLine($"            writer.WriteValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value));");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
    
    static void GenerateStaticDeserializeMethods(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        foreach (var member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            string methodName = GetMethodName(member.Type);
            
            sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        private static unsafe void Read{methodName}(ref LuminPackReader reader, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {memberType} tempValue = default!;");
            sb.AppendLine($"            reader.ReadValue(ref tempValue);");
            sb.AppendLine($"            value = LuminPackMarshal.As<{memberType}, {classGlobalName}>(ref tempValue!);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
    
    static string GetMemberType(LuminDataInfo data, LuminUnionMemberInfo member)
    {
        if (member.Type.IsUnboundGenericType)
        {
            string nameSpaceName = member.Type.ContainingNamespace?.ToDisplayString() ?? "";
            string memberType = nameSpaceName == "" 
                ? member.Type.Name 
                : "global::" + nameSpaceName + "." + member.Type.Name;
            
            memberType += $"<{data.GenericParameters.FirstOrDefault()}";
            for(var j = 1; j < data.GenericParameters.Count; j++)
            {
                memberType += "," + data.GenericParameters[j];
            }
            memberType += ">";
            
            return memberType;
        }
        else
        {
            return "global::" + member.Type.ToDisplayString();
        }
    }
    
    static string GetMethodName(ITypeSymbol type)
    {
        // 生成一个有效的方法名，移除特殊字符
        string name = type.ToDisplayString().Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_");
        return name;
    }
    
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classGlobalName = data.classFullName;
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        
        // 序列化方法中保留 offset
        sb.AppendLine("            ref int offset = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullUnionHeader(ref offset);");
            sb.AppendLine("                offset += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        
        sb.AppendLine();
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");
        sb.AppendLine("                var valueMT = LuminPackMarshal.GetMethodTable(value);");
        sb.AppendLine($"                if (!global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{data.className}Parser.TryGetEntry(valueMT, out var entry))");
        sb.AppendLine("                {");
        sb.AppendLine($"                    LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("                }");
        sb.AppendLine("                entry.WriteDelegate(ref writer, ref Unsafe.AsRef(in value!));");
        sb.AppendLine("            }");
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }

    public static void GenerateDeserializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classGlobalName = data.classFullName;
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        
        // 反序列化方法中保留 offset
        sb.AppendLine("            ref int offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryPeekUnionHeader(ref offset, out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");
        sb.AppendLine($"                ref var entry = ref global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{data.className}Parser._directTable[tag];");
        sb.AppendLine("                entry.ReadDelegate(ref reader, ref value!);");
        sb.AppendLine("            }");
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }
    
    public static void GenerateCalculateOffsetCode(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                evaluator += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        
        sb.AppendLine("            unsafe");
        sb.AppendLine("            {");
        sb.AppendLine("                var valueMT = LuminPackMarshal.GetMethodTable(value);");
        sb.AppendLine("                if (TryGetEntry(valueMT, out var entry))");
        sb.AppendLine("                {");
        sb.AppendLine("                    evaluator.CalculateUnionHeader(entry.Tag);");
        sb.AppendLine();
        
        if (data.UnionMembers.Count <= 4)
        {
            for (int i = 0; i < data.UnionMembers.Count; i++)
            {
                var member = data.UnionMembers[i];
                string memberType = GetMemberType(data, member);
                
                if (i == 0)
                    sb.AppendLine($"                    if (entry.Tag == {member.Id}) evaluator.CalculateValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value));");
                else
                    sb.AppendLine($"                    else if (entry.Tag == {member.Id}) evaluator.CalculateValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value));");
            }
        }
        else
        {
            sb.AppendLine("                    switch(entry.Tag)");
            sb.AppendLine("                    {");
            
            foreach (var member in data.UnionMembers)
            {
                string memberType = GetMemberType(data, member);
                sb.AppendLine($"                        case {member.Id}: evaluator.CalculateValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value)); break;");
            }
            
            sb.AppendLine("                    }");
        }
        
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
    }
}
