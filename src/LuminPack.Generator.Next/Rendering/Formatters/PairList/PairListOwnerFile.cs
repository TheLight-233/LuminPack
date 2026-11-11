namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>List&lt;KeyValuePair&lt;TKey, TValue&gt;&gt;</c> whose key is a raw scalar and
/// whose value is a raw scalar or a string.</summary>
/// <remarks>The wire is the entry count followed by each key and its value. The write counts the enumeration of the list; the
/// read creates a presized list or clears the reused one and adds each pair. The JSON form is an array of key and value arrays.</remarks>
internal static class PairListOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.CountedWrite(), Read,
        "value.Add(global::System.Collections.Generic.KeyValuePair.Create(key!, itemValue));");

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed pair list owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Dictionary + "(length);"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("value.Add(new " + c.Pair + "(k!, v));");
        w.Close();
    }
}
