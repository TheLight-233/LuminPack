using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class NullableFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.DangerousWriteUnmanaged(ref index, value);");
        sb.AppendLine();
        sb.AppendLine("                index += Unsafe.SizeOf<T>();");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (!value.HasValue)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine("                index += 1;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteValue(value.Value)");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.DangerousReadUnmanaged(ref index, out value);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = reader.ReadValue<T>();");
    }
}