using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Publishes managed binary Writer cursors without spending a scalar helper's inline budget.</summary>
internal sealed class BinaryWriterPublicationProjection : CSharpSyntaxRewriter
{
    private HashSet<string> _writers = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Finds the Writer parameters and localized aliases within one final generated method.</summary>
    /// <param name="node">The operation and its original local helpers.</param>
    /// <returns>The operation with the same publication points and storage lifetimes.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var previous = _writers;
        _writers = new HashSet<string>(node.DescendantNodes().OfType<ParameterSyntax>()
            .Where(parameter => IsWriter(parameter.Type)).Select(parameter => parameter.Identifier.ValueText), StringComparer.Ordinal);
        foreach (var declaration in node.DescendantNodes().OfType<VariableDeclarationSyntax>().Where(declaration => IsWriter(declaration.Type)))
            foreach (var variable in declaration.Variables) _writers.Add(variable.Identifier.ValueText);
        var result = base.VisitMethodDeclaration(node);
        _writers = previous;
        return result;
    }

    /// <summary>Matches the canonical generated binary state spellings.</summary>
    /// <param name="type">The declaration's type syntax.</param>
    /// <returns>Whether this declaration owns the binary Writer cursor.</returns>
    private static bool IsWriter(TypeSyntax type) => type?.ToString().Replace("global::", string.Empty)
        .Replace("LuminPack.Core.", string.Empty) == "LuminPackWriter";

    /// <summary>Replaces the pure CommitRecords operation with its exact managed-ref assignment.</summary>
    /// <param name="node">The original complete publication statement.</param>
    /// <returns>A publication at the same point, or the untouched statement.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (node.Expression is not InvocationExpressionSyntax
            { Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver,
                Name: IdentifierNameSyntax { Identifier.ValueText: "CommitRecords" } } } call ||
            !_writers.Contains(receiver.Identifier.ValueText) || call.ArgumentList.Arguments.Count != 1 ||
            !call.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
            return base.VisitExpressionStatement(node);
        var destination = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
            receiver, SyntaxFactory.IdentifierName("Cursor"));
        return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
            destination, SyntaxFactory.RefExpression(call.ArgumentList.Arguments[0].Expression))).WithTriviaFrom(node);
    }
}
