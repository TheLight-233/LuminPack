using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class ReadOnlyDictionaryFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                var key = item.Key;");
        sb.AppendLine("                writer.WriteValue(key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }

    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine("            var dictionary = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">(length);");
        sb.AppendLine("            for (int i = 0; i < length; i++)");
        sb.AppendLine("            {");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                reader.ReadValue(ref key);");
        sb.AppendLine("                reader.ReadValue(ref itemValue);");
        sb.AppendLine("                dictionary.Add(key!, itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = new global::System.Collections.ObjectModel.ReadOnlyDictionary<" + keyType + ", " + valueType + ">(dictionary);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb) =>
        DictionaryFormatter.GenerateJsonDictionarySerializeCode(fieldData, sb);

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        string keyType = GetFirstGeneric(fieldData.TypeName);
        string valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var dictionary = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">();");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayStart)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                {");
        sb.AppendLine("                    global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref key);");
        sb.AppendLine("                    if (reader.Read() && reader.CurrentTokenType != global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref itemValue);");
        sb.AppendLine("                }");
        sb.AppendLine("                while (reader.Read())");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                        break;");
        sb.AppendLine("                }");
        sb.AppendLine("                dictionary.Add(key!, itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = new global::System.Collections.ObjectModel.ReadOnlyDictionary<" + keyType + ", " + valueType + ">(dictionary);");
    }
}
