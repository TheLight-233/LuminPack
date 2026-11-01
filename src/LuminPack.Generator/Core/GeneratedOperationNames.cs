using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Restores readable local roles after every identity-sensitive operation projection is complete.</summary>
internal static class GeneratedOperationNames
{
    internal const string RoleAnnotation = "LuminPack.GeneratedLocalRole";

    /// <summary>Assigns deterministic local names without changing executable statements or externally usable declarations.</summary>
    /// <param name="root">The final projected source with compiler-bound local role annotations.</param>
    /// <returns>The same source with readable and collision-free method-local names.</returns>
    internal static SyntaxNode Rewrite(SyntaxNode root) => new MethodNames().Visit(root);

    /// <summary>Marks a local whose unique identity was reserved by a late projection against every existing token.</summary>
    /// <typeparam name="T">The generated syntax node containing its declaration and uses.</typeparam>
    /// <param name="node">The projection result containing the newly introduced local.</param>
    /// <param name="identity">The reserved compiler-only identity.</param>
    /// <param name="role">The local's presentation role.</param>
    /// <returns>The same executable syntax with the new identity marked for final presentation.</returns>
    internal static T MarkLocal<T>(T node, string identity, string role) where T : SyntaxNode
        => node.ReplaceTokens(node.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken) && token.ValueText == identity),
            (token, _) => token.WithAdditionalAnnotations(new SyntaxAnnotation(RoleAnnotation, role)));

    /// <summary>Reserves each method's existing identifiers before choosing presentation names for its private locals.</summary>
    private sealed class MethodNames : CSharpSyntaxRewriter
    {
        /// <summary>Changes only fully marked local identities within one complete operation.</summary>
        /// <param name="node">The original method declaration and unchanged signature.</param>
        /// <returns>The method with renamed locals, or its original syntax when no identity is eligible.</returns>
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Body == null) return node;
            var marked = node.Body.GetAnnotatedTokens(RoleAnnotation).Where(IsLocalDeclaration)
                .GroupBy(token => token.ValueText, StringComparer.Ordinal).Where(group => group.Count() == 1)
                .Select(group => group.First()).Where(token => token.GetAnnotations(RoleAnnotation)
                    .Any(annotation => !string.IsNullOrEmpty(annotation.Data) &&
                        !annotation.Data.StartsWith("__lumin_", StringComparison.Ordinal))).ToArray();
            if (marked.Length == 0) return node;
            var tokens = node.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).ToArray();
            var occurrences = tokens.GroupBy(token => token.ValueText, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
            var observed = new HashSet<string>(node.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
                .SelectMany(call => call.ArgumentList.Arguments)
                .SelectMany(argument => argument.DescendantNodes().OfType<IdentifierNameSyntax>())
                .Select(identifier => identifier.Identifier.ValueText), StringComparer.Ordinal);
            var declarations = marked.Where(token => !observed.Contains(token.ValueText) &&
                occurrences[token.ValueText].All(use => use.HasAnnotations(RoleAnnotation))).ToArray();
            if (declarations.Length == 0) return node;
            var eligible = new HashSet<string>(declarations.Select(token => token.ValueText), StringComparer.Ordinal);
            var reserved = new HashSet<string>(tokens.Where(token => !eligible.Contains(token.ValueText)).Select(token => token.ValueText), StringComparer.Ordinal);
            var replacements = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var declaration in declarations)
            {
                string role = declaration.GetAnnotations(RoleAnnotation).Select(annotation => annotation.Data).FirstOrDefault();
                if (string.IsNullOrEmpty(role) || role.StartsWith("__lumin_", StringComparison.Ordinal)) continue;
                if (role.StartsWith("__lumin", StringComparison.Ordinal) && role.Length > 7)
                    role = char.ToLowerInvariant(role[7]) + role.Substring(8);
                role = role.TrimStart('_');
                bool localFunction = declaration.Parent is LocalFunctionStatementSyntax;
                if (role.Length != 0) role = (localFunction ? char.ToUpperInvariant(role[0]) : char.ToLowerInvariant(role[0])) + role.Substring(1);
                if (!SyntaxFacts.IsValidIdentifier(role) || SyntaxFacts.GetKeywordKind(role) != SyntaxKind.None ||
                    SyntaxFacts.GetContextualKeywordKind(role) != SyntaxKind.None) role = localFunction ? "Local" : "local";
                string name = role;
                int suffix = declaration.Parent is LabeledStatementSyntax ? 0 : 2;
                if (declaration.Parent is LabeledStatementSyntax) name += suffix++;
                while (!reserved.Add(name)) name = role + suffix++;
                replacements.Add(declaration.ValueText, name);
            }
            if (replacements.Count == 0) return node;
            return node.WithBody((BlockSyntax)new LocalNames(replacements).Visit(node.Body));
        }

        /// <summary>Recognizes local declarations while excluding support types, fields and member signatures.</summary>
        /// <param name="token">A marked token from a method body.</param>
        /// <returns>Whether the token declares method-local storage or a control-flow label.</returns>
        private static bool IsLocalDeclaration(SyntaxToken token) => token.Parent switch
        {
            VariableDeclaratorSyntax variable => variable.Identifier == token,
            ForEachStatementSyntax iteration => iteration.Identifier == token,
            SingleVariableDesignationSyntax designation => designation.Identifier == token,
            CatchDeclarationSyntax exception => exception.Identifier == token,
            ParameterSyntax parameter => parameter.Identifier == token,
            LabeledStatementSyntax label => label.Identifier == token,
            LocalFunctionStatementSyntax function => function.Identifier == token,
            _ => false
        };
    }

    /// <summary>Renames only presentation-annotated tokens; user fields and unmarked identifiers retain their exact spelling.</summary>
    private sealed class LocalNames : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, string> _names;

        /// <summary>Captures the deterministic names selected for this one method.</summary>
        /// <param name="names">Compiler-local identities and their final role names.</param>
        internal LocalNames(Dictionary<string, string> names) => _names = names;

        /// <summary>Preserves all annotations and trivia while replacing one proven local identity.</summary>
        /// <param name="token">An original declaration or use token.</param>
        /// <returns>The renamed local token or the original token.</returns>
        public override SyntaxToken VisitToken(SyntaxToken token)
            => token.HasAnnotations(RoleAnnotation) && _names.TryGetValue(token.ValueText, out string name)
                ? token.CopyAnnotationsTo(SyntaxFactory.Identifier(name).WithTriviaFrom(token)).WithoutAnnotations(RoleAnnotation) : base.VisitToken(token);
    }
}
