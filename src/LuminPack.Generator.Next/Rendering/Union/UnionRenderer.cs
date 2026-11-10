using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Union;

/// <summary>Renders the Union root file: root operations, registration, virtual slots and per-case overrides.</summary>
internal sealed class UnionRenderer : IRenderer
{
    /// <summary>The shared renderer.</summary>
    internal static readonly UnionRenderer Instance = new UnionRenderer();

    /// <inheritdoc/>
    public string Render(TypeModel model, RenderContext context) => model is UnionModel union ? Render(union, context) : string.Empty;

    private static string Render(UnionModel model, RenderContext context)
    {
        // TODO: the compiled profile slots, the dispatch tier, the case slot overrides and the registration table.
        return string.Empty;
    }
}
