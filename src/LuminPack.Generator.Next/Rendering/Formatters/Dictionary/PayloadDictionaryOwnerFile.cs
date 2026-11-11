using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>Dictionary&lt;TKey, TValue&gt;</c> whose value is a packable class.</summary>
/// <remarks>The write counts the enumeration of the dictionary as it writes each key and expands each value's record body. The read
/// creates a presized dictionary or clears the reused one and adds each entry, expanding the value's record body on the caller's
/// reader. The JSON form is an array of key and value arrays whose values use the value's own formatter owner.</remarks>
internal static class PayloadDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(c => c.CountedWrite(), Read, "value[key!] = itemValue;");

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
        w.Line("value ??= new " + c.Dictionary + "(0);");
        w.Line("value.Clear();");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("if (value is not null)");
        w.Open();
        c.ReadExistingDictionary();
        w.Line("goto operationComplete_RootRead;");
        w.Close();
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
        w.Line("value.Add(k!, v);");
        w.Close();
    }
}
