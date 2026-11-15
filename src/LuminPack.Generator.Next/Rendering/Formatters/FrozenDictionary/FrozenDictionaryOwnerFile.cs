using LuminPack.SourceGenerator.Next.Rendering.Emitters;
namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owner of one closed <c>FrozenDictionary&lt;TKey, TValue&gt;</c> of raw scalar or string entries.</summary>
/// <remarks>The write walks the key and value arrays and fails when the written count differs from the stored one. The read
/// returns the empty instance for an empty payload, otherwise adds each entry to a presized dictionary and freezes it. The JSON
/// read stages entries in a dictionary that is frozen after the last entry.</remarks>
internal static class FrozenDictionaryOwnerFile
{
    private static readonly PairedDictionarySpec Spec = new PairedDictionarySpec(Write, Read, "staged.Add(key!, itemValue);",
        BeforeJson, AfterJson, capturesJsonPairs: true);

    /// <summary>Renders the file.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string Render(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, Spec);

    /// <summary>Handles write for <c>FrozenDictionaryOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Write(PairedDictionaryContext c)
    {
        var w = c.W;
        if (c.RawPairs && c.Supports("FrozenRawPairs"))
        {
            w.Line("global::LuminPack.Utility.LuminFrozenConstruction.WritePairs<" + c.Key + ", " + c.Value + ">(ref operationState, value.Keys.AsSpan(), value.Values.AsSpan());");
            return;
        }
        w.Line("int count = value.Count;");
        w.Line("int countPosition = operationState.BeginCollection();");
        w.Line("var keys = value.Keys.AsSpan();");
        w.Line("var values = value.Values.AsSpan();");
        w.Line("int pairCount = keys.Length;");
        w.Line("if (pairCount != 0)");
        w.Open();
        w.Line("if (values.Length < pairCount)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();"));
        w.Line("ref var firstKey = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(keys);");
        w.Line("ref var firstValue = ref global::System.Runtime.InteropServices.MemoryMarshal.GetReference(values);");
        ParallelDictionaryWrite.Emit(c, "pairCount");
        w.Close();
        w.Line("operationState.EndCollection(countPosition, count);");
        w.Line("operationState.CheckBuffer();");
        w.Line("if (pairCount != count)");
        w.Indented(() => w.Line("global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidConcurrrentCollectionOperation();"));
    }

    /// <summary>Handles read for <c>FrozenDictionaryOwnerFile</c>.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void Read(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = " + c.Dictionary + ".Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        bool rented = c.Supports("RentFrozenDictionary");
        w.Line(rented ? "var dict = global::LuminPack.Utility.LuminFrozenConstruction.RentDictionary<" + c.Key + ", " + c.Value + ">(length);" :
            "var dict = new global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">(length);");
        if (rented)
        {
            w.Line("try");
            w.Open();
        }
        w.Line("int remaining = length;");
        w.Line("do");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("dict.Add(k!, v);");
        w.Close();
        w.Line("while (--remaining != 0);");
        w.Line("value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(dict);");
        if (rented)
        {
            w.Close();
            w.Line("finally");
            w.Open();
            w.Line("global::LuminPack.Utility.LuminFrozenConstruction.ReturnDictionary(dict);");
            w.Close();
        }
    }

    /// <summary>Rents the JSON builder when the matching ownership API is available.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void BeforeJson(PairedDictionaryContext c)
    {
        c.W.Line(c.Supports("RentFrozenDictionary") ?
            "var staged = global::LuminPack.Utility.LuminFrozenConstruction.RentDictionary<" + c.Key + ", " + c.Value + ">(0);" :
            "var staged = new global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">();");
        if (c.Supports("RentFrozenDictionary"))
        {
            c.W.Line("try");
            c.W.Open();
        }
    }

    /// <summary>Freezes the staged entries and returns the builder on every exit.</summary>
    /// <param name="c">The case or formatter context in scope.</param>
    private static void AfterJson(PairedDictionaryContext c)
    {
        c.W.Line("value = global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(staged);");
        if (!c.Supports("RentFrozenDictionary"))
            return;
        c.W.Close();
        c.W.Line("finally");
        c.W.Open();
        c.W.Line("global::LuminPack.Utility.LuminFrozenConstruction.ReturnDictionary(staged);");
        c.W.Close();
    }
}
