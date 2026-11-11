using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>FrozenDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write walks the key and value arrays and fails when the written count differs from the stored one. The read
/// returns the empty instance for an empty payload, otherwise adds each entry to a presized dictionary and freezes it. The JSON
/// read stages entries in a dictionary that is frozen after the last entry.</remarks>
internal static class FrozenDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(Write, Read, "staged.Add(key!, itemValue);",
        c => c.W.Line("var staged = new global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">();"),
        c => c.W.Line("value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(staged);"));

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    private static void Write(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("int count = value.Count;");
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("var keys = value.Keys.AsSpan();");
        w.Line("var values = value.Values.AsSpan();");
        w.Line("int written = 0;");
        w.Line("while (written < keys.Length)");
        w.Open();
        w.Line(c.Key + " key = keys[written];");
        w.Line(c.Value + " itemValue = values[written];");
        w.Line("written++;");
        c.WriteMember(c.Key, "key");
        c.WriteMember(c.Value, "itemValue");
        w.Close();
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
        w.Line("if (written != count)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();"));
    }

    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + c.Dictionary + ".Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("var dict = new global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">(length);");
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("dict.Add(k!, v);");
        w.Close();
        w.Line("value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);");
    }
}
