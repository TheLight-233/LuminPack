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
    private const string CultureInfoHint = "LuminPack.SharedFormatters/CultureInfo.g.cs";
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
        if ((set.Owners & SharedFormatterKind.CultureInfo) != 0)
            production.AddSource(CultureInfoHint, CultureInfoOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.ConcurrentQueueOfString) != 0)
            production.AddSource(ConcurrentQueueOfStringHint, ConcurrentQueueStringOwnerFile.Render(set));
        if ((set.Owners & SharedFormatterKind.ImmutableArrayOfString) != 0)
            production.AddSource(ImmutableArrayOfStringHint, ImmutableArrayStringOwnerFile.Render(set));
        // A raw scalar element is transferred through its own scalar owner, published once per keyword.
        if (set.Scalars != null)
            foreach (string scalar in set.Scalars)
                production.AddSource(ScalarHint(scalar), ScalarOwnerFile.Render(set, scalar));
        // A concurrent or immutable sequence publishes one owner per closed sequence type, keyed by its declared spelling.
        if (set.SequenceOwners != null)
            foreach (SequenceOwnerFact fact in set.SequenceOwners)
            {
                string hint = SequenceHint(fact);
                if (fact.Element != null)
                {
                    if (fact.Family == SequenceFamily.ConcurrentQueue)
                        production.AddSource(hint, ConcurrentQueuePayloadOwnerFile.Render(set, fact));
                    else if (fact.Family == SequenceFamily.ImmutableArray)
                        production.AddSource(hint, ImmutableArrayPayloadOwnerFile.Render(set, fact));
                    continue;
                }
                switch (fact.Family)
                {
                    case SequenceFamily.ConcurrentQueue:
                        production.AddSource(hint, ConcurrentQueueOwnerFile.Render(set, fact));
                        break;
                    case SequenceFamily.ImmutableArray:
                        production.AddSource(hint, ImmutableArrayOwnerFile.Render(set, fact));
                        break;
                    case SequenceFamily.ImmutableStack:
                        production.AddSource(hint, ImmutableStackOwnerFile.Render(set, fact));
                        break;
                }
            }
    }

    /// <summary>Names the hint of a sequence owner.</summary>
    /// <param name="fact">The sequence owner fact.</param>
    /// <returns>The hint name, unique per closed sequence type.</returns>
    private static string SequenceHint(SequenceOwnerFact fact) => "LuminPack.SharedFormatters/Sequence_" +
        ReadableTypeName.Of(GeneratedNames.PayloadKey(fact.DeclaredType)) + ".g.cs";

    /// <summary>Names the hint of a raw scalar owner.</summary>
    /// <param name="scalar">The scalar keyword.</param>
    /// <returns>The hint name, unique per scalar.</returns>
    private static string ScalarHint(string scalar) => "LuminPack.SharedFormatters/Scalar_" +
        ReadableTypeName.Of(scalar) + ".g.cs";
}
