using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ReadOnlyDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write counts the enumeration of the wrapper as it writes. The read forwards to the concrete dictionary owner,
/// so the wrapped dictionary is built by its direct entry and bucket construction, and wraps the result; the JSON read stages
/// the entries in a dictionary the wrapper then holds.</remarks>
internal static class ReadOnlyDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.CountedWrite(), Read, "staged.Add(key!, itemValue);",
        c => c.W.Line("var staged = new global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">();"),
        c => c.W.Line("value = new " + c.Dictionary + "(staged);"), readsHeader: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Spells the concrete dictionary a wrapper forwards to.</summary>
    /// <param name="fact">The wrapper fact.</param>
    /// <returns>The concrete dictionary spelling.</returns>
    internal static string Concrete(DictionaryOwnerFact fact) =>
        "global::System.Collections.Generic.Dictionary<" + fact.Key + ", " + fact.Value + ">";

    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        string concrete = "global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">";
        string owner = "global::LuminPack.Binary_" + c.Extension + "_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(concrete));
        w.Line(concrete + "? dictionary = null;");
        w.Line(owner + ".Read<TLuminProfile>(ref operationState, ref dictionary);");
        w.Line("value = dictionary is null ? null : new " + c.Dictionary + "(dictionary);");
    }
}
