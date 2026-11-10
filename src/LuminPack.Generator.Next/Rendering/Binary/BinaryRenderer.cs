using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Binary;

/// <summary>Renders binary roots, facades and payload extensions from a model.</summary>
/// <remarks>Each payload kind has one entry point. Ordinary records go through the direct emitters; Union, reference-tracked
/// and version-tolerant payloads keep their placeholders until their rendering facts are modeled.</remarks>
internal sealed class BinaryRenderer : IRenderer
{
    /// <summary>The shared renderer.</summary>
    internal static readonly BinaryRenderer Instance = new BinaryRenderer();

    /// <inheritdoc/>
    public string Render(TypeModel model, RenderContext context) => model switch
    {
        RecordModel record => Record(record, context),
        StructModel structModel => Struct(structModel, context),
        // A Union root's payload file combines the root record with the dispatch container; UnionRenderer owns it.
        UnionModel => string.Empty,
        _ => string.Empty,
    };

    private static string Record(RecordModel model, RenderContext context) => Emit(model, context);

    private static string Struct(StructModel model, RenderContext context) => Emit(model, context);

    private static string Emit(TypeModel model, RenderContext context)
    {
        PayloadRenderModel? direct = RenderModelFactory.Create(model, context.Options);
        return direct == null ? string.Empty : PayloadEmitter.Render(direct, json: false, context.Options.AvailableAliases,
            (GeneratedOperations)(int)context.Operations);
    }
}
