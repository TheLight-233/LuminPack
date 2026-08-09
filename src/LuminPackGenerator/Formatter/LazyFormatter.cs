using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class LazyFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
		sb.AppendLine("            var item = value.Value;");
		sb.AppendLine("            writer.WriteValue(in item);");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!reader.TryReadObjectHead(ref index))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                reader.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            " + elementType + " item = default!;");
        sb.AppendLine("            reader.ReadValue(ref item);");
        sb.AppendLine("            value = new global::System.Lazy<" + elementType + ">(item);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var item = value.Value;");
        sb.AppendLine("            writer.WriteValue(in item);");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            " + elementType + " item = default!;");
        sb.AppendLine("            reader.ReadValue(ref item);");
        sb.AppendLine("            value = new global::System.Lazy<" + elementType + ">(item);");
    }
}
