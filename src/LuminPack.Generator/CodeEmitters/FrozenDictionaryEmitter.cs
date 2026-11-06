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
        if (fieldData.CanWriteFrozenPairs && fieldData.TypeSymbol is Microsoft.CodeAnalysis.INamedTypeSymbol pair &&
            CollectionEmitterKit.IsRawScalar(pair.TypeArguments[0], keyType) && CollectionEmitterKit.IsRawScalar(pair.TypeArguments[1], valueType))
        {
            sb.AppendLine("            if (value is null) { writer.EnsureAdditionalCapacity(4); writer.WriteNullCollectionHeader(); return; }");
            sb.AppendLine("            global::LuminPack.Utility.LuminFrozenConstruction.WritePairs<" + keyType + ", " + valueType + ">(ref writer, value.Keys.AsSpan(), value.Values.AsSpan());");
            return;
        }
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
        sb.AppendLine("            var count = value.Count;");
        sb.AppendLine("            writer.WriteCollectionHeader(ref index, count);");
        sb.AppendLine("            writer.Advance(4);");
        sb.AppendLine();
        sb.AppendLine("            var i = 0;");
        sb.AppendLine("            var keys = value.Keys.AsSpan();");
        sb.AppendLine("            var values = value.Values.AsSpan();");
        sb.AppendLine("            for (; i < keys.Length;)");
        sb.AppendLine("            {");
        sb.AppendLine("                var key = keys[i];");
        sb.AppendLine("                var itemValue = values[i];");
        sb.AppendLine("                writer.CheckBuffer();");
        sb.AppendLine("                i++;");
        sb.AppendLine("                writer.WriteValue(in key);");
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
        sb.AppendLine("            if (!reader.TryReadCollectionHead(ref index, out var length" + CollectionEmitterKit.HeaderMinimumArgument(fieldData.TypeSymbol) + "))");
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
        sb.AppendLine("            if (length == 0) { value = global::System.Collections.Frozen.FrozenDictionary<" + keyType + ", " + valueType + ">.Empty; return; }");
        if (fieldData.CanRentFrozenBuilder)
        {
            sb.AppendLine("            var dict = global::LuminPack.Utility.LuminFrozenConstruction.RentDictionary<" + keyType + ", " + valueType + ">(length);");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
        }
        else
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
        if (fieldData.CanRentFrozenBuilder)
        {
            sb.AppendLine("            }");
            sb.AppendLine("            finally { global::LuminPack.Utility.LuminFrozenConstruction.ReturnDictionary(dict); }");
        }
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
        DictionaryEmitter.AppendJsonDictionaryEntrySerialize(sb, "item.Key", "item.Value", "                    ");
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
        if (fieldData.CanRentFrozenBuilder)
        {
            sb.AppendLine("            var dict = global::LuminPack.Utility.LuminFrozenConstruction.RentDictionary<" + keyType + ", " + valueType + ">(0);");
            sb.AppendLine("            try");
            sb.AppendLine("            {");
        }
        else
            sb.AppendLine("            var dict = new global::System.Collections.Generic.Dictionary<" + keyType + ", " + valueType + ">();");
        sb.AppendLine("            reader.TryConsumeArrayStart();");
        sb.AppendLine("            while (reader.Read())");
        sb.AppendLine("            {");
        sb.AppendLine("                if (reader.CurrentTokenType == global::LuminPack.Core.LuminPackJsonReader.JsonTokenType.ArrayEnd)");
        sb.AppendLine("                    break;");
        DictionaryEmitter.AppendSkipNonArrayEntry(sb, "                ");
        sb.AppendLine("                reader.TryConsumeArrayStart();");
        sb.AppendLine("                " + keyType + " key = default!;");
        sb.AppendLine("                " + valueType + " itemValue = default!;");
        // The shared entry grammar reads the key/value pair array exactly as the Dictionary writer's pair array is written.
        DictionaryEmitter.AppendJsonEntryRead(sb);
        sb.AppendLine("                dict.Add(key!, itemValue);");
        sb.AppendLine("            }");
        sb.AppendLine("            value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);");
        if (fieldData.CanRentFrozenBuilder)
        {
            sb.AppendLine("            }");
            sb.AppendLine("            finally { global::LuminPack.Utility.LuminFrozenConstruction.ReturnDictionary(dict); }");
        }
    }
}
