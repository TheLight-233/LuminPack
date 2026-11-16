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
    /// <param name="sortedList">The exact sorted-list type when the runtime can traverse its validated storage.</param>
    internal static void Emit(CodeWriter w, string key, string value, string keyOwner, string valueOwner, string store, string jsonReader,
        string? sortedList = null)
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
        w.Line("ref var firstEntry = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(entries);");
        const string unsafeType = "global::System.Runtime.CompilerServices.Unsafe";
        if (key == "string")
        {
            if (sortedList != null)
            {
                const string access = "global::LuminPack.Code.LuminPackCollectionAccess.";
                w.Line("if (value.GetType() == typeof(" + sortedList + ") && " + access +
                    "TryGetSortedListTraversal(value, out var oldKeys, out var oldValues, out int oldVersion))");
                w.Open();
                w.Line("for (int oldIndex = 0; oldIndex < oldKeys.Length; oldIndex++)");
                w.Open();
                w.Line(access + "CheckSortedListTraversalVersion(value, oldVersion);");
                w.Line("entries[oldIndex] = new " + pair + "(oldKeys[oldIndex], oldValues[oldIndex]);");
                w.Close();
                w.Line(access + "CheckSortedListTraversalVersion(value, oldVersion);");
                w.Close();
                w.Line("else");
                w.Open();
            }
            w.Line("int oldIndex = 0;");
            w.Line("foreach (var entry in value) entries[oldIndex++] = entry;");
            if (sortedList != null)
                w.Close();
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
        w.Line("if (count != 0)");
        w.Open();
        w.Line("int index = 0;");
        w.Line("do");
        w.Open();
        w.Line("var entry = " + unsafeType + ".Add(ref firstEntry, (nint)(uint)index);");
        w.Line("var key = entry.Key;");
        w.Line("var itemValue = entry.Value;");
        w.Line(store);
        w.Close();
        w.Line("while (++index < count);");
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
