using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Publishes the shared formatter owners the complete payload models require.</summary>
/// <remarks>The files mirror the legacy formatter generator's shared roots and owner classes. They are emitted once per
/// compilation and only when a rendered payload references their owner type; the hint names use a directory prefix so they
/// cannot collide with a payload file's hint name.</remarks>
internal static class SharedFormatterPublisher
{
    private const string IntArrayHint = "LuminPack.SharedFormatters/IntArray.g.cs";
    private const string ListOfIntHint = "LuminPack.SharedFormatters/ListOfInt.g.cs";
    private const string StringHint = "LuminPack.SharedFormatters/String.g.cs";
    private const string IntHint = "LuminPack.SharedFormatters/Int.g.cs";
    private const string CultureInfoHint = "LuminPack.SharedFormatters/CultureInfo.g.cs";
    private const string ConcurrentQueueOfIntHint = "LuminPack.SharedFormatters/ConcurrentQueueOfInt.g.cs";
    private const string ImmutableArrayOfIntHint = "LuminPack.SharedFormatters/ImmutableArrayOfInt.g.cs";
    private const string ConcurrentQueueOfStringHint = "LuminPack.SharedFormatters/ConcurrentQueueOfString.g.cs";
    private const string ImmutableArrayOfStringHint = "LuminPack.SharedFormatters/ImmutableArrayOfString.g.cs";

    /// <summary>Publishes every owner in the set.</summary>
    /// <param name="production">The source production context.</param>
    /// <param name="set">The required owners and the extension container they belong to.</param>
    internal static void Publish(SourceProductionContext production, SharedFormatterSet set)
    {
        if (set.IsEmpty)
            return;
        if ((set.Owners & SharedFormatterKind.IntArray) != 0)
            production.AddSource(IntArrayHint, IntArrayOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.ListOfInt) != 0)
            production.AddSource(ListOfIntHint, ListOfIntOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.String) != 0)
            production.AddSource(StringHint, StringOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.Int) != 0)
            production.AddSource(IntHint, IntOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.CultureInfo) != 0)
            production.AddSource(CultureInfoHint, CultureInfoOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.ConcurrentQueueOfInt) != 0)
            production.AddSource(ConcurrentQueueOfIntHint, ConcurrentQueueOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.ImmutableArrayOfInt) != 0)
            production.AddSource(ImmutableArrayOfIntHint, ImmutableArrayOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.ConcurrentQueueOfString) != 0)
            production.AddSource(ConcurrentQueueOfStringHint, ConcurrentQueueStringOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.ImmutableArrayOfString) != 0)
            production.AddSource(ImmutableArrayOfStringHint, ImmutableArrayStringOwnerFile.Render(set));
        // A packable-class sequence publishes one owner per closed sequence type, keyed by its declared spelling.
        if (set.SequenceOwners != null)
            foreach (SequenceOwnerFact fact in set.SequenceOwners)
            {
                string hint = SequenceHint(fact);
                if (fact.Immutable)
                    production.AddSource(hint, ImmutableArrayPayloadOwnerFile.Render(set, fact));
                else
                    production.AddSource(hint, ConcurrentQueuePayloadOwnerFile.Render(set, fact));
            }
    }

    /// <summary>Names the hint of a payload-element sequence owner.</summary>
    /// <param name="fact">The sequence owner fact.</param>
    /// <returns>The hint name, unique per closed sequence type.</returns>
    private static string SequenceHint(SequenceOwnerFact fact) => "LuminPack.SharedFormatters/Sequence_" +
        ReadableTypeName.Of(GeneratedNames.PayloadKey(fact.DeclaredType)) + ".g.cs";
}
