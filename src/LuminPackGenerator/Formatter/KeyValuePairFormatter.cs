using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class KeyValuePairFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            writer.WriteValue(value.Key);");
        sb.AppendLine("            writer.WriteValue(value.Value);");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            " + keyType + " key = default!;");
        sb.AppendLine("            " + valueType + " itemValue = default!;");
        sb.AppendLine("            reader.ReadValue(ref key);");
        sb.AppendLine("            reader.ReadValue(ref itemValue);");
        sb.AppendLine("            value = new global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">(key, itemValue);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            var key = value.Key;");
        sb.AppendLine("            global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in key);");
        sb.AppendLine("            writer.WriteByteRaw((byte)',');");
        sb.AppendLine("            var itemValue = value.Value;");
        sb.AppendLine("            global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in itemValue);");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            " + keyType + " key = default!;");
        sb.AppendLine("            " + valueType + " itemValue = default!;");
        sb.AppendLine("            global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref key);");
        sb.AppendLine("            global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref itemValue);");
        sb.AppendLine("            value = new global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">(key, itemValue);");
    }
}
