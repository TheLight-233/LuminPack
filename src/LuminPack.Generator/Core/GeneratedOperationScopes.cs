using System.Collections.Generic;
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

    /// <summary>Removes plain nested blocks from completed source when they declare nothing in their own scope.</summary>
    /// <param name="root">The completely projected source.</param>
    /// <returns>The same statements in the same order, without braces that add no scope.</returns>
    internal static SyntaxNode RemoveEmptyScopes(SyntaxNode root) => new EmptyScopes().Visit(root);

    /// <summary>Inlines nested statement blocks whose removal cannot change any name's scope or a local's lifetime.</summary>
    private sealed class EmptyScopes : CSharpSyntaxRewriter
    {
        /// <summary>Moves the statements of declaration-free inner blocks into the enclosing block.</summary>
        /// <param name="node">The generated block to visit.</param>
        /// <returns>The block with nested declaration-free blocks inlined.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var rewritten = (BlockSyntax)base.VisitBlock(node);
            if (!rewritten.Statements.Any(statement => statement is BlockSyntax inner && DeclaresNothing(inner)))
                return rewritten;
            var statements = new List<StatementSyntax>(rewritten.Statements.Count);
            foreach (var statement in rewritten.Statements)
            {
                if (statement is BlockSyntax inner && DeclaresNothing(inner))
                    statements.AddRange(inner.Statements);
                else
                    statements.Add(statement);
            }
            return rewritten.WithStatements(SyntaxFactory.List(statements));
        }

        /// <summary>Checks that a block's own scope holds no declaration and that its braces carry nothing.</summary>
        /// <param name="block">The candidate inner block.</param>
        /// <returns>Whether the braces can be removed.</returns>
        private static bool DeclaresNothing(BlockSyntax block)
            => block.AttributeLists.Count == 0 && block.Statements.Count != 0 && !block.ContainsDirectives &&
                !block.GetLeadingTrivia().Concat(block.CloseBraceToken.LeadingTrivia).Concat(block.CloseBraceToken.TrailingTrivia)
                    .Concat(block.OpenBraceToken.TrailingTrivia).Any(trivia => !trivia.IsKind(SyntaxKind.WhitespaceTrivia) && !trivia.IsKind(SyntaxKind.EndOfLineTrivia)) &&
                !block.Statements.Any(statement => statement.DescendantNodesAndSelf(descendIntoChildren: node =>
                    node is not BlockSyntax && node is not AnonymousFunctionExpressionSyntax && node is not LocalFunctionStatementSyntax)
                    .Any(node => node is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax or LabeledStatementSyntax or
                        SingleVariableDesignationSyntax or ParenthesizedVariableDesignationSyntax or DeclarationExpressionSyntax));
    }

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
                HasSensitiveStorage(inner))
                return rewritten;

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
