using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator;

/// <summary>A syntax rewriter for passes that replace nodes but never tokens or trivia.</summary>
/// <remarks>
/// The base rewriter rebuilds the leading and trailing trivia lists of every token it visits, only to return the same token
/// when no trivia method is overridden and structured trivia is not visited. Passes deriving from this class return each
/// token unchanged instead, so their output is identical while the per-token walk disappears. A pass that renames
/// tokens, edits trivia or visits structured trivia must derive from <see cref="CSharpSyntaxRewriter"/> directly.
/// </remarks>
internal abstract class NodeRewriter : CSharpSyntaxRewriter
{
    /// <summary>Returns the token unchanged.</summary>
    /// <param name="token">The visited token.</param>
    /// <returns>The same token.</returns>
    public override SyntaxToken VisitToken(SyntaxToken token) => token;
}
