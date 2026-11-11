using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;
/// <summary>Preserves JSON key and value reuse until all replacement entries have been decoded.</summary>
internal static class DictionaryJsonReuseEmitter
{
    /// <summary>Emits the existing-destination branch before the family's fresh JSON read.</summary>
    /// <param name = "w">The generated source writer.</param>
    /// <param name = "key">The key used by this operation.</param>
    /// <param name = "value">The value used by this operation.</param>
    /// <param name = "keyOwner">The key owner used by this operation.</param>
    /// <param name = "valueOwner">The value owner used by this operation.</param>
    /// <param name = "store">The store used by this operation.</param>
    /// <param name = "jsonReader">The json reader used by this operation.</param>
    internal static void Emit(CodeWriter w, string key, string value, string keyOwner, string valueOwner, string store, string jsonReader)
    {
        string pair = "global::System.Collections.Generic.KeyValuePair<" + key + ", " + value + ">";
        w.Line("if (value is not null)");
        w.Open();
        w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
        w.Line("int length = reader.CountArrayElements<TLuminProfile>();");
        w.Line("if (length == 0)");
        w.Open();
        w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
        w.Line("value.Clear();");
        w.Line("return;");
        w.Close();
        w.Line("int oldCount = value.Count;");
        w.Line("var entries = global::System.Buffers.ArrayPool<" + pair + ">.Shared.Rent(global::System.Math.Max(length, oldCount));");
        w.Line("int count = 0;");
        w.Line("try");
        w.Open();
        if (key == "string")
        {
            w.Line("int oldIndex = 0;");
            w.Line("foreach (var entry in value) entries[oldIndex++] = entry;");
        }

        w.Line("while (reader.Read<TLuminProfile>())");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd) break;");
        w.Line("if (reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.ArrayStart)");
        w.Open();
        w.Line("reader.Skip<TLuminProfile>();");
        w.Line("continue;");
        w.Close();
        w.Line(key + " key = default!;");
        w.Line(value + " itemValue = default!;");
        w.Line("if (reader.Read<TLuminProfile>() && reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.ArrayEnd)");
        w.Open();
        if (key == "string")
            w.Line("key = count < oldCount ? entries[count].Key : default!;");
        w.Line(keyOwner + ".Read<TLuminProfile>(ref reader, ref key);");
        w.Line("if (reader.Read<TLuminProfile>() && reader.CurrentTokenType != " + jsonReader + ".JsonTokenType.ArrayEnd)");
        w.Open();
        w.Line("value.TryGetValue(key, out itemValue);");
        w.Line(valueOwner + ".Read<TLuminProfile>(ref reader, ref itemValue);");
        w.Line("while (reader.Read<TLuminProfile>())");
        w.Open();
        w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ArrayEnd) break;");
        w.Line("reader.Skip<TLuminProfile>();");
        w.Close();
        w.Close();
        w.Close();
        w.Line("entries[count++] = new " + pair + "(key!, itemValue);");
        w.Close();
        w.Line("value.Clear();");
        w.Line("for (int index = 0; index < count; index++)");
        w.Open();
        w.Line("var key = entries[index].Key;");
        w.Line("var itemValue = entries[index].Value;");
        w.Line(store);
        w.Close();
        w.Close();
        w.Line("finally");
        w.Open();
        w.Line("global::System.Buffers.ArrayPool<" + pair + ">.Shared.Return(entries, global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + pair + ">());");
        w.Close();
        w.Line("return;");
        w.Close();
    }
}
