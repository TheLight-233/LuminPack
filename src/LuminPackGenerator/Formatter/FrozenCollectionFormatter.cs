using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.Formatter;

using static FormatterDiscovery;

public static class FrozenDictionaryFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
		var keyType = GetFirstGeneric(fieldData.TypeName);
		var valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var count = value.Count;");
        sb.AppendLine("            var i = 0;");
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
        sb.AppendLine("                i++;");
		sb.AppendLine("                var key = item.Key;");
		sb.AppendLine("                writer.WriteValue(key);");
		sb.AppendLine("                var itemValue = item.Value;");
		sb.AppendLine("                writer.WriteValue(itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine();
        sb.AppendLine("            if (i != count)");
        sb.AppendLine("                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
		var keyType = GetFirstGeneric(fieldData.TypeName);
		var valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var dict = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">(length);");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
		sb.AppendLine("                " + keyType + " k = default!;");
		sb.AppendLine("                " + valueType + " v = default!;");
		sb.AppendLine("                reader.ReadValue(ref k);");
		sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                dict.Add(k!, v);");
        sb.AppendLine("            }");
		sb.AppendLine("            value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    writer.WriteArrayStart();");
        sb.AppendLine("                    var key = item.Key;");
        sb.AppendLine("                    global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in key);");
        sb.AppendLine("                    var itemValue = item.Value;");
        sb.AppendLine("                    global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in itemValue);");
        sb.AppendLine("                    writer.WriteArrayEnd();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var keyType = GetFirstGeneric(fieldData.TypeName);
        var valueType = GetSecondGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var dict = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">();");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref key);");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref itemValue);");
        sb.AppendLine("                dict.Add(key!, itemValue);");
        sb.AppendLine("            }");
		sb.AppendLine("            value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);");
    }
}

public static class FrozenSetFormatter
{
    public static void GenerateSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
		var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref writer.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (value is null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNullCollectionHeader(ref index);");
        sb.AppendLine();
        sb.AppendLine("                writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, value.Count);");
        sb.AppendLine();
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            foreach (var item in value)");
        sb.AppendLine("            {");
		sb.AppendLine("                " + elementType + " v = item;");
		sb.AppendLine("                writer.WriteValue(v);");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.CheckBuffer();");
    }
    
    public static void GenerateDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
		var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            ref var index = ref reader.GetCurrentSpanOffset();");
        sb.AppendLine();
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length))");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine();
        sb.AppendLine("                reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            reader.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var set = new global::System.Collections.Generic.HashSet<" + elementType + ">(length);");
        sb.AppendLine();
        sb.AppendLine("            for (var i = 0; i < length; i++)");
        sb.AppendLine("            {");
		sb.AppendLine("                " + elementType + " v = default!;");
		sb.AppendLine("                reader.ReadValue(ref v);");
        sb.AppendLine("                set.Add(v);");
        sb.AppendLine("            }");
        sb.AppendLine();
		sb.AppendLine("            value = global::System.Collections.Frozen.FrozenSet.ToFrozenSet(set);");
    }

    public static void GenerateJsonSerializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        sb.AppendLine("            if (value == null)");
        sb.AppendLine("            {");
        sb.AppendLine("                writer.WriteNull();");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayStart();");
        sb.AppendLine("            if (value.Count > 0)");
        sb.AppendLine("            {");
        sb.AppendLine("                bool isFirst = true;");
        sb.AppendLine("                foreach (var item in value)");
        sb.AppendLine("                {");
        sb.AppendLine("                    if (!isFirst) writer.WriteByteRaw((byte)',');");
        sb.AppendLine("                    else isFirst = false;");
        sb.AppendLine("                    writer.SetFirstElement(true);");
        sb.AppendLine("                    var temp = item;");
        sb.AppendLine("                    global::LuminPack.Generated.LuminPackExtensions.WriteValue(ref writer, in temp);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    public static void GenerateJsonDeserializeCode(LuminLocalFieldData fieldData, StringBuilder sb)
    {
        var elementType = GetFirstGeneric(fieldData.TypeName);
        sb.AppendLine("            if (reader.IsNull())");
        sb.AppendLine("            {");
        sb.AppendLine("                value = null;");
        sb.AppendLine("                return;");
        sb.AppendLine("            }");
        sb.AppendLine("            var set = new global::System.Collections.Generic.HashSet<" + elementType + ">();");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ObjectEnd)");
        sb.AppendLine("                    continue;");
        sb.AppendLine("                " + elementType + " item = default!;");
        sb.AppendLine("                global::LuminPack.Generated.LuminPackExtensions.ReadValue(ref reader, ref item);");
        sb.AppendLine("                set.Add(item);");
        sb.AppendLine("            }");
		sb.AppendLine("            value = global::System.Collections.Frozen.FrozenSet.ToFrozenSet(set);");
    }
}
