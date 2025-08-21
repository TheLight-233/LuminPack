using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

public static class LuminPackCircleReferenceCodeGenerator
{
    const int MaxParametersPerCall = 15;
    
    public static void CircleReferenceCodeGenerator(StringBuilder sb, LuminDataInfo data, MetaInfo metaInfo)
    {
        string paraNullable = data.isValueType ? string.Empty : "?";
        string classFullName = data.className + "Parser";
        string classGlobalName = data.classFullName;
        string parserName = data.className + "Parser";
        bool isAllUnmanagedType = LuminPackCodeGenerator.FindAllUnmanagedType(data.fields);
        uint memberCount = data.fields.Max(x => x.Order) + 1; //Start From Zero
        
        if (data.isGeneric)
        {
            classFullName += $"<{data.GenericParameters.FirstOrDefault()}";
            for(var i = 1; i < data.GenericParameters.Count; i++)
            {
                classFullName += "," + data.GenericParameters[i];
            }
            classFullName += ">";
        }
        
        if (!classGlobalName.Contains(".") && data.classNameSpace != "<global namespace>")
        {
            classGlobalName = "global::" + data.classNameSpace + "." + data.classFullName;
        }
        
        sb.AppendLine($"        static {parserName}()");
        sb.AppendLine("        {");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new {classFullName}());");
        sb.AppendLine($"            LuminPackParseProvider.RegisterParsers(new ArrayParser<{classGlobalName}>());");
        sb.AppendLine("        }");
        sb.AppendLine();
        
        // Serialize实现
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8 
            ? $"        public override void Serialize(ref LuminPackWriter writer, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void Serialize(ref LuminPackWriter writer, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteNullObjectHeader();");
            sb.AppendLine("                writer.Advance(1);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        
        sb.AppendLine();
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, Local{data.classFullName}>(ref value);");
        sb.AppendLine("            var (existsReference, id) = writer.OptionState.GetOrAddReference(local);");
        sb.AppendLine("            if (existsReference)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteObjectReferenceId(ref index, id);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (isAllUnmanagedType)
        {
            sb.AppendLine($"            writer.WriteObjectHeader(ref index, {memberCount});");
            sb.AppendLine($"            writer.Advance(1);");
            for (int order = 0; order < memberCount; order++)
            {
                var field = data.fields.FirstOrDefault(f => f.Order == order);
                if (field is null)
                {
                    sb.AppendLine("            writer.WriteVarInt(0); writer.Advance(LuminPackEvaluator.CalculateVarInt(0));");
                }
                else
                {
                    sb.AppendLine($"            writer.WriteVarInt(System.Runtime.CompilerServices.Unsafe.SizeOf<{field.TypeName}>());");
                    sb.AppendLine($"            writer.Advance(LuminPackEvaluator.CalculateVarInt(System.Runtime.CompilerServices.Unsafe.SizeOf<{field.TypeName}>()));");
                }
                sb.AppendLine();
            }
            sb.AppendLine($"            writer.WriteVarInt(id);");
            sb.AppendLine($"            writer.Advance(LuminPackEvaluator.CalculateVarInt(id));");
            if (data.fields.Count <= MaxParametersPerCall)
            {
                // 不超过15个字段，直接单次调用
                sb.Append($"            writer.Advance(writer.WriteUnmanaged(ref index");
                for (var i = 0; i < data.fields.Count; i++)
                {
                    sb.Append($", local.@{data.fields[i].Name}");
                }
                sb.Append("));");
            }
            else
            {
                // 超过15个字段，需要分组多次调用
                int fieldCount = data.fields.Count;
                int processed = 0;
    
                while (processed < fieldCount)
                {
                    int batchSize = Math.Min(MaxParametersPerCall, fieldCount - processed);
        
                    sb.AppendLine();
                    sb.Append($"            writer.Advance(writer.WriteUnmanaged(ref index");
        
                    // 添加当前批次字段
                    for (int i = 0; i < batchSize; i++)
                    {
                        sb.Append($", local.@{data.fields[processed + i].Name}");
                    }
                    sb.Append("));");
        
                    processed += batchSize;
        
                    // 如果不是最后一批，添加换行
                    if (processed < fieldCount)
                    {
                        sb.AppendLine();
                    }
                }
            }

            sb.AppendLine();

        }
        else
        {
            sb.AppendLine("            var writerBuffer = global::LuminPack.Utility.ReusableLinkedArrayBufferWriterPool.Rent();");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
            sb.AppendLine($"                var tempWriter = new LuminPackWriter(writerBuffer, writer.OptionState);");
            sb.AppendLine($"                Span<int> offsets = stackalloc int[{data.fields.Count}];");
            // 处理每个字段（按Order顺序，跳过空字段）
            for (int order = 0; order < memberCount; order++)
            {
                var field = data.fields.FirstOrDefault(f => f.Order == order);
                if (field is not null)
                {
                    if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
                    {
                        sb.AppendLine($"                tempWriter.Advance(tempWriter.WriteUnmanaged(local.@{field.Name}));");
                    }
                    else
                    {
                        sb.AppendLine($"                tempWriter.WriteValue(local.@{field.Name});");
                    }
                    
                }
                else
                {
                    // 空字段处理，不写入数据，只记录当前位置
                    sb.AppendLine("                // Empty field placeholder");
                }
                sb.AppendLine($"                offsets[{order}] = tempWriter.GetCurrentSpanIndex();");
            }
            //sb.AppendLine($"                tempWriter.Flush();");
            sb.AppendLine();
            sb.AppendLine($"                writer.WriteObjectHeader(ref index, {memberCount});");
            sb.AppendLine($"                writer.Advance(1);");
            sb.AppendLine($"                for (int i = 0; i < {memberCount}; i++)");
            sb.AppendLine($"                {{");
            sb.AppendLine($"                    int delta;");
            sb.AppendLine($"                    if (i is 0)");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        delta = offsets[i];");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    else");
            sb.AppendLine($"                    {{");
            sb.AppendLine($"                        delta = offsets[i] - offsets[i - 1];");
            sb.AppendLine($"                    }}");
            sb.AppendLine($"                    writer.WriteVarInt(delta);");
            sb.AppendLine($"                    writer.Advance(LuminPackEvaluator.CalculateVarInt(delta));");
            sb.AppendLine($"                }}");
            sb.AppendLine($"                writer.WriteVarInt(id);");
            sb.AppendLine($"                writer.Advance(LuminPackEvaluator.CalculateVarInt(id));");
            sb.AppendLine($"                writerBuffer.WriteToAndReset(ref writer);");
            sb.AppendLine("            }");
            sb.AppendLine("            finally");
            sb.AppendLine("            {");
            sb.AppendLine("                global::LuminPack.Utility.ReusableLinkedArrayBufferWriterPool.Return(writerBuffer);");
            sb.AppendLine("            }");
        }
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnSerialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        sb.AppendLine("        }");
        
        // Deserialize实现
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8 
            ? $"        public override void Deserialize(ref LuminPackReader reader, scoped ref {data.classFullName}{paraNullable} value)"
            : $"        public override void Deserialize(ref LuminPackReader reader, ref {data.classFullName}{paraNullable} value)");
        sb.AppendLine("        {");
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserializing))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        sb.AppendLine("            ref var offset = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine($"            var local = new Local{data.classFullName}();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadObjectHead(ref offset, out var count))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = default;");
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.Advance(1);");
        sb.AppendLine();
        sb.AppendLine("            uint id;");
        sb.AppendLine("            if (count is LuminPackCode.ReferenceId)");
        sb.AppendLine("            {");
        sb.AppendLine("                id = reader.ReadVarIntUInt32();");
        sb.AppendLine("                reader.Advance(LuminPackEvaluator.CalculateVarInt(id));");
        sb.AppendLine($"                var tempValue = (Local{data.classFullName})reader.OptionState.GetObjectReference(id);");
        sb.AppendLine($"                value = LuminPackMarshal.As<Local{data.classFullName}, {classGlobalName}>(ref tempValue);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            Span<int> deltas = stackalloc int[count];");
        sb.AppendLine("            for (int i = 0; i < count; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                deltas[i] = reader.ReadVarIntInt32();");
        sb.AppendLine("                reader.Advance(LuminPackEvaluator.CalculateVarInt(deltas[i]));");
        sb.AppendLine("            }");
        sb.AppendLine("            id = reader.ReadVarIntUInt32();");
        sb.AppendLine("            reader.Advance(LuminPackEvaluator.CalculateVarInt(id));");
        sb.AppendLine("            reader.OptionState.AddObjectReference(id, local);");
        sb.AppendLine();
        for (var order = 0; order < memberCount; order++)
        {
            var field = data.fields.FirstOrDefault(f => f.Order == order);

            if (field is not null)
            {
                sb.AppendLine($"            if (deltas[{order}] != 0)");
                sb.AppendLine("            {");
                if (LuminPackCodeGenerator.IsUnmanagedFiledType(data.fields[order].Type)) 
                {
                    sb.AppendLine($"                reader.Advance(reader.ReadUnmanaged(ref offset, out local.@{field.Name}));");
                }
                else
                {
                    sb.AppendLine($"                reader.ReadValue(ref local.@{field.Name});");
                }
                sb.AppendLine("            }");
                sb.AppendLine("            else");
                sb.AppendLine("            {");
                sb.AppendLine($"                local.@{field.Name} = default;");
                sb.AppendLine("            }");
            }
            else
            {
                sb.AppendLine($"            offset += deltas[{order}];");
            }
            sb.AppendLine();
        }
        
        sb.AppendLine($"            value = LuminPackMarshal.As<Local{data.classFullName}, {classGlobalName}>(ref local);");
        
        sb.AppendLine();
        foreach (var item in data.callBackMethods.Where(x => x.Item2 is SerializeCallBackType.OnDeserialized))
        {
            sb.AppendLine(item.Item3
                ? $"            {classGlobalName}.{item.Item1}();"
                : $"            value?.{item.Item1}();");
        }
        sb.AppendLine();
        
        sb.AppendLine("        }");
        
        sb.AppendLine();
        
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(metaInfo.IsNet8 
            ? $"        public override void CalculateOffset(ref LuminPackEvaluator evaluator, scoped ref {classGlobalName}{paraNullable} value)"
            : $"        public override void CalculateOffset(ref LuminPackEvaluator evaluator, ref {classGlobalName}{paraNullable} value)");
        sb.AppendLine("        {");
        if (!data.isValueType)
        {
            sb.AppendLine("            if (value is null)");
            sb.AppendLine("            {");
            sb.AppendLine("                evaluator += 1;");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        sb.AppendLine();
        sb.AppendLine("            var (existsReference, id) = evaluator.OptionState.GetOrAddReference(value);");
        sb.AppendLine("            if (existsReference)");
        sb.AppendLine("            {");
        sb.AppendLine("                evaluator += 1;");
        sb.AppendLine("                evaluator += LuminPackEvaluator.CalculateVarInt(id);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            int size = 1;");
        sb.AppendLine("            size += LuminPackEvaluator.CalculateVarInt(id);");
        sb.AppendLine();
        sb.AppendLine($"            ref var local = ref LuminPackMarshal.As<{classGlobalName}, Local{data.classFullName}>(ref value);");
        foreach (var field in data.fields)
        {
            if (LuminPackCodeGenerator.IsUnmanagedFiledType(field.Type))
            {
                sb.AppendLine($"            size += Unsafe.SizeOf<{field.TypeName}>() + LuminPackEvaluator.CalculateVarInt(Unsafe.SizeOf<{field.TypeName}>());");
            }
            else
            {
                sb.AppendLine($"            var {field.Name}TempLength = evaluator.Value;");
                sb.AppendLine($"            evaluator.CalculateValue(local.@{field.Name});");
                sb.AppendLine($"            size += LuminPackEvaluator.CalculateVarInt(evaluator.Value - {field.Name}TempLength);");
            }
            
        }
        sb.AppendLine();
        sb.AppendLine("            evaluator += size;");
        sb.AppendLine("        }");
        
        sb.AppendLine();
        switch (data.structLayout)
        {
            case StructLayout.Explicit : sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Explicit)]"); break;
            case StructLayout.Auto : sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Auto)]"); break;
            case StructLayout.Sequential : sb.AppendLine("        [global::System.Runtime.InteropServices.StructLayout(LayoutKind.Sequential)]"); break;
            case StructLayout.Default : break;
        }
        sb.AppendLine("        [global::LuminPack.Attribute.Preserve]");
        sb.AppendLine(data.isValueType 
            ? $"        private struct Local{data.classFullName}" 
            : $"        private sealed class Local{data.classFullName}");
        sb.AppendLine("        {");
        foreach (var field in data.localFields)
        {
            if (data.structLayout is StructLayout.Explicit)
                sb.AppendLine($"            [global::System.Runtime.InteropServices.FieldOffset({field.filedOffset})]");
            var nullable = LuminPackCodeGenerator.IsUnmanagedFiledType(field.TypeName) || field.IsValue ? string.Empty : "?";
            sb.AppendLine($"            internal {field.TypeName}{nullable} {field.Name};");
        }
            
        sb.AppendLine("        }");
            
        sb.AppendLine();
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }
    
    
}