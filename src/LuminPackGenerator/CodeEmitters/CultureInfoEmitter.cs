using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

public static class CultureInfoEmitter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader();");
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
        sb.AppendLine("            var str = reader.ReadStringAndAdvance(ref index);");
        sb.AppendLine("            ");
        sb.AppendLine("            if (string.IsNullOrEmpty(str))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                if (global::System.Object.ReferenceEquals(global::System.Globalization.CultureInfo.CurrentCulture, global::System.Globalization.CultureInfo.InvariantCulture) &&");
        sb.AppendLine("                    global::System.Object.ReferenceEquals(global::System.Globalization.CultureInfo.CurrentUICulture, global::System.Globalization.CultureInfo.InvariantCulture))");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = str == global::System.Globalization.CultureInfo.InvariantCulture.Name ? ");
        sb.AppendLine("                        global::System.Globalization.CultureInfo.InvariantCulture : null;");
        sb.AppendLine("                }");
        sb.AppendLine("                else");
        sb.AppendLine("                {");
        sb.AppendLine("                    value = global::System.Globalization.CultureInfo.GetCultureInfo(str);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteString(value.Name);");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var str = reader.ReadString();");
        sb.AppendLine();
        sb.AppendLine("            if (global::System.Object.ReferenceEquals(global::System.Globalization.CultureInfo.CurrentCulture, global::System.Globalization.CultureInfo.InvariantCulture) &&");
        sb.AppendLine("                global::System.Object.ReferenceEquals(global::System.Globalization.CultureInfo.CurrentUICulture, global::System.Globalization.CultureInfo.InvariantCulture))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = str == global::System.Globalization.CultureInfo.InvariantCulture.Name ? global::System.Globalization.CultureInfo.InvariantCulture : null;");
        sb.AppendLine("            }");
        sb.AppendLine("            else");
        sb.AppendLine("            {");
        sb.AppendLine("                value = global::System.Globalization.CultureInfo.GetCultureInfo(str);");
        sb.AppendLine("            }");
    }
}
