using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>Dictionary&lt;TKey, TValue&gt;</c> whose value is a packable class.</summary>
/// <remarks>The write counts the enumeration of the dictionary as it writes each key and expands each value's record body. The read
/// creates a presized dictionary or clears the reused one and adds each entry, expanding the value's record body on the caller's
/// reader. JSON string keys name object properties. Each value uses its own JSON formatter in either input form.</remarks>
internal static class PayloadDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.DenseWrite(), Read, "value[key!] = itemValue;");

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        Render(set, fact, operations, null);

    /// <summary>Renders the same dictionary traversal with checked fresh Union elements when requested.</summary>
    /// <param name="set">The shared formatter owners and publication options.</param>
    /// <param name="fact">The fact supplied to <c>Render</c>.</param>
    /// <param name="operations">The serialization operations selected for emission.</param>
    /// <param name="checkedUnion">The checked union supplied to <c>Render</c>.</param>
    /// <returns>The generated C# source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations,
        PayloadRenderModel? checkedUnion) => PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec, checkedUnion);

    /// <summary>Handles read for <c>PayloadDictionaryOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value ??= new " + c.Dictionary + "(0);");
        w.Line("value.Clear();");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("if (value is not null)");
        w.Open();
        w.Line("ReadExisting(ref operationState, ref value, length);");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line(w.MethodImpl("NoInlining"));
        w.Line("static void ReadExisting(scoped ref " + w.A("LuminPackReader") + " reader, scoped ref " + c.Dictionary + " value, int length)");
        w.Open();
        w.Line("ref var operationState = ref reader;");
        c.ReadExistingDictionary();
        w.Close();
        w.Blank();
        if (c.UnionValue is { } union && c.Dictionary.StartsWith("global::System.Collections.Generic.Dictionary<", System.StringComparison.Ordinal))
        {
            FreshUnionDictionary(c, union);
            return;
        }
        w.Line("value = new " + c.Dictionary + "(length);");
        w.Blank();
        w.Line("int remaining = length;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k", fresh: true);
        c.ReadMember(c.Value, "v", fresh: true);
        w.Line("value.Add(k!, v);");
        w.Close();
        w.Line("while (--remaining != 0);");
    }

    /// <summary>The original fresh dictionary of Union values: entries are written into the new dictionary's storage with the
    /// Union dispatch expanded per value, the buckets are rebuilt once, and a 32-bit process keeps ordinary insertion.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    /// <param name="union">The union supplied to <c>FreshUnionDictionary</c>.</param>
    private static void FreshUnionDictionary(PairedDictionaryContext c, PayloadRenderModel union)
    {
        var w = c.W;
        w.Line("if (!global::LuminPack.Code.LuminPackMarshal.CanPopulateDictionaryEntries<" + c.Key + ", " + c.Value + ">())");
        w.Open();
        w.Line("var fallback = new " + c.Dictionary + "(length);");
        w.Line("int fallbackRemaining = length;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("fallback.Add(k!, v);");
        w.Close();
        w.Line("while (--fallbackRemaining != 0);");
        w.Line("value = fallback;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("var result = new " + c.Dictionary + "(length);");
        w.Line("var dictionaryView = global::LuminPack.Code.LuminPackMarshal.GetDictionaryView(result);");
        w.Line("ref var first = ref global::LuminPack.Code.LuminPackMarshal.GetNotNullArrayReference(dictionaryView._entries!);");
        w.Line("int entryIndex = 0;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " key = default !;");
        w.Line(c.Value + " itemValue = default !;");
        c.ReadMember(c.Key, "key");
        UnionEmitter.FreshElementRead(union, w, "itemValue", "freshEnd", "operationState");
        w.Line("freshEnd:");
        w.Line("ref var entry = ref " + c.Memory + ".Add(ref first, (nint)(uint)entryIndex);");
        w.Line("entry.Key = key;");
        w.Line("entry.Value = itemValue;");
        w.Close();
        w.Line("while (++entryIndex < length);");
        w.Blank();
        w.Line("if (!global::LuminPack.Code.LuminPackMarshal.TryRebuildFreshDictionaryBuckets(dictionaryView, length))");
        w.Open();
        w.Line("var rebuilt = new " + c.Dictionary + "(length);");
        w.Line("entryIndex = 0;");
        w.Line("do");
        w.Open();
        w.Line("ref var entry = ref " + c.Memory + ".Add(ref first, (nint)(uint)entryIndex);");
        w.Line("rebuilt.Add(entry.Key, entry.Value);");
        w.Close();
        w.Line("while (++entryIndex < length);");
        w.Line("result = rebuilt;");
        w.Close();
        w.Line("value = result;");
    }
}
