using System.Text;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class ArrayFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        
        sb.AppendLine($@"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{fieldData.TypeName.TrimEnd(']').TrimEnd('[')}>())
            {{
                writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);
                writer.Advance(offset);
                return;
            }}

            if (value is null)
            {{
                writer.WriteNullCollectionHeader(ref index);
                writer.Advance(4);
                return;
            }}
            
            writer.WriteCollectionHeader(ref index, value.Length);
            writer.Advance(4);
            
            foreach (ref var item in value.AsSpan())
            {{
                writer.WriteValue(item);
            }}");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine($@"            if (!RuntimeHelpers.IsReferenceOrContainsReferences<{fieldData.TypeName.TrimEnd(']').TrimEnd('[')}>())");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                reader.Advance(offset);");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                value = null;");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                reader.Advance(4);");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine(@"");
        sb.AppendLine(@"            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine(@"            if (length is 0)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = Array.Empty<{fieldData.TypeName.TrimEnd(']').TrimEnd('[')}>();");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            if (value is null || value.Length != length)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = LuminPackMarshal.AllocateUninitializedArray<{fieldData.TypeName.TrimEnd(']').TrimEnd('[')}>(length);");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            var span = value.AsSpan();");
        sb.AppendLine(@"            for (int i = 0; i < length; i++)");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                reader.ReadValue(ref span[i]!);");
        sb.AppendLine(@"            }");
    }
    
}
