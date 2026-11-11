using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Renders the shared owners of the closed dictionary interfaces of raw scalar or string entries.</summary>
/// <remarks>The write counts the enumeration of the value. The <c>IDictionary</c> and <c>IReadOnlyDictionary</c> reads forward to
/// the concrete dictionary owner, which an exact non-null destination of that type reuses. The immutable interface reads
/// the empty instance for an empty payload and otherwise adds each entry to a builder.</remarks>
internal static class DictionaryInterfaceOwnerFile
{
    private static readonly PairedDictionarySpec MutableSpec = new PairedDictionarySpec(c => c.CountedWrite(), ReadMutable,
        "staged.Add(key!, itemValue);",
        c => c.W.Line("var staged = new global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">();"),
        c => c.W.Line("value = staged;"), readsHeader: true);

    private static readonly PairedDictionarySpec ImmutableSpec = new PairedDictionarySpec(c => c.CountedWrite(), ReadImmutable,
        "staged[key!] = itemValue;",
        c => c.W.Line("var staged = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<" + c.Key + ", " + c.Value + ">();"),
        c => c.W.Line("value = staged.ToImmutable();"));

    /// <summary>Renders an <c>IDictionary</c> or <c>IReadOnlyDictionary</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderMutable(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, MutableSpec);

    /// <summary>Renders an <c>IImmutableDictionary</c> owner.</summary>
    /// <param name="set">The required owners and the extension container.</param>
    /// <param name="fact">The closed dictionary owner.</param>
    /// <param name="operations">The operations requested for this owner.</param>
    /// <returns>The complete source.</returns>
    internal static string RenderImmutable(SharedFormatterSet set, DictionaryOwnerFact fact, GeneratedOperations operations) =>
        PairedDictionaryOwnerTemplate.Render(set, fact, operations, ImmutableSpec);

    private static void ReadMutable(PairedDictionaryContext c)
    {
        var w = c.W;
        string concrete = "global::System.Collections.Generic.Dictionary<" + c.Key + ", " + c.Value + ">";
        string owner = "global::LuminPack.Binary_" + c.Extension + "_" + ReadableTypeName.Of(GeneratedNames.PayloadKey(concrete));
        // An exact dictionary destination is reused by the concrete read.
        w.Line(concrete + "? dictionary = value is not null && value.GetType() == typeof(" + concrete + ") ? " + c.Memory + ".As<" + concrete + ">(value) : null;");
        w.Line(owner + ".Read<TLuminProfile>(ref operationState, ref dictionary);");
        w.Line("value = dictionary;");
    }

    private static void ReadImmutable(PairedDictionaryContext c)
    {
        var w = c.W;
        w.Line("if (length == 0)");
        w.Open();
        w.Line("value = global::System.Collections.Immutable.ImmutableDictionary<" + c.Key + ", " + c.Value + ">.Empty;");
        w.Line("goto operationComplete_RootRead;");
        w.Close();
        w.Blank();
        w.Line("var builder = global::System.Collections.Immutable.ImmutableDictionary.CreateBuilder<" + c.Key + ", " + c.Value + ">();");
        w.Line("for (int index = 0; index < length; index++)");
        w.Open();
        w.Line(c.Key + " k = default !;");
        w.Line(c.Value + " v = default !;");
        c.ReadMember(c.Key, "k");
        c.ReadMember(c.Value, "v");
        w.Line("builder.Add(k!, v);");
        w.Close();
        w.Line("value = builder.ToImmutable();");
    }
}
