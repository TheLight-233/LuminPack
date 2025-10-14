using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class TypeFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            var full = value?.AssemblyQualifiedName;");
        sb.AppendLine();
        sb.AppendLine("            if (full is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullStringHeader(ref index, out var offset);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(offset);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var shortName = global::LuminPack.Parsers.TypeParser.ShortTypeNameRegex().Replace(full, \"\");");
        sb.AppendLine();
        sb.AppendLine("            var length = writer.GetStringLength(shortName);");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteString(shortName, length);");
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
        sb.AppendLine("            if (reader.Option.StringRecording is LuminPackStringRecording.Length)");
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var typeName = reader.ReadString(index, length) ?? string.Empty;");
        sb.AppendLine();
        sb.AppendLine("            if (typeName == string.Empty)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            value = Type.GetType(typeName, throwOnError: true);");
        sb.AppendLine();
        sb.AppendLine("            var symbol = reader.Option.StringRecording is LuminPackStringRecording.Token ? 1 : 0;");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length + symbol);");
    }
}