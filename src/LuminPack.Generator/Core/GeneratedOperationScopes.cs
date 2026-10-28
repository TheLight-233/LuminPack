using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Removes redundant generated block wrappers after operation lifetime normalization.</summary>
internal static class GeneratedOperationScopes
{
    /// <summary>Collapses block chains without exposing their declarations to sibling statements.</summary>
    /// <param name="root">The generated tree after semantic projection and lifetime normalization.</param>
    /// <returns>The same operations with redundant outer block scopes removed.</returns>
    internal static SyntaxNode Rewrite(SyntaxNode root) => new BlockChains().Visit(root);

    /// <summary>Retains the innermost block when its parent contains no other statement.</summary>
    private sealed class BlockChains : CSharpSyntaxRewriter
    {
        /// <summary>Removes one empty outer scope while retaining the contained statement block.</summary>
        /// <param name="node">The generated block to visit.</param>
        /// <returns>The original scope or its sole inner block with both nodes' annotations.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var rewritten = (BlockSyntax)base.VisitBlock(node);
            if (rewritten.Statements.Count != 1 || rewritten.Statements[0] is not BlockSyntax inner ||
                rewritten.ContainsDiagnostics || rewritten.ContainsDirectives ||
                !PlainBrace(rewritten.OpenBraceToken) || !PlainBrace(rewritten.CloseBraceToken) ||
                !PlainBrace(inner.OpenBraceToken) || !PlainBrace(inner.CloseBraceToken) ||
                HasSensitiveStorage(inner)) return rewritten;

            // The inner block remains a block in the same statement position. No
            // declaration enters a sibling scope, and both boundaries execute together.
            return rewritten.CopyAnnotationsTo(inner.WithTriviaFrom(rewritten));
        }

        /// <summary>Checks that removing a brace cannot discard directives, comments, or annotations.</summary>
        /// <param name="token">An opening or closing brace.</param>
        /// <returns>Whether the brace contains only ordinary formatting trivia.</returns>
        private static bool PlainBrace(SyntaxToken token)
            => !token.IsMissing && !token.ContainsAnnotations && token.LeadingTrivia.Concat(token.TrailingTrivia)
                .All(trivia => trivia.IsKind(SyntaxKind.WhitespaceTrivia) || trivia.IsKind(SyntaxKind.EndOfLineTrivia));

        /// <summary>Preserves direct disposal and reference-storage declarations while leaving nested storage blocks intact.</summary>
        /// <param name="block">The inner scope whose outer wrapper could be removed.</param>
        /// <returns>Whether storage or disposal declarations require retaining the wrapper.</returns>
        private static bool HasSensitiveStorage(BlockSyntax block)
            => block.Statements.SelectMany(statement => statement.DescendantNodesAndSelf(descendIntoChildren: node =>
                    node is not BlockSyntax && node is not AnonymousFunctionExpressionSyntax && node is not LocalFunctionStatementSyntax))
                .Any(node => node is StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax or RefTypeSyntax or UsingStatementSyntax ||
                    node is LocalDeclarationStatementSyntax local && (local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword) ||
                        local.Modifiers.Any(token => token.IsKind(SyntaxKind.ScopedKeyword)) || local.Declaration.Type.GetFirstToken().IsKind(SyntaxKind.ScopedKeyword)));
    }
}
