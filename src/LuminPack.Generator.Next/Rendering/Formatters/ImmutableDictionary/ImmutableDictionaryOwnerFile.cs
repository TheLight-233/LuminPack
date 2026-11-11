namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ImmutableDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write copies the hash-tree pairs once before writing them. The read returns the empty instance for an empty
/// payload, otherwise stages the pairs in a pooled array and builds the dictionary through <c>CreateImmutableDictionary</c>,
/// which links distinct key hash codes in one pass and uses the builder for any other input. The JSON read stages entries in a
/// builder.</remarks>
internal static class ImmutableDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.TreeWrite(), Read, "staged[key] = itemValue;",
        c => c.W.Line("var staged = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<" + c.Key + ", " + c.Value + ">();"),
        c => c.W.Line("value = staged.ToImmutable();"));

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + c.Dictionary + ".Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        c.ReadPairs(() => w.Line("value = global::LuminPack.Utility.LuminTreeStorage.CreateImmutableDictionary(pairs, length);"));
    }
}
