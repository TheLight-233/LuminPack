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
        sb.AppendLine("            var item = value.Value;");
        sb.AppendLine("            writer.WriteValue(in item);");
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
        sb.AppendLine("            reader.ReadValue(ref item);");
        sb.AppendLine("            value = new global::System.Lazy<" + elementType + ">(item);");
    }
}
