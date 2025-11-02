using System.Text;
using System.Text.RegularExpressions;
using LuminPack.Code;


namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class ArrayFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        
        if (KnownValueTypes.Contains(baseTypeName))
        {
            sb.AppendLine("            writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);");
            sb.AppendLine("            writer.Advance(offset);");
            return;
        }

        if (!baseTypeName.Contains("?"))
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.DangerousWriteUnmanagedArray(ref index, value, out var offset);");
            sb.AppendLine("                writer.Advance(offset);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        else
        {
            sb.AppendLine("            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + baseTypeName + ">())");
            sb.AppendLine("            {");
            sb.AppendLine("                writer.WriteArray(value);");
            sb.AppendLine("                return;");
            sb.AppendLine("            }");
        }
        
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Length);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (ref var item in value.AsSpan())");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteValue(item);");
        sb.AppendLine("            }");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string baseTypeName = Regex.Replace(fieldData.TypeName, @"\[\s*\]\s*$", "");
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();

        if (KnownValueTypes.Contains(baseTypeName))
        {
            sb.AppendLine(@"            reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
            sb.AppendLine(@"            reader.Advance(offset);");
            return;
        }

        if (!baseTypeName.Contains("?"))
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.DangerousReadUnmanagedArray(ref index, ref value!, out var offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                reader.Advance(offset);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine($@"            if (!global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<{baseTypeName}>())");
            sb.AppendLine(@"            {");
            sb.AppendLine(@"                reader.ReadArray(ref value!);");
            sb.AppendLine(@"                ");
            sb.AppendLine(@"                return;");
            sb.AppendLine(@"            }");
            sb.AppendLine();
        }
        
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
        sb.AppendLine($@"                value = Array.Empty<{baseTypeName}>();");
        sb.AppendLine(@"                ");
        sb.AppendLine(@"                return;");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            if (value is null || value.Length != length)");
        sb.AppendLine(@"            {");
        sb.AppendLine($@"                value = LuminPackMarshal.AllocateUninitializedArray<{baseTypeName}>(length);");
        sb.AppendLine(@"            }");
        sb.AppendLine();
        sb.AppendLine(@"            ref var first = ref LuminPackMarshal.GetArrayReference(value);");
        sb.AppendLine(@"            for (nint i = 0; i < length; i++)");
        sb.AppendLine(@"            {");
        sb.AppendLine(@"                reader.ReadValue(ref global::System.Runtime.CompilerServices.Unsafe.Add(ref first, i)!);");
        sb.AppendLine(@"            }");
    }
    
}
