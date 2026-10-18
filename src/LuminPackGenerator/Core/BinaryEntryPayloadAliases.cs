using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Uses the original local storage for expanded binary reader parameters when its binding is stable.</summary>
internal static class BinaryEntryPayloadAliases
{
    internal const string BindingAnnotation = "LuminPack.ExpandedPayloadBinding";

    /// <summary>Removes writable parameter aliases of uniquely bound generated locals without changing storage or evaluation.</summary>
    /// <param name="body">The complete expanded reader body, including every nested operation.</param>
    /// <returns>The same body with stable local aliases substituted at their marked uses.</returns>
    internal static BlockSyntax Rewrite(BlockSyntax body)
    {
        var declarations = body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Where(local =>
            local.Declaration.Type is RefTypeSyntax reference && reference.ReadOnlyKeyword.RawKind == 0 &&
            local.Declaration.Variables.Count == 1 &&
            local.Declaration.Variables[0].Identifier.HasAnnotations(BindingAnnotation) &&
            local.Declaration.Variables[0].Initializer?.Value is RefExpressionSyntax initializer &&
            StorageIdentifier(initializer.Expression)?.Identifier.HasAnnotations(BinaryEntryInlining.ExpandedIdentityAnnotation) == true).ToArray();
        if (declarations.Length == 0) return body;
        var nodes = body.DescendantNodes().ToArray();
        var names = nodes.Select(node => node switch
            {
                VariableDeclaratorSyntax variable => variable.Identifier.ValueText,
                ParameterSyntax parameter => parameter.Identifier.ValueText,
                ForEachStatementSyntax iteration => iteration.Identifier.ValueText,
                CatchDeclarationSyntax exception => exception.Identifier.ValueText,
                SingleVariableDesignationSyntax designation => designation.Identifier.ValueText,
                LocalFunctionStatementSyntax function => function.Identifier.ValueText,
                _ => null
            }).Where(name => name != null).GroupBy(name => name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var rebound = new HashSet<string>(nodes.OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Right is RefExpressionSyntax)
            .Select(assignment => StorageIdentifier(assignment.Left)?.Identifier.ValueText)
            .Where(name => name != null), StringComparer.Ordinal);
        var observed = new HashSet<string>(nodes.OfType<InvocationExpressionSyntax>()
            .Where(call => call.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
            .SelectMany(call => call.ArgumentList.Arguments)
            .SelectMany(argument => argument.DescendantNodes().OfType<IdentifierNameSyntax>())
            .Select(identifier => identifier.Identifier.ValueText), StringComparer.Ordinal);
        var incomplete = new HashSet<string>(nodes.OfType<IdentifierNameSyntax>()
            .Where(identifier => IsStorageUse(identifier) && !identifier.Identifier.HasAnnotations(BinaryEntryInlining.ExpandedIdentityAnnotation))
            .Select(identifier => identifier.Identifier.ValueText), StringComparer.Ordinal);
        var replacements = new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal);
        var removable = new List<LocalDeclarationStatementSyntax>();
        foreach (var declaration in declarations)
        {
            var variable = declaration.Declaration.Variables[0];
            if (variable.Initializer?.Value is not RefExpressionSyntax reference) continue;
            var identifier = StorageIdentifier(reference.Expression);
            if (identifier == null || !identifier.Identifier.HasAnnotations(BinaryEntryInlining.ExpandedIdentityAnnotation)) continue;
            string alias = variable.Identifier.ValueText;
            string target = identifier.Identifier.ValueText;
            if (alias == target || !names.TryGetValue(target, out int bindings) || bindings != 1 || names[alias] != 1 ||
                rebound.Contains(alias) || rebound.Contains(target) || observed.Contains(alias) || observed.Contains(target) ||
                incomplete.Contains(alias)) continue;
            replacements.Add(alias, reference.Expression);
            removable.Add(declaration);
        }
        if (removable.Count == 0) return body;
        return (BlockSyntax)new StableBindings(replacements).Visit(body.RemoveNodes(removable, SyntaxRemoveOptions.KeepExteriorTrivia));
    }

    /// <summary>Recognizes storage identifiers without evaluating an accessor, index or receiver expression.</summary>
    /// <param name="expression">The original reference target or ref-assignment destination.</param>
    /// <returns>The underlying identifier, or null for an expression requiring evaluation.</returns>
    private static IdentifierNameSyntax StorageIdentifier(ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax || expression is PostfixUnaryExpressionSyntax suppressed && suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression))
            expression = expression is ParenthesizedExpressionSyntax parentheses ? parentheses.Expression : ((PostfixUnaryExpressionSyntax)expression).Operand;
        return expression as IdentifierNameSyntax;
    }

    /// <summary>Excludes member selectors, qualified names and named arguments from local-storage references.</summary>
    /// <param name="identifier">An identifier that may refer to a generated local.</param>
    /// <returns>Whether the syntax position can reference the local storage being substituted.</returns>
    private static bool IsStorageUse(IdentifierNameSyntax identifier)
        => !(identifier.Parent is MemberAccessExpressionSyntax member && member.Name == identifier ||
            identifier.Parent is MemberBindingExpressionSyntax binding && binding.Name == identifier ||
            identifier.Parent is AssignmentExpressionSyntax assignment && assignment.Left == identifier &&
                assignment.Parent is InitializerExpressionSyntax initializer &&
                initializer.Kind() is SyntaxKind.ObjectInitializerExpression or SyntaxKind.WithInitializerExpression ||
            identifier.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax or NameColonSyntax);

    /// <summary>Substitutes only compiler-bound local uses, retaining user members with identical spellings.</summary>
    private sealed class StableBindings : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, ExpressionSyntax> _bindings;

        /// <summary>Captures aliases whose complete expanded lifetime keeps the same storage binding.</summary>
        /// <param name="bindings">The removed aliases and their original local storage.</param>
        internal StableBindings(Dictionary<string, ExpressionSyntax> bindings) => _bindings = bindings;

        /// <summary>Resolves a chain of stable aliases directly to its retained storage local.</summary>
        /// <param name="node">An identifier use in the expanded operation.</param>
        /// <returns>The same identifier or its original storage identity.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (!node.Identifier.HasAnnotations(BinaryEntryInlining.ExpandedIdentityAnnotation) || !IsStorageUse(node))
                return base.VisitIdentifierName(node);
            if (!_bindings.TryGetValue(node.Identifier.ValueText, out var source)) return node;
            var result = (ExpressionSyntax)Visit(source);
            SyntaxNode target = node;
            while (target.Parent is ParenthesizedExpressionSyntax) target = target.Parent;
            if (target.Parent is AssignmentExpressionSyntax assignment && assignment.Left == target ||
                target.Parent is PrefixUnaryExpressionSyntax prefix && prefix.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression ||
                target.Parent is PostfixUnaryExpressionSyntax postfix && postfix.Kind() is SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression or SyntaxKind.SuppressNullableWarningExpression)
                result = StorageIdentifier(result);
            return result.WithTriviaFrom(node);
        }
    }
}
