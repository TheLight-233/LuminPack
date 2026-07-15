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
        string classFullName = TypeMetaChecker.BuildParserClassName(data);
        string classGlobalName = data.classFullName;
        string parserName = classFullName;
        int unionMemberCount = data.UnionMembers.Count;
        
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
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        public delegate void WriteDelegateType(ref LuminPackWriter writer, ref {classGlobalName} value);");
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        public delegate void WriteJsonDelegateType(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref {classGlobalName} value);");
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        public delegate void ReadDelegateType(ref LuminPackReader reader, ref {classGlobalName} value);");
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        public delegate void ReadJsonDelegateType(ref global::LuminPack.Core.LuminPackJsonReader reader, ref {classGlobalName} value);");
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        public struct HashEntry");
        sb.AppendLine("        {");
        sb.AppendLine("            public ushort Tag;");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        public struct WriteEntry");
        sb.AppendLine("        {");
        sb.AppendLine("            public WriteDelegateType WriteDelegate;");
        sb.AppendLine("            public WriteJsonDelegateType WriteJsonDelegate;");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        public struct ReadEntry");
        sb.AppendLine("        {");
        sb.AppendLine("            public ReadDelegateType ReadDelegate;");
        sb.AppendLine("            public ReadJsonDelegateType ReadJsonDelegate;");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        internal static readonly global::LuminPack.Utility.LuminUnionMap<HashEntry> _unionMap = new global::LuminPack.Utility.LuminUnionMap<HashEntry>({data.UnionMembers.Count});");
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        internal static readonly global::LuminPack.Utility.LuminUnionMap<WriteEntry> _externalWriteMap = new global::LuminPack.Utility.LuminUnionMap<WriteEntry>(4);");
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        internal static readonly global::LuminPack.Utility.LuminUnionMap<ReadEntry> _externalMap = new global::LuminPack.Utility.LuminUnionMap<ReadEntry>(4);");
        sb.AppendLine();
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine($"        static object _registerLock = new object();");
        sb.AppendLine();
        
        GenerateStaticSerializeMethods(data, sb, classGlobalName);
        sb.AppendLine();
        
        GenerateStaticDeserializeMethods(data, sb, classGlobalName);
        sb.AppendLine();
        
        GenerateStaticJsonSerializeMethods(data, sb, classGlobalName);
        sb.AppendLine();

        GenerateStaticJsonDeserializeMethods(data, sb, classGlobalName);
        sb.AppendLine();
        
        sb.AppendLine($"        static {parserName}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new {classFullName}());");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new ArrayParser<{classGlobalName}>());");
        sb.AppendLine();

        for (int i = 0; i < data.UnionMembers.Count; i++)
        {
            var member = data.UnionMembers[i];
            string fullName = member.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            sb.AppendLine($"            _unionMap.TryRegister(LuminPackMarshal.GetMethodTable(typeof({fullName})), new HashEntry {{ Tag = {member.Id} }});");
        }

        sb.AppendLine("        }");
        sb.AppendLine();

        // Serialize 方法
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? $"        public override void Serialize(ref LuminPackWriter writer, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void Serialize(ref LuminPackWriter writer, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        GenerateSerializeCode(data, sb);
        sb.AppendLine("        }");
        sb.AppendLine();

        // Deserialize 方法
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
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
        
        // SerializeJson 方法
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void SerializeJson(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        GenerateSerializeJsonCode(data, sb, classGlobalName);
        sb.AppendLine("        }");
        sb.AppendLine();

        // DeserializeJson 方法
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine("        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        sb.AppendLine(metaInfo.IsNet8
            ? $"        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void DeserializeJson(ref global::LuminPack.Core.LuminPackJsonReader reader, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        GenerateDeserializeJsonCode(data, sb, classGlobalName);
        sb.AppendLine("        }");
        sb.AppendLine();
        
        sb.AppendLine(
            "        public static void Register(System.Type type, ushort tag, " +
            "WriteDelegateType writeDelegate, " +
            "ReadDelegateType readDelegate, " +
            "WriteJsonDelegateType writeJsonDelegate, " +
            "ReadJsonDelegateType readJsonDelegate)");
        sb.AppendLine("        {");
        sb.AppendLine("            lock (_registerLock)");
        sb.AppendLine("            {");
        sb.AppendLine("                var mt = LuminPackMarshal.GetMethodTable(type);");
        sb.AppendLine();
        sb.AppendLine("                if (!_unionMap.TryRegister(mt, new HashEntry { Tag = tag }))");
        sb.AppendLine("                    throw new System.ArgumentException($\"An entry with the same method table already exists. Type: {type}\");");
        sb.AppendLine();
        sb.AppendLine("                _externalWriteMap.TryRegister(mt, new WriteEntry");
        sb.AppendLine("                {");
        sb.AppendLine("                    WriteDelegate = writeDelegate,");
        sb.AppendLine("                    WriteJsonDelegate = writeJsonDelegate,");
        sb.AppendLine("                });");
        sb.AppendLine("                _externalMap.TryRegister((nint)(uint)tag, new ReadEntry");
        sb.AppendLine("                {");
        sb.AppendLine("                    ReadDelegate = readDelegate,");
        sb.AppendLine("                    ReadJsonDelegate = readJsonDelegate,");
        sb.AppendLine("                });");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    static void GenerateStaticSerializeMethods(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        if (!data.UnionMembers.Any())
            return;
        
        var maxTag = data.UnionMembers.Max(x => x.Id);
        foreach (var member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            string methodName = GetMethodName(member.Type);
            
            sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public static void Write{methodName}(ref LuminPackWriter writer, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            sb.AppendLine(maxTag <= 255 && !data.IsWideTag
                ? $"            writer.WriteUnionHeader({member.Id});"
                : $"            writer.WriteWideUnionHeader({member.Id});");
            sb.AppendLine($"            writer.WritePolymorphismValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value));");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
    
    static void GenerateStaticDeserializeMethods(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        if (!data.UnionMembers.Any())
            return;
        
        foreach (var member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            string methodName = GetMethodName(member.Type);
            
            sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public static void Read{methodName}(ref LuminPackReader reader, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {memberType} tempValue = default!;");
            sb.AppendLine($"            reader.ReadPolymorphismValue(ref tempValue);");
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
            for (var j = 1; j < data.GenericParameters.Count; j++)
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
        string name = type.ToDisplayString().Replace(".", "_").Replace("<", "_").Replace(">", "_").Replace(",", "_");
        return name;
    }
    
    public static void GenerateSerializeCode(LuminDataInfo data, StringBuilder sb)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = TypeMetaChecker.BuildParserClassName(data);
        string classGlobalName = data.classFullName;
        string genericParameters = data.GenericParameters.Count == 0 ? string.Empty : "<" + string.Join(", ", data.GenericParameters) + ">";
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                ref int offset = ref writer.GetCurrentSpanOffset();");
            sb.AppendLine("                writer.WriteNullUnionHeader(ref offset);");
            sb.AppendLine("                offset += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        
        sb.AppendLine();
        sb.AppendLine("            var valueMT = LuminPackMarshal.GetMethodTable(value);");
        sb.AppendLine($"            ref var entry = ref global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}._unionMap.TryGetValueRef(valueMT);");
        sb.AppendLine("            if (global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref entry))");
        sb.AppendLine("            {");
        sb.AppendLine($"                LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            switch (entry.Tag)");
        sb.AppendLine("            {");
        foreach (var member in data.UnionMembers)
        {
            string methodName = GetMethodName(member.Type);
            sb.AppendLine($"                case {member.Id}: global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}.Write{methodName}(ref writer, ref Unsafe.AsRef(in value!)); break;");
        }
        sb.AppendLine("                default:");
        sb.AppendLine("                {");
        sb.AppendLine($"                    ref var writeEntry = ref global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}._externalWriteMap.TryGetValueRef(valueMT);");
        sb.AppendLine("                    if (!global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref writeEntry))");
        sb.AppendLine("                        writeEntry.WriteDelegate(ref writer, ref Unsafe.AsRef(in value!));");
        sb.AppendLine("                    else");
        sb.AppendLine($"                        LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("                    break;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        
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
        string classFullName = TypeMetaChecker.BuildParserClassName(data);
        string classGlobalName = data.classFullName;
        string genericParameters = data.GenericParameters.Count == 0 ? string.Empty : "<" + string.Join(", ", data.GenericParameters) + ">";
        var maxTag = data.UnionMembers.Max(x => x.Id);
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        
        sb.AppendLine(maxTag <= 255 && !data.IsWideTag
            ? "            if (!reader.TryPeekUnionHeader(out var tag))"
            : "            if (!reader.TryPeekWideUnionHeader(out var tag))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();

        sb.AppendLine("            switch (tag)");
        sb.AppendLine("            {");
        foreach (var member in data.UnionMembers)
        {
            string methodName = GetMethodName(member.Type);
            sb.AppendLine($"                case {member.Id}: global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}.Read{methodName}(ref reader, ref value!); break;");
        }
        sb.AppendLine("                default:");
        sb.AppendLine($"                    if (global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}._externalMap.TryGetValue((nint)(uint)tag, out var extEntry))");
        sb.AppendLine("                        extEntry.ReadDelegate(ref reader, ref value!);");
        sb.AppendLine("                    else");
        sb.AppendLine($"                        LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof({classGlobalName}));");
        sb.AppendLine("                    break;");
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
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                evaluator += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        
        sb.AppendLine("            var valueMT = LuminPackMarshal.GetMethodTable(value);");
        sb.AppendLine("            if (_unionMap.TryGetValue(valueMT, out var entry))");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator.CalculateUnionHeader(entry.Tag);");
        sb.AppendLine();
        
        if (data.UnionMembers.Count <= 4)
        {
            for (int i = 0; i < data.UnionMembers.Count; i++)
            {
                var member = data.UnionMembers[i];
                string memberType = GetMemberType(data, member);
                
                if (i == 0)
                    sb.AppendLine($"                if (entry.Tag == {member.Id}) evaluator.CalculateValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value));");
                else
                    sb.AppendLine($"                else if (entry.Tag == {member.Id}) evaluator.CalculateValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value));");
            }
        }
        else
        {
            sb.AppendLine("                switch(entry.Tag)");
            sb.AppendLine("                {");
            
            foreach (var member in data.UnionMembers)
            {
                string memberType = GetMemberType(data, member);
                sb.AppendLine($"                    case {member.Id}: evaluator.CalculateValue(LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value)); break;");
            }
            
            sb.AppendLine("                }");
        }
        
        sb.AppendLine("            }");
        sb.AppendLine();
    }
    
    static void GenerateStaticJsonSerializeMethods(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        if (!data.UnionMembers.Any())
            return;
        
        foreach (var member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            string methodName = GetMethodName(member.Type);
            
            sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public static void WriteJson{methodName}(ref global::LuminPack.Core.LuminPackJsonWriter writer, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            sb.AppendLine("            writer.WriteObjectStart();");
            sb.AppendLine("            if (writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
            sb.AppendLine("                writer.WritePropertyName(LuminPackConstUtf8.TypeU8);");
            sb.AppendLine("            else");
            sb.AppendLine("                writer.WritePropertyName(LuminPackConstUtf8.TypeU16);");
            sb.AppendLine($"            writer.WriteInt({member.Id});");
            sb.AppendLine("            if (writer.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
            sb.AppendLine("                writer.WritePropertyName(LuminPackConstUtf8.ValueU8);");
            sb.AppendLine("            else");
            sb.AppendLine("                writer.WritePropertyName(LuminPackConstUtf8.ValueU16);");
            sb.AppendLine($"            writer.WriteValue(ref LuminPackMarshal.As<{classGlobalName}, {memberType}>(ref value)!);");
            sb.AppendLine("            writer.WriteObjectEnd();");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
    
    static void GenerateStaticJsonDeserializeMethods(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        if (!data.UnionMembers.Any())
            return;
        
        foreach (var member in data.UnionMembers)
        {
            string memberType = GetMemberType(data, member);
            string methodName = GetMethodName(member.Type);
            
            sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
            sb.AppendLine("        [global::System.Runtime.CompilerServices.MethodImpl(MethodImplOptions.AggressiveInlining)]");
            sb.AppendLine($"        public static void ReadJson{methodName}(ref global::LuminPack.Core.LuminPackJsonReader reader, ref {classGlobalName} value)");
            sb.AppendLine("        {");
            sb.AppendLine($"            {memberType} tempValue = default!;");
            sb.AppendLine($"            reader.ReadValue(ref tempValue!);");
            sb.AppendLine($"            value = LuminPackMarshal.As<{memberType}, {classGlobalName}>(ref tempValue!);");
            sb.AppendLine("        }");
            sb.AppendLine();
        }
    }
    
    public static void GenerateSerializeJsonCode(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = TypeMetaChecker.BuildParserClassName(data);
        string genericParameters = data.GenericParameters.Count == 0 ? string.Empty : "<" + string.Join(", ", data.GenericParameters) + ">";
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNull();");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        
        sb.AppendLine();
        sb.AppendLine("            var valueMT = LuminPackMarshal.GetMethodTable(value);");
        sb.AppendLine($"            ref var entry = ref global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}._unionMap.TryGetValueRef(valueMT);");
        sb.AppendLine("            if (global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref entry))");
        sb.AppendLine("            {");
        sb.AppendLine($"                LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            switch (entry.Tag)");
        sb.AppendLine("            {");
        foreach (var member in data.UnionMembers)
        {
            string methodName = GetMethodName(member.Type);
            sb.AppendLine($"                case {member.Id}: global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}.WriteJson{methodName}(ref writer, ref Unsafe.AsRef(in value!)); break;");
        }
        sb.AppendLine("                default:");
        sb.AppendLine("                {");
        sb.AppendLine($"                    ref var writeEntry = ref global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}._externalWriteMap.TryGetValueRef(valueMT);");
        sb.AppendLine("                    if (!global::System.Runtime.CompilerServices.Unsafe.IsNullRef(ref writeEntry))");
        sb.AppendLine("                        writeEntry.WriteJsonDelegate(ref writer, ref Unsafe.AsRef(in value!));");
        sb.AppendLine("                    else");
        sb.AppendLine($"                        LuminPackExceptionHelper.ThrowNotFoundInUnionType(value.GetType(), typeof({classGlobalName}));");
        sb.AppendLine("                    break;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }
    
    public static void GenerateDeserializeJsonCode(LuminDataInfo data, StringBuilder sb, string classGlobalName)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = TypeMetaChecker.BuildParserClassName(data);
        string genericParameters = data.GenericParameters.Count == 0 ? string.Empty : "<" + string.Join(", ", data.GenericParameters) + ">";
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine();
        sb.AppendLine("            ushort tag = 0;");
        sb.AppendLine("            bool foundType = false;");
        sb.AppendLine("            bool foundValue = false;");
        sb.AppendLine();
        
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("                    continue;");
        sb.AppendLine();
        sb.AppendLine("                ReadOnlySpan<byte> name;");
        sb.AppendLine("                if (reader.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    name = reader.ReadStringUtf8();");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    name = MemoryMarshal.Cast<char, byte>(reader.ReadStringUtf16());");
        sb.AppendLine("                }");
        sb.AppendLine();
        sb.AppendLine("                if (!reader.Read())");
        sb.AppendLine("                    break;");
        sb.AppendLine();
        
        sb.AppendLine("                if (reader.Option.StringEncoding == global::LuminPack.Option.LuminPackStringEncoding.UTF8)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (name.SequenceEqual(LuminPackConstUtf8.TypeU8))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tag = (ushort)reader.ReadInt();");
        sb.AppendLine("                        foundType = true;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else if (name.SequenceEqual(LuminPackConstUtf8.ValueU8))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (foundType)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            switch (tag)");
        sb.AppendLine("                            {");
        foreach (var member in data.UnionMembers)
        {
            string methodName = GetMethodName(member.Type);
            sb.AppendLine($"                                case {member.Id}: global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}.ReadJson{methodName}(ref reader, ref value!); break;");
        }
        sb.AppendLine("                                default:");
        sb.AppendLine($"                                    if (global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}._externalMap.TryGetValue((nint)(uint)tag, out var extEntry))");
        sb.AppendLine("                                        extEntry.ReadJsonDelegate(ref reader, ref value!);");
        sb.AppendLine("                                    else");
        sb.AppendLine($"                                        LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof({classGlobalName}));");
        sb.AppendLine("                                    break;");
        sb.AppendLine("                            }");
        sb.AppendLine("                            foundValue = true;");
        sb.AppendLine("                            break;");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine("                        reader.Skip();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (name.SequenceEqual(MemoryMarshal.Cast<char, byte>(LuminPackConstUtf8.TypeU16)))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        tag = (ushort)reader.ReadInt();");
        sb.AppendLine("                        foundType = true;");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else if (name.SequenceEqual(MemoryMarshal.Cast<char, byte>(LuminPackConstUtf8.ValueU16)))");
        sb.AppendLine("                    {");
        sb.AppendLine("                        if (foundType)");
        sb.AppendLine("                        {");
        sb.AppendLine("                            switch (tag)");
        sb.AppendLine("                            {");
        foreach (var member in data.UnionMembers)
        {
            string methodName = GetMethodName(member.Type);
            sb.AppendLine($"                                case {member.Id}: global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}.ReadJson{methodName}(ref reader, ref value!); break;");
        }
        sb.AppendLine("                                default:");
        sb.AppendLine($"                                    if (global::{LuminPackSourceGenerator.LUMIN_GENERATED_NAMESPACE}.{classFullName}{genericParameters}._externalMap.TryGetValue((nint)(uint)tag, out var extEntry2))");
        sb.AppendLine("                                        extEntry2.ReadJsonDelegate(ref reader, ref value!);");
        sb.AppendLine("                                    else");
        sb.AppendLine($"                                        LuminPackExceptionHelper.ThrowNotFoundInUnionType(tag, typeof({classGlobalName}));");
        sb.AppendLine("                                    break;");
        sb.AppendLine("                            }");
        sb.AppendLine("                            foundValue = true;");
        sb.AppendLine("                            break;");
        sb.AppendLine("                        }");
        sb.AppendLine("                    }");
        sb.AppendLine("                    else");
        sb.AppendLine("                    {");
        sb.AppendLine("                        reader.Skip();");
        sb.AppendLine("                    }");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
    }
}