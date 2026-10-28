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
        sb.AppendLine("            string? text = value is null ? null : global::LuminPack.Code.LuminPackTimeZoneInfoCache.GetText(value);");
        sb.AppendLine("            writer.WriteTimeZoneText(text);");
    }

    /// <summary>Appends binary reading statements for time zone info values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (!reader.TryReadObjectHead())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            var candidate = reader.GetTimeZoneCandidate();");
        sb.AppendLine("            if (reader.TryReadTimeZoneInfo(candidate, out var cached)) { value = cached!; return; }");
        sb.AppendLine("            var source = reader.ReadStringRecord(candidate?.Text);");
        sb.AppendLine("            ");
        sb.AppendLine("            if (string.IsNullOrEmpty(source))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        sb.AppendLine("            value = global::LuminPack.Code.LuminPackTimeZoneInfoCache.Parse(source, candidate);");
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
        sb.AppendLine("            writer.WriteString(global::LuminPack.Code.LuminPackTimeZoneInfoCache.GetText(value));");
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
        sb.AppendLine("            value = global::LuminPack.Code.LuminPackTimeZoneInfoCache.Parse(source);");
    }
}
