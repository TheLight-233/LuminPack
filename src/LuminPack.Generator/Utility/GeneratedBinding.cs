using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Binds generated syntax without the consumer's nullable flow analysis.</summary>
/// <remarks>
/// The generator binds its own templates to resolve symbols, argument types and declared locals. When the consumer enables
/// nullable analysis, every such query also runs the nullable walker over the enclosing generated method, although no
/// generator decision reads a nullable flow state. A leading <c>#nullable disable</c> directive limits binding to the
/// symbols themselves. The directive carries an annotation and is removed before the syntax leaves the binding step.
/// </remarks>
internal static class GeneratedBinding
{
    private static readonly SyntaxAnnotation DirectiveAnnotation = new("LuminPack.BindingNullableContext");
    private static readonly SyntaxTrivia Directive = SyntaxFactory.ParseLeadingTrivia("#nullable disable\n")[0].WithAdditionalAnnotations(DirectiveAnnotation);

    /// <summary>Creates a tree whose generated members bind without nullable flow analysis.</summary>
    /// <param name="root">The generated compilation unit.</param>
    /// <param name="options">The consumer's parse options.</param>
    /// <returns>A tree for semantic binding; its nodes keep the original text after <see cref="Restore"/>.</returns>
    internal static SyntaxTree Tree(CompilationUnitSyntax root, CSharpParseOptions options)
    {
        var first = root.GetFirstToken(includeZeroWidth: true);
        return CSharpSyntaxTree.Create(root.ReplaceToken(first, first.WithLeadingTrivia(first.LeadingTrivia.Insert(0, Directive))), options);
    }

    /// <summary>Removes the binding directive from a rewritten unit.</summary>
    /// <param name="root">A unit produced from a <see cref="Tree"/>.</param>
    /// <returns>The unit without the directive.</returns>
    internal static SyntaxNode Restore(SyntaxNode root)
    {
        var first = root.GetFirstToken(includeZeroWidth: true);
        if (!first.LeadingTrivia.Any(trivia => trivia.HasAnnotation(DirectiveAnnotation)))
            return root;
        return root.ReplaceToken(first, first.WithLeadingTrivia(first.LeadingTrivia.Where(trivia => !trivia.HasAnnotation(DirectiveAnnotation))));
    }
}
