using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class UriFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            var length = writer.GetStringLength(value?.OriginalString);");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteString(value?.OriginalString, length);");
        sb.AppendLine();
        sb.AppendLine("            var symbol = writer.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(length + symbol);");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadStringLength(ref index, out var length);");
        sb.AppendLine();
        sb.AppendLine("            var str = reader.ReadString(length);");
        sb.AppendLine();
        sb.AppendLine("            var symbol = reader.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length + symbol);");
        sb.AppendLine();
        sb.AppendLine("            if (str is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = new Uri(str, UriKind.RelativeOrAbsolute);");
        sb.AppendLine("            }");
    }
}