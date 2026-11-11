using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>ConcurrentDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write counts the enumeration of the dictionary as it writes. The read creates a new dictionary from the staged
/// pairs through the collection-access helper, or clears and refills a non-null destination with <c>TryAdd</c>.</remarks>
internal static class ConcurrentDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.CountedWrite(), Read, "value.TryAdd(key, itemValue);");

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
        w.Open();
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentDictionary(global::System.Array.Empty<" + c.Pair + ">(), length);");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        c.ReadPairs(() => w.Line("value = global::LuminPack.Code.LuminPackCollectionAccess.CreateConcurrentDictionary(pairs, length);"));
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("value.Clear();");
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("value.TryAdd(k!, v);");
        w.Close();
    }
}
