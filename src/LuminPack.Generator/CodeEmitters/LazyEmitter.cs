using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides lazy emitter functionality.</summary>
public static class LazyEmitter
{
    /// <summary>Appends binary writing statements for lazy values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("                writer.WriteNullObjectHeader(ref index);");
        sb.AppendLine("                writer.Advance(1);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteObjectHeader(0);");
        sb.AppendLine("            var item = value.Value;");
        sb.AppendLine("            writer.WriteValue(in item);");
    }

    /// <summary>Appends binary reading statements for lazy values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!reader.TryReadObjectHead())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            " + elementType + " item = default!;");
        sb.AppendLine("            reader.ReadValue(ref item);");
        sb.AppendLine("            value = new global::System.Lazy<" + elementType + ">(item);");
    }

    /// <summary>Appends JSON writing statements for lazy values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteObjectStart();");
        sb.AppendLine("            writer.WritePropertyName(\"Value\");");
        sb.AppendLine("            var item = value.Value;");
        sb.AppendLine("            writer.WriteValue(in item);");
        sb.AppendLine("            writer.WriteObjectEnd();");
    }

    /// <summary>Appends JSON reading statements for lazy values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            " + elementType + " item = default!;");
        sb.AppendLine("            reader.TryConsumeObjectStart();");
        sb.AppendLine("            bool hasValue = false;");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd) break;");
        JsonPropertyNameEmitter.Append(sb, "Value");
        sb.AppendLine("                reader.Read();");
        sb.AppendLine("                if (property == 1) { reader.ReadValue(ref item); hasValue = true; }");
        sb.AppendLine("                else reader.Skip();");
        sb.AppendLine("            }");
        sb.AppendLine("            if (!hasValue) global::LuminPack.Code.LuminPackExceptionHelper.ThrowFormatException(\"A Lazy JSON object requires a Value property.\");");
        sb.AppendLine("            value = new global::System.Lazy<" + elementType + ">(item);");
    }
}
