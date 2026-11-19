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
    /// <param name="exactSnapshot">The exact runtime type and its existing value traversal, if available.</param>
    /// <param name="boundedSnapshot">Whether enumeration can observe later inserts or removals.</param>
    /// <param name="stringProperties">Whether this family accepts the object form for string keys.</param>
    /// <param name="stagedConcurrent">Whether the runtime can publish this concrete string-keyed concurrent dictionary.</param>
    internal static void Emit(CodeWriter w, string key, string value, string keyOwner, string valueOwner, string store, string jsonReader,
        string? sortedList = null, (string Type, string Source)? exactSnapshot = null,
        bool boundedSnapshot = false, bool stringProperties = false, bool stagedConcurrent = false)
    {
        string pair = "global::System.Collections.Generic.KeyValuePair<" + key + ", " + value + ">";
        string pool = "global::System.Buffers.ArrayPool<" + pair + ">.Shared";
        string clear = "global::System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<" + pair + ">()";
        w.Line("if (value is not null)");
        w.Open();
        if (stringProperties)
        {
            w.Line("bool objectForm = reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectStart;");
            w.Line("int length = 16;");
            w.Line("if (objectForm)");
            w.Open();
            DictionaryJsonPropertyReader.SelectFirst(w);
            w.Line("if (reader.CurrentTokenType == " + jsonReader + ".JsonTokenType.ObjectEnd)");
            w.Open();
            w.Line("value.Clear();");
            w.Line("return;");
            w.Close();
            w.Close();
            w.Line("else");
            w.Open();
        }
        w.Line("reader.TryConsumeArrayStart<TLuminProfile>();");
        w.Line((stringProperties ? "" : "int ") + "length = reader.CountArrayElements<TLuminProfile>();");
        w.Line("if (length == 0)");
        w.Open();
        w.Line("reader.ConsumeArrayEnd<TLuminProfile>();");
        w.Line("value.Clear();");
        w.Line("return;");
        w.Close();
        if (stringProperties)
            w.Close();
        w.Line("int oldCount = value.Count;");
        w.Line("var entries = " + pool + ".Rent(global::System.Math.Max(length, oldCount));");
        w.Line("int count = 0;");
        w.Line("try");
        w.Open();
        const string firstEntry = "ref var firstEntry = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(entries);";
        if (!stringProperties)
            w.Line(firstEntry);
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
            if (boundedSnapshot)
            {
                w.Line("if (oldCount != 0)");
                w.Open();
            }
            if (exactSnapshot is { } snapshot)
            {
                w.Line("if (value.GetType() == typeof(" + snapshot.Type + "))");
                w.Indented(() => CopyOldEntries(snapshot.Source));
                w.Line("else");
                w.Indented(() => CopyOldEntries("value"));
            }
            else
                CopyOldEntries("value");
            if (boundedSnapshot)
            {
                w.Close();
                w.Line("oldCount = oldIndex;");
            }
            if (sortedList != null)
                w.Close();

            void CopyOldEntries(string source)
            {
                if (!boundedSnapshot)
                {
                    w.Line("foreach (var entry in " + source + ") entries[oldIndex++] = entry;");
                    return;
                }
                w.Line("foreach (var entry in " + source + ")");
                w.Open();
                w.Line("entries[oldIndex++] = entry;");
                w.Line("if (oldIndex == oldCount) break;");
                w.Close();
            }
        }

        if (stringProperties)
        {
            w.Line("if (objectForm)");
            w.Open();
            DictionaryJsonPropertyReader.Emit(w, keyOwner, valueOwner, value, jsonReader,
                "entries[count++] = new " + pair + "(key, itemValue);",
                "count < oldCount ? entries[count].Key : default!",
                "value.TryGetValue(key, out itemValue);", writer =>
                {
                    writer.Line("if (count == entries.Length)");
                    writer.Open();
                    writer.Line("var larger = " + pool + ".Rent(checked(count * 2));");
                    writer.Line("global::System.Array.Copy(entries, larger, count);");
                    writer.Line(pool + ".Return(entries, " + clear + ");");
                    writer.Line("entries = larger;");
                    writer.Close();
                }, firstPropertySelected: true);
            w.Close();
            w.Line("else");
            w.Open();
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
        if (stringProperties)
        {
            w.Close();
            w.Line(firstEntry);
        }
        if (stagedConcurrent)
        {
            w.Line("if (global::LuminPack.Code.LuminPackCollectionAccess.TryPublishStagedStringDictionary(value, entries, count))");
            w.Indented(() => w.Line("return;"));
        }
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
        w.Line(pool + ".Return(entries, " + clear + ");");
        w.Close();
        w.Line("return;");
        w.Close();
    }
}
