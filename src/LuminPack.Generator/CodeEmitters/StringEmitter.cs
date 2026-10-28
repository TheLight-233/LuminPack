using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Provides string emitter functionality.</summary>
public static class StringEmitter
{
    /// <summary>Appends binary writing statements for string values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        AppendSerializeCode(sb, "value", "writer.StringRecordLength()", "            ");
    }

    /// <summary>Shares the string formatter body with collection loops that can prove a stable recording mode.</summary>
    /// <param name="sb">The generated source builder.</param>
    /// <param name="value">The already evaluated string expression.</param>
    /// <param name="recordLength">The original header expression or a proven stable cached value.</param>
    /// <param name="indent">Statement indentation.</param>
    internal static void AppendSerializeCode(StringBuilder sb, string value, string recordLength, string indent)
    {
        sb.AppendLine(indent + "writer.WriteStringRecord(" + value + ");");
    }

    /// <summary>Appends binary reading statements for string values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            reader.ReadStringRecord(ref value);");
    }

    /// <summary>Appends statements that calculate the binary payload size.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateCalculateOffsetCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            evaluator += evaluator.GetStringLength(ref value);");
    }

    /// <summary>Appends JSON writing statements for string values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            writer.WriteString(value);");
    }

    /// <summary>Appends JSON reading statements for string values.</summary>
    /// <param name="data">The metadata describing the serializable type.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData data, StringBuilder sb)
    {
        sb.AppendLine("            if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.Null)");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.String)");
        sb.AppendLine("                global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(\"Expected string token\");");
        sb.AppendLine();
        sb.AppendLine("            value = reader.ReadString();");
    }
}
