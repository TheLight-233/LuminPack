using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class TimeZoneInfoFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader();");
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            int offset = writer.WriteString(value.ToSerializedString()) + writer.StringRecordLength();");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.Advance(offset);");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            ");
        sb.AppendLine("            if (reader.PeekIsNullObject(ref index))");
        sb.AppendLine("            {");
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.ReadStringLength(ref index, out var length);");
        sb.AppendLine("            ");
        sb.AppendLine("            var source = reader.ReadString(length);");
        sb.AppendLine("            ");
        sb.AppendLine("            var symbol = reader.StringRecordLength();");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(length + symbol);");
        sb.AppendLine("            ");
        sb.AppendLine("            if (string.IsNullOrEmpty(source))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            value = global::System.TimeZoneInfo.FromSerializedString(source);");
    }
}