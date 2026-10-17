using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Removes redundant generated aliases of the writer's stable cursor storage.</summary>
internal static class BinaryEntryCursorAliases
{
    /// <summary>Uses the original ref-return expression without retaining another interior-reference local.</summary>
    /// <param name="body">The expanded binary writer body with collision-free generated local names.</param>
    /// <returns>The same operation and cursor publication order with fewer byref aliases.</returns>
    internal static BlockSyntax Rewrite(BlockSyntax body)
    {
        if (body == null) return body;
        var declarations = body.DescendantNodes().OfType<LocalDeclarationStatementSyntax>().Where(statement =>
            statement.Declaration.Type is RefTypeSyntax referenceType && referenceType.ReadOnlyKeyword.RawKind == 0 &&
            (referenceType.Type is PredefinedTypeSyntax type && type.Keyword.IsKind(SyntaxKind.IntKeyword) || referenceType.Type.IsVar) &&
            statement.Declaration.Variables.Count == 1 &&
            statement.Declaration.Variables[0].Initializer?.Value is RefExpressionSyntax reference &&
            reference.Expression is InvocationExpressionSyntax invocation &&
            invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "writer" },
                Name.Identifier.ValueText: "GetCurrentSpanOffset"
            } && invocation.ArgumentList.Arguments.Count == 0).ToArray();
        if (declarations.Length == 0) return body;
        var nodes = body.DescendantNodes().ToArray();
        var names = nodes.Select(node => node switch
            {
                VariableDeclaratorSyntax variable => variable.Identifier.ValueText,
                ParameterSyntax parameter => parameter.Identifier.ValueText,
                ForEachStatementSyntax iteration => iteration.Identifier.ValueText,
                CatchDeclarationSyntax exception => exception.Identifier.ValueText,
                SingleVariableDesignationSyntax designation => designation.Identifier.ValueText,
                _ => null
            }).Where(name => name != null).GroupBy(name => name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var rebound = new HashSet<string>(nodes.OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Right is RefExpressionSyntax && assignment.Left is IdentifierNameSyntax)
            .Select(assignment => ((IdentifierNameSyntax)assignment.Left).Identifier.ValueText), StringComparer.Ordinal);
        if (rebound.Contains("writer")) return body;
        var observedNames = new HashSet<string>(nodes.OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
            .SelectMany(invocation => invocation.ArgumentList.Arguments)
            .SelectMany(argument => argument.Expression.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>())
            .Select(identifier => identifier.Identifier.ValueText), StringComparer.Ordinal);
        var removable = declarations.Where(statement =>
        {
            string name = statement.Declaration.Variables[0].Identifier.ValueText;
            return statement.Declaration.Variables[0].Identifier.GetAnnotations(BinaryEntryInlining.ExpandedIdentityAnnotation).Any() &&
                names[name] == 1 && !rebound.Contains(name) && !observedNames.Contains(name);
        }).ToArray();
        if (removable.Length == 0) return body;
        var aliases = new HashSet<string>(removable.Select(statement => statement.Declaration.Variables[0].Identifier.ValueText), StringComparer.Ordinal);
        var withoutAliases = body.RemoveNodes(removable, SyntaxRemoveOptions.KeepExteriorTrivia);
        return (BlockSyntax)new AliasRewriter(aliases).Visit(withoutAliases);
    }

    /// <summary>Substitutes collision-free local expressions while leaving member names untouched.</summary>
    private sealed class AliasRewriter : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _aliases;

        /// <summary>Stores the generated aliases proven unique in this operation.</summary>
        /// <param name="aliases">The alias identifiers eligible for substitution.</param>
        internal AliasRewriter(HashSet<string> aliases) => _aliases = aliases;

        /// <summary>Reuses the writer's original cursor address at each alias use.</summary>
        /// <param name="node">The generated identifier expression.</param>
        /// <returns>The original identifier or the equivalent ref-return invocation.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (!_aliases.Contains(node.Identifier.ValueText) || !node.Identifier.GetAnnotations(BinaryEntryInlining.ExpandedIdentityAnnotation).Any() ||
                node.Parent is MemberAccessExpressionSyntax member && member.Name == node ||
                node.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax or NameColonSyntax)
                return base.VisitIdentifierName(node);
            return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName("writer"), SyntaxFactory.IdentifierName("GetCurrentSpanOffset"))).WithTriviaFrom(node);
        }
    }
}
