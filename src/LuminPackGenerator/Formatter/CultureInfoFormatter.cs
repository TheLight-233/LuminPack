using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class CultureInfoFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader();");
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            int offset = writer.WriteString(value.Name) + writer.StringRecordLength();");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.Advance(offset);");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.CheckBuffer();");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
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
        sb.AppendLine("            var str = reader.ReadString(length);");
        sb.AppendLine("            ");
        sb.AppendLine("            var symbol = reader.StringRecordLength();");
        sb.AppendLine("            ");
        sb.AppendLine("            reader.Advance(length + symbol);");
        sb.AppendLine("            ");
        sb.AppendLine("            if (string.IsNullOrEmpty(str))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                if (global::LuminPack.Parsers.CultureInfoParser.IsInvariantMode)");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = str == global::LuminPack.Parsers.CultureInfoParser.InvariantCultureName ? ");
        sb.AppendLine("                        global::System.Globalization.CultureInfo.InvariantCulture : null;");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = global::System.Globalization.CultureInfo.GetCultureInfo(str);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }
}