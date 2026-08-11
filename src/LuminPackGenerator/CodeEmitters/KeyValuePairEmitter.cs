using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

public static class KeyValuePairEmitter
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

    public static void GenerateCalculateOffsetCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            " + keyType + " key = value.Key;");
        sb.AppendLine("            evaluator.CalculateOffset(ref key);");
        sb.AppendLine("            " + valueType + " itemValue = value.Value;");
        sb.AppendLine("            evaluator.CalculateOffset(ref itemValue);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            var key = value.Key;");
        sb.AppendLine("            writer.WriteValue(in key);");
        sb.AppendLine("            writer.WriteByteRaw((byte)',');");
        sb.AppendLine("            var itemValue = value.Value;");
        sb.AppendLine("            writer.WriteValue(in itemValue);");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            " + keyType + " key = default!;");
        sb.AppendLine("            " + valueType + " itemValue = default!;");
        sb.AppendLine("            reader.ReadValue(ref key);");
        sb.AppendLine("            reader.ReadValue(ref itemValue);");
        sb.AppendLine("            value = new global::System.Collections.Generic.KeyValuePair<" + keyType + ", " + valueType + ">(key, itemValue);");
    }
}
