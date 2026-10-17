using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Shares generated cursor storage between mutually exclusive binary reader switch sections.</summary>
internal static class BinaryEntryCursorLocals
{
    /// <summary>Coalesces section-local integer cursors without changing their initialization or state publication.</summary>
    /// <param name="root">The fully expanded serializer source.</param>
    /// <returns>The same operations with independent switches retaining independent cursor slots.</returns>
    internal static SyntaxNode NormalizeRoot(SyntaxNode root) => new ExclusiveCursors(root).Visit(root);

    /// <summary>Rewrites only compiler-marked operation locals in sibling switch sections.</summary>
    private sealed class ExclusiveCursors : CSharpSyntaxRewriter
    {
        private readonly SyntaxNode _root;
        private HashSet<string> _names;
        private int _next;

        /// <summary>Retains emission-local syntax until a switch actually needs a shared cursor name.</summary>
        /// <param name="root">The complete source whose identifiers must remain distinct.</param>
        internal ExclusiveCursors(SyntaxNode root) => _root = root;

        /// <summary>Skips writer, evaluator, JSON, and convenience bodies that cannot contain eligible reader cursors.</summary>
        /// <param name="node">The projected method.</param>
        /// <returns>The unchanged method or its normalized binary reader body.</returns>
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
            => node.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString().EndsWith("LuminPackReader", StringComparison.Ordinal) == true
                ? base.VisitMethodDeclaration(node) : node;

        /// <summary>Gives mutually exclusive cursor lifetimes one stack slot while preserving each section's initializer.</summary>
        /// <param name="node">An expanded tag or slot switch.</param>
        /// <returns>The original switch or a block containing its shared cursor declaration.</returns>
        public override SyntaxNode VisitSwitchStatement(SwitchStatementSyntax node)
        {
            bool binaryReader = node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault()?.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString()
                .EndsWith("LuminPackReader", StringComparison.Ordinal) == true;
            var rewritten = (SwitchStatementSyntax)base.VisitSwitchStatement(node);
            if (!binaryReader) return rewritten;
            var candidates = new List<LocalDeclarationStatementSyntax>();
            foreach (var section in rewritten.Sections)
            {
                var cursors = section.DescendantNodes(descendIntoChildren: child => child is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax or
                    SwitchStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax))
                    .OfType<LocalDeclarationStatementSyntax>().Where(local =>
                        local.Modifiers.Count == 0 && local.Declaration.Type is PredefinedTypeSyntax type && type.Keyword.IsKind(SyntaxKind.IntKeyword) &&
                        local.Declaration.Variables.Count == 1 && local.Declaration.Variables[0] is var variable &&
                        variable.Identifier.GetAnnotations(BinaryEntryInlining.OperationLocalAnnotation).Any() &&
                        variable.Initializer?.Value is InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0,
                            Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "reader" }, Name.Identifier.ValueText: "GetCurrentSpanIndex" } })
                    .ToArray();
                if (cursors.Length == 1) candidates.Add(cursors[0]);
            }
            if (candidates.Count < 2) return rewritten;
            _names ??= new HashSet<string>(_root.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken) &&
                token.ValueText.StartsWith("__luminSwitchCursor", StringComparison.Ordinal)).Select(token => token.ValueText), StringComparer.Ordinal);
            string name;
            do { name = "__luminSwitchCursor" + _next++; } while (!_names.Add(name));
            var replacements = candidates.ToDictionary(local => local.Declaration.Variables[0].Identifier.ValueText, _ => name, StringComparer.Ordinal);
            var body = (SwitchStatementSyntax)new CursorNames(replacements).Visit(rewritten);
            return SyntaxFactory.Block(SyntaxFactory.ParseStatement("int " + name + ";"), body).WithTriviaFrom(node);
        }
    }

    /// <summary>Renames marked cursor tokens and turns their declarations into assignments at the original locations.</summary>
    private sealed class CursorNames : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, string> _names;

        /// <summary>Captures disjoint generated cursor identities and their shared destination.</summary>
        /// <param name="names">Only the selected operation-local names.</param>
        internal CursorNames(Dictionary<string, string> names) => _names = names;

        /// <summary>Preserves initializer evaluation where the original section created its local.</summary>
        /// <param name="node">A local declaration in the switch.</param>
        /// <returns>The original declaration or its equivalent cursor assignment.</returns>
        public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
        {
            if (node.Declaration.Variables.Count == 1 && node.Declaration.Variables[0] is var variable &&
                variable.Identifier.GetAnnotations(BinaryEntryInlining.OperationLocalAnnotation).Any() && _names.TryGetValue(variable.Identifier.ValueText, out string name))
                return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(name), (ExpressionSyntax)Visit(variable.Initializer.Value))).WithTriviaFrom(node);
            return base.VisitLocalDeclarationStatement(node);
        }

        /// <summary>Leaves user members and every unmarked token unchanged, even when their text matches a generated local.</summary>
        /// <param name="token">The current source token.</param>
        /// <returns>The original token or its shared generated cursor identity.</returns>
        public override SyntaxToken VisitToken(SyntaxToken token)
            => token.GetAnnotations(BinaryEntryInlining.OperationLocalAnnotation).Any() && _names.TryGetValue(token.ValueText, out string name)
                ? SyntaxFactory.Identifier(name).WithTriviaFrom(token) : base.VisitToken(token);
    }
}
