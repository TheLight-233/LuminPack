using System.Text;
using LuminPack.Code;

namespace LuminPack.SourceGenerator.CodeEmitters;

using static CodeEmitterRegistry;

/// <summary>Provides frozen dictionary emitter functionality.</summary>
public static class FrozenDictionaryEmitter
{
    /// <summary>Appends binary writing statements for frozen dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
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
        sb.AppendLine("                writer.WriteValue(in key);");
        sb.AppendLine("                var itemValue = item.Value;");
        sb.AppendLine("                writer.WriteValue(in itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine("            writer.CheckBuffer();");
        sb.AppendLine();
        sb.AppendLine("            if (i != count)");
        sb.AppendLine("                LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();");
    }

    /// <summary>Appends binary reading statements for frozen dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
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

    /// <summary>Appends JSON writing statements for frozen dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
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
        sb.AppendLine("                    writer.WriteValue(in key);");
        sb.AppendLine("                    var itemValue = item.Value;");
        sb.AppendLine("                    writer.WriteValue(in itemValue);");
        sb.AppendLine("                    writer.WriteArrayEnd();");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            writer.WriteArrayEnd();");
    }

    /// <summary>Appends JSON reading statements for frozen dictionary values.</summary>
    /// <param name="fieldData">The metadata describing the field being processed.</param>
    /// <param name="sb">The builder receiving generated source code.</param>
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
        sb.AppendLine("                reader.ReadValue(ref key);");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        sb.AppendLine("                reader.ReadValue(ref itemValue);");
        sb.AppendLine("                dict.Add(key!, itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);");
    }
}
