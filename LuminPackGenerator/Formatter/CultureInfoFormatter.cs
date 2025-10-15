using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class CultureInfoFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullStringHeader(ref index, out var offset);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(offset);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var length = writer.GetStringLength(value.Name);");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteString(value.Name, length);");
        sb.AppendLine();
        sb.AppendLine("            var symbol = writer.Option.StringRecording is LuminPack.Option.LuminPackStringRecording.Token ? 1 : 4;");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(length + symbol);");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadStringLength(ref index, out var length);");
        sb.AppendLine();
        sb.AppendLine("            if (reader.Option.StringRecording is LuminPack.Option.LuminPackStringRecording.Length)");
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var str = reader.ReadString(index, length) ?? string.Empty;");
        sb.AppendLine("            if (str == string.Empty)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = global::System.Globalization.CultureInfo.GetCultureInfo(str);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var symbol = reader.Option.StringRecording is LuminPack.Option.LuminPackStringRecording.Token ? 1 : 0;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length + symbol);");
    }
}