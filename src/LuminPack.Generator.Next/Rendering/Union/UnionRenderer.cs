using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;

namespace LuminPack.SourceGenerator.Next.Rendering.Union;

/// <summary>Renders the Union root files: root operations, registration, virtual slots and per-case overrides.</summary>
/// <remarks>A Union root publishes one binary file and one JSON file, matching the legacy direct pipeline. Each file owns the
/// root's registration and slots of its format, while the case records keep their own files.</remarks>
internal sealed class UnionRenderer : IRenderer
{
    /// <summary>The shared renderer.</summary>
    internal static readonly UnionRenderer Instance = new UnionRenderer();

    /// <inheritdoc/>
    public string Render(TypeModel model, RenderContext context) => RenderBinary(model, context);

    /// <summary>Renders the binary Union root file.</summary>
    /// <param name="model">The payload model.</param>
    /// <param name="context">The render facts.</param>
    /// <returns>The complete file text, or an empty string when the root is not covered.</returns>
    internal string RenderBinary(TypeModel model, RenderContext context) => Render(model, context, json: false);

    /// <summary>Renders the JSON Union root file.</summary>
    /// <param name="model">The payload model.</param>
    /// <param name="context">The render facts.</param>
    /// <returns>The complete file text, or an empty string when the root is not covered.</returns>
    internal string RenderJson(TypeModel model, RenderContext context) => Render(model, context, json: true);

    /// <summary>Handles render for <c>UnionRenderer</c>.</summary>
    /// <param name="model">The normalized model for this contract.</param>
    /// <param name="context">The context in scope for the operation.</param>
    /// <param name="json">Whether to use the JSON operation.</param>
    /// <returns>The generated C# source.</returns>
    private static string Render(TypeModel model, RenderContext context, bool json)
    {
        if (model is not UnionModel ||
            json && (context.Operations & (GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead)) == 0)
            return string.Empty;
        PayloadRenderModel? direct = RenderModelFactory.Create(model, context.Options);
        return direct == null ? string.Empty : Render(direct, context, json);
    }

    /// <summary>Publishes a Union from the projection shared by its ordinary and checked reads.</summary>
    /// <param name="direct">The projected Union.</param>
    /// <param name="context">The render facts.</param>
    /// <param name="json">Whether to render the JSON roots.</param>
    /// <returns>The generated file.</returns>
    internal static string Render(PayloadRenderModel direct, RenderContext context, bool json)
    {
        if (!direct.UnionRoot || json && (context.Operations & (GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead)) == 0)
            return string.Empty;
        using var writer = new CodeWriter(context.Options.AvailableAliases) { Scoped = direct.Net8 };
        if (json)
            UnionEmitter.EmitJson(direct, writer, context.Operations);
        else
            UnionEmitter.EmitBinary(direct, writer, context.Operations);
        return PublicationLayout.LayOutLines(writer.Complete(json));
    }
}
