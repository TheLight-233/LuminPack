using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering.Binary;

/// <summary>Renders binary roots, facades and payload extensions from a model.</summary>
/// <remarks>Each payload kind has one entry point. The shared primitives (cursor reservation, fixed-run proofs, reserved stores,
/// checked reads) are the next porting step; until then the renderer emits nothing.</remarks>
internal sealed class BinaryRenderer : IRenderer
{
    /// <summary>The shared renderer.</summary>
    internal static readonly BinaryRenderer Instance = new BinaryRenderer();

    /// <inheritdoc/>
    public string Render(TypeModel model, RenderContext context) => model switch
    {
        RecordModel record => Record(record, context),
        StructModel structModel => Struct(structModel, context),
        CircleReferenceModel circle => Circle(circle, context),
        // A Union root's payload file combines the root record with the dispatch container; UnionRenderer owns it.
        UnionModel => string.Empty,
        _ => string.Empty,
    };

    private static string Record(RecordModel model, RenderContext context)
    {
        // TODO: roots, entries, extensions, the named member write and the version schema.
        return string.Empty;
    }

    private static string Struct(StructModel model, RenderContext context)
    {
        // TODO: raw copy, portable transfers and the managed record body.
        return string.Empty;
    }

    private static string Circle(CircleReferenceModel model, RenderContext context)
    {
        // TODO: slot table, reference id and the same-table read.
        return string.Empty;
    }
}
