using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

public static class StringFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            int offset = writer.WriteString(value) + writer.StringRecordLength();");
        sb.AppendLine("            writer.Advance(offset);");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            reader.ReadStringLength(ref index, out var length);");
        sb.AppendLine();
        sb.AppendLine("            value  = reader.ReadString(length);");
        sb.AppendLine();
        sb.AppendLine("            var symbol = reader.StringRecordLength();");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(length + symbol);");
    }
}