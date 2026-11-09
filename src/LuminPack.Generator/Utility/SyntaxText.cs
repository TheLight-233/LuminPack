using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Compares the spelling of a syntax node with a known text without printing nodes that cannot match.</summary>
/// <remarks>
/// <see cref="SyntaxNode.ToString"/> prints the node without its outer trivia, so its length is the node's span length.
/// A node whose span length differs from the expected text cannot spell it, and most receivers in generated code are
/// rejected this way before a string is allocated.
/// </remarks>
internal static class SyntaxText
{
    /// <summary>Checks whether a node is spelled exactly as the given text.</summary>
    /// <param name="node">The syntax node.</param>
    /// <param name="text">The expected spelling.</param>
    /// <returns>Whether <c>node.ToString() == text</c>.</returns>
    internal static bool Is(SyntaxNode node, string text) => node.Span.Length == text.Length && node.ToString() == text;
}
