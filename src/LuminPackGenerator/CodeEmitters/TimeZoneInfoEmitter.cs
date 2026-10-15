using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides time zone info emitter functionality.</summary>
public static class TimeZoneInfoEmitter
{
    /// <summary>Appends binary writing statements for time zone info values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            int offset = writer.WriteString(value.ToSerializedString()) + writer.StringRecordLength();");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.Advance(offset);");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    /// <summary>Appends binary reading statements for time zone info values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
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
        sb.AppendLine("            var source = reader.ReadStringAndAdvance(ref index);");
        sb.AppendLine("            ");
        sb.AppendLine("            if (string.IsNullOrEmpty(source))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            value = global::System.TimeZoneInfo.FromSerializedString(source);");
    }

    /// <summary>Appends JSON writing statements for time zone info values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteString(value.ToSerializedString());");
    }

    /// <summary>Appends JSON reading statements for time zone info values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            var source = reader.ReadString();");
        sb.AppendLine("            value = global::System.TimeZoneInfo.FromSerializedString(source);");
    }
}
