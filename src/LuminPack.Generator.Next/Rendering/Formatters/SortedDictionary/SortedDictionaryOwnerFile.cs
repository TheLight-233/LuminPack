using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>SortedDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write copies the in-order pairs once through the tree storage helper before writing them. The read stages the
/// pairs in a pooled array and fills the cleared or new dictionary through <c>FillSortedDictionary</c>, which links strictly
/// ascending keys under the default comparer in one pass and adds any other input pair by pair.</remarks>
internal static class SortedDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.TreeWrite(), Read, "value.Add(key, itemValue);");

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
        w.Line("if (value is null)");
        w.Indented(() => w.Line("value = new " + c.Dictionary + "();"));
        w.Line("else");
        w.Indented(() => w.Line("value.Clear();"));
        w.Blank();
        w.Line("if (length == 0)");
        w.Indented(() => w.Line("goto operationComplete_RootRead;"));
        w.Blank();
        c.ReadPairs(() => w.Line("global::LuminPack.Utility.LuminTreeStorage.FillSortedDictionary(value, pairs, length);"));
    }
}
