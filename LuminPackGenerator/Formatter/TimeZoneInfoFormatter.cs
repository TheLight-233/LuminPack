using System.Text;

namespace LuminPack.SourceGenerator.Formatter;

public static class TimeZoneInfoFormatter
{
    public static void GenerateSerializeCode(StringBuilder sb)
    {
        sb.AppendLine("        var length = writer.GetStringLength(value?.ToSerializedString());");
        sb.AppendLine();
        sb.AppendLine("        writer.WriteString(value?.ToSerializedString(), length);");
        sb.AppendLine();
        sb.AppendLine("        var symbol = writer.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 4;");
        sb.AppendLine();
        sb.AppendLine("        writer.Advance(length + symbol);");
    }
    
    public static void GenerateDeserializeCode(StringBuilder sb)
    {
        sb.AppendLine("        ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("        reader.ReadStringLength(ref index, out var length);");
        sb.AppendLine();
        sb.AppendLine("        if (reader.Option.StringRecording is LuminPackStringRecording.Length)");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("        var source = reader.ReadString(length) ?? string.Empty;");
        sb.AppendLine();
        sb.AppendLine("        if (source == string.Empty)");
        sb.AppendLine("        {");
        sb.AppendLine("            value = null;");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        value = TimeZoneInfo.FromSerializedString(source);");
        sb.AppendLine();
        sb.AppendLine("        var symbol = reader.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 0;");
        sb.AppendLine();
        sb.AppendLine("        reader.Advance(length + symbol);");
    }
}