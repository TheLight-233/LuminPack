using System;

namespace LuminPack.SourceGenerator.Next.Rendering;

/// <summary>The single layout layer: final names and final text enter, wrapped text leaves.</summary>
/// <remarks>Layout owns line wrapping only. It never parses a complete file and never rewrites names, so a renderer has exactly
/// one chance to spell its output and the layout cannot change meaning.</remarks>
internal static class LayoutEngine
{
    /// <summary>The preferred maximum line width.</summary>
    internal const int MaxWidth = 140;

    /// <summary>Lays out a completed file.</summary>
    /// <param name="source">The complete source.</param>
    /// <returns>The wrapped source.</returns>
    internal static string Apply(string source)
    {
        // The wrapping rules are ported next; until then the scaffold returns the completed text unchanged.
        return source;
    }
}
