using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Aligns expanded ref-struct temporary lifetimes with the concrete entry's writer or reader.</summary>
internal static class BinaryEntryLocalScopes
{
    /// <summary>Hoists only declarations; their original initializers still run at the exact operation and loop position.</summary>
    /// <param name="fragment">The completed concrete overload contribution.</param>
    /// <param name="metadata">The consumer parse settings.</param>
    /// <returns>The same operation flow with compatible ref-escape scopes.</returns>
    internal static string Normalize(string fragment, MetaInfo metadata)
    {
        const string header = "class __Entry {";
        var root = CSharpSyntaxTree.ParseText(header + fragment + "}", metadata.ParseOptions).GetRoot();
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.Body != null).ToArray();
        var changed = root.ReplaceNodes(methods, (original, current) =>
        {
            var temporaries = original.Body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Where(declaration =>
                (declaration.Declaration.Type.GetFirstToken().IsKind(SyntaxKind.ScopedKeyword) || declaration.Modifiers.Any(token => token.IsKind(SyntaxKind.ScopedKeyword))) &&
                declaration.Declaration.Variables.Count == 1 && declaration.Declaration.Variables[0].Identifier.ValueText.StartsWith("__lp", StringComparison.Ordinal)).ToArray();
            if (temporaries.Length == 0) return current;
            var groups = new Dictionary<BlockSyntax, List<LocalDeclarationStatementSyntax>>();
            var states = new Dictionary<BlockSyntax, LocalDeclarationStatementSyntax>();
            foreach (var temporary in temporaries)
            {
                foreach (var scope in temporary.Ancestors().OfType<BlockSyntax>())
                {
                    var state = scope.Statements.OfType<LocalDeclarationStatementSyntax>().FirstOrDefault(declaration =>
                        declaration.Declaration.Variables.Any(variable => variable.Identifier.ValueText is "reader" or "writer") &&
                        declaration.Declaration.Variables[0].Initializer?.Value is ObjectCreationExpressionSyntax creation &&
                        creation.Type.ToString() is "LuminPackReader" or "LuminPackWriter");
                    if (state == null) continue;
                    if (!groups.TryGetValue(scope, out var group))
                    {
                        groups.Add(scope, group = new List<LocalDeclarationStatementSyntax>());
                        states.Add(scope, state);
                    }
                    group.Add(temporary);
                    break;
                }
            }
            var body = (BlockSyntax)new RelocateDeclarations(groups, states).Visit(original.Body);
            return current.WithBody(body);
        });
        string result = changed.ToFullString();
        return result.Substring(header.Length, result.Length - header.Length - 1);
    }

    /// <summary>Relocates ref-struct storage without moving its evaluation, branches, or loop iteration.</summary>
    private sealed class RelocateDeclarations : CSharpSyntaxRewriter
    {
        private readonly Dictionary<BlockSyntax, (int Position, LocalDeclarationStatementSyntax[] Declarations)> _scopes =
            new Dictionary<BlockSyntax, (int, LocalDeclarationStatementSyntax[])>();
        private readonly HashSet<LocalDeclarationStatementSyntax> _temporaries;
        private readonly HashSet<LocalDeclarationStatementSyntax> _states;

        /// <summary>Captures the original state scope and expanded temporary declarations.</summary>
        /// <param name="groups">Expanded ref-struct locals grouped by their state scope.</param>
        /// <param name="states">The corresponding writer or reader declarations.</param>
        internal RelocateDeclarations(Dictionary<BlockSyntax, List<LocalDeclarationStatementSyntax>> groups,
            Dictionary<BlockSyntax, LocalDeclarationStatementSyntax> states)
        {
            _temporaries = new HashSet<LocalDeclarationStatementSyntax>(groups.Values.SelectMany(group => group));
            _states = new HashSet<LocalDeclarationStatementSyntax>(states.Values);
            foreach (var group in groups)
            {
                var state = states[group.Key];
                var declarations = group.Value.Select(declaration => declaration.WithDeclaration(declaration.Declaration.WithVariables(
                    SyntaxFactory.SingletonSeparatedList(declaration.Declaration.Variables[0].WithInitializer(
                        SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.DefaultLiteralExpression))))))
                    .WithLeadingTrivia(state.GetLeadingTrivia()).WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)).ToArray();
                _scopes.Add(group.Key, (group.Key.Statements.IndexOf(state), declarations));
            }
        }

        /// <summary>Leaves each original initializer at its execution point as an assignment.</summary>
        /// <param name="node">The original declaration.</param>
        /// <returns>The same initializer assignment, or an unchanged statement.</returns>
        public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (_states.Contains(node))
                return node.WithDeclaration(node.Declaration.WithoutLeadingTrivia()).WithModifiers(node.Modifiers.Add(
                    SyntaxFactory.Token(SyntaxKind.ScopedKeyword).WithTrailingTrivia(SyntaxFactory.Space))).WithTriviaFrom(node);
            if (!_temporaries.Contains(node)) return base.VisitLocalDeclarationStatement(node);
            var variable = node.Declaration.Variables[0];
            return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                SyntaxFactory.IdentifierName(variable.Identifier), variable.Initializer.Value)).WithTriviaFrom(node);
        }

        /// <summary>Introduces scoped storage alongside the state whose Core methods may exchange span references.</summary>
        /// <param name="node">The original lexical block.</param>
        /// <returns>The state block with only declarations hoisted.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            var result = (BlockSyntax)base.VisitBlock(node);
            return _scopes.TryGetValue(node, out var scope) ? result.WithStatements(result.Statements.InsertRange(scope.Position, scope.Declarations)) : result;
        }
    }
}
