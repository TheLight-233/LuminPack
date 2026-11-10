using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Rendering.Formatters;

/// <summary>Publishes the shared formatter owners the complete payload models require.</summary>
/// <remarks>The three files mirror the legacy formatter generator's shared roots and owner classes. They are emitted once
/// per compilation and only when a rendered payload references their owner type; the hint names use a directory prefix so
/// they cannot collide with a payload file's hint name.</remarks>
internal static class SharedFormatterPublisher
{
    private const string IntArrayHint = "LuminPack.SharedFormatters/IntArray.g.cs";
    private const string ListOfIntHint = "LuminPack.SharedFormatters/ListOfInt.g.cs";
    private const string StringHint = "LuminPack.SharedFormatters/String.g.cs";
    private const string IntHint = "LuminPack.SharedFormatters/Int.g.cs";

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
    }
}
