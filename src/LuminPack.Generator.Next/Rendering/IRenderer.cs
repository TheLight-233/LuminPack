using LuminPack.SourceGenerator.Next.Model;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>Renders one file format of one payload model.</summary>
internal interface IRenderer
{
    /// <summary>Renders a payload.</summary>
    /// <param name="model">The payload model.</param>
    /// <param name="context">The render facts.</param>
    /// <returns>The complete file text, or an empty string when this renderer owns nothing for the model.</returns>
    string Render(TypeModel model, RenderContext context);
}
