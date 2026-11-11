using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Json;

/// <summary>Renders JSON roots and extensions from a model.</summary>
/// <remarks>Ordinary records go through the direct JSON emitter. Payload kinds whose rendering facts are not modeled yet
/// emit nothing.</remarks>
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
        _ => string.Empty,
    };

    private static string Record(RecordModel model, RenderContext context) => Emit(model, context);

    private static string Struct(StructModel model, RenderContext context) => Emit(model, context);

    private static string Circle(CircleReferenceModel model, RenderContext context) => Emit(model, context);

    private static string Emit(TypeModel model, RenderContext context)
    {
        PayloadRenderModel? direct = RenderModelFactory.Create(model, context.Options);
        return direct == null ? string.Empty : PayloadEmitter.Render(direct, json: true, context.Options.AvailableAliases,
            context.Operations);
    }
}
