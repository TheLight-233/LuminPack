using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Json;

/// <summary>Renders JSON roots and name tables from a model.</summary>
internal sealed class JsonRenderer : IRenderer
{
    /// <summary>The shared renderer.</summary>
    internal static readonly JsonRenderer Instance = new JsonRenderer();

    /// <inheritdoc/>
    public string Render(TypeModel model, RenderContext context) => model switch
    {
        RecordModel record => Record(record, context),
        StructModel structModel => Struct(structModel, context),
        CircleReferenceModel circle => Circle(circle, context),
        UnionModel union => Union(union, context),
        _ => string.Empty,
    };

    private static string Record(RecordModel model, RenderContext context)
    {
        // TODO: object write/read, name tables and the fresh/reuse split.
        return string.Empty;
    }

    private static string Struct(StructModel model, RenderContext context)
    {
        // TODO: struct write/read and fresh mirror assignment.
        return string.Empty;
    }

    private static string Circle(CircleReferenceModel model, RenderContext context)
    {
        // TODO: identifier and reference properties beside the member names.
        return string.Empty;
    }

    private static string Union(UnionModel model, RenderContext context)
    {
        // TODO: the Union JSON dispatch and per-case arms.
        return string.Empty;
    }
}
