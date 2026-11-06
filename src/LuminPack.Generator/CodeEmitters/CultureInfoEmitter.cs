using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides culture info emitter functionality.</summary>
public static class CultureInfoEmitter
{
    /// <summary>Appends binary writing statements for culture info values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullObjectHeader();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteObjectHeader(0);");
        sb.AppendLine("            ");
        sb.AppendLine("            writer.WriteStringRecord(value.Name);");
        sb.AppendLine("            ");
        sb.AppendLine("            ");
    }

    /// <summary>Appends binary reading statements for culture info values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (!reader.TryReadObjectHead())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            ");
        string hint = fieldData.CanReuseCultureInfo ? "global::LuminPack.Code.LuminPackCultureInfoCache.Name" : "null";
        sb.AppendLine("            var name = value?.GetType() == typeof(global::System.Globalization.CultureInfo) && value.IsReadOnly ? value.Name : " + hint + ";");
        sb.AppendLine("            var str = reader.ReadStringRecord(name);");
        sb.AppendLine("            ");
        AppendResolution(fieldData, sb);
    }

    /// <summary>Appends JSON writing statements for culture info values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
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

    /// <summary>Appends JSON reading statements for culture info values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        if (fieldData.CanReuseCultureInfo)
        {
            sb.AppendLine("            string str = global::LuminPack.Code.LuminPackCultureInfoCache.Name!;");
            sb.AppendLine("            if (str is not null && reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String) reader.Read(ref str);");
            sb.AppendLine("            else str = reader.ReadString();");
        }
        else
            sb.AppendLine("            var str = reader.ReadString();");
        AppendResolution(fieldData, sb);
    }

    /// <summary>Resolves the decoded name with the cache available in the referenced runtime.</summary>
    /// <param name="fieldData">The framework type and available runtime capabilities.</param>
    /// <param name="sb">The generated reader body.</param>
    private static void AppendResolution(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string lookup = fieldData.CanReuseCultureInfo ? "global::LuminPack.Code.LuminPackCultureInfoCache.ResolveCulture" : "global::System.Globalization.CultureInfo.GetCultureInfo";
        sb.AppendLine("            value = " + lookup + "(str ?? string.Empty);");
    }
}
