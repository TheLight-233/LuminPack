using System.Linq;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Removes container checks only within compiler-marked, completely reserved binary regions.</summary>
internal sealed class ReservedCapacityRegions : CSharpSyntaxRewriter
{
    private int _reserved;
    private int _guaranteedMaxBytes;

    /// <summary>Uses an enclosing complete reservation instead of repeating a nested collection reservation.</summary>
    /// <param name="node">A statically bounded collection's fast and overflow traversals.</param>
    /// <returns>The already reserved traversal, or the original independent reservation.</returns>
    public override SyntaxNode VisitIfStatement(IfStatementSyntax node)
    {
        if (_reserved != 0 && node.Condition is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } &&
            IsWriter(access) && access.Name.Identifier.ValueText == "TryEnsureCapacity") return Visit(node.Statement);
        return base.VisitIfStatement(node);
    }

    /// <summary>Propagates a proven reservation through fully expanded formatter bodies.</summary>
    /// <param name="node">The generated region, including nested inlined operations.</param>
    /// <returns>The same operations with redundant capacity checks removed.</returns>
    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        if (_reserved != 0 && node.HasAnnotations(BinaryCollectionCapacity.ReservationAnnotation))
            return Visit(node.Statements.Last());
        int previousMaximum = _guaranteedMaxBytes;
        var guarantee = node.GetAnnotations(BinaryCollectionCapacity.GuaranteedAnnotation).SingleOrDefault();
        if (guarantee != null) _guaranteedMaxBytes = int.Parse(guarantee.Data, System.Globalization.CultureInfo.InvariantCulture);
        bool reserved = node.HasAnnotations(BinaryCollectionCapacity.ReservedAnnotation);
        if (reserved) _reserved++;
        var rewritten = (BlockSyntax)base.VisitBlock(node);
        if (_guaranteedMaxBytes > 0)
        {
            var statements = new List<StatementSyntax>();
            foreach (var statement in rewritten.Statements)
            {
                statements.Add(statement);
                if (statement is not LocalDeclarationStatementSyntax declaration) continue;
                foreach (var variable in declaration.Declaration.Variables)
                    if (variable.Initializer?.Value is ObjectCreationExpressionSyntax creation &&
                        creation.HasAnnotations(BinaryCollectionCapacity.WriterConstructionAnnotation))
                        statements.Add(SyntaxFactory.ParseStatement(variable.Identifier.ValueText + ".EnsureAdditionalCapacity(" + _guaranteedMaxBytes + ");"));
            }
            rewritten = rewritten.WithStatements(SyntaxFactory.List(statements));
        }
        if (reserved) _reserved--;
        _guaranteedMaxBytes = previousMaximum;
        return rewritten;
    }

    /// <summary>Erases standalone guards and retains cursor publication as an unchecked assignment.</summary>
    /// <param name="node">A compiler-generated writer statement.</param>
    /// <returns>The unchanged operation, its original publication, or an empty statement.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (_reserved != 0 && node.Expression is AssignmentExpressionSyntax assignment &&
            assignment.Right is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax cursorAccess, ArgumentList.Arguments.Count: 1 } cursorCall &&
            IsWriter(cursorAccess) && cursorAccess.Name.Identifier.ValueText == "CheckBuffer" &&
            assignment.Left.IsEquivalentTo(cursorCall.ArgumentList.Arguments[0].Expression))
            return SyntaxFactory.EmptyStatement().WithTriviaFrom(node);
        if (_reserved != 0 && node.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } call && IsWriter(access))
        {
            string name = access.Name.Identifier.ValueText;
            if (name is "EnsureAdditionalCapacity" or "EnsureCollectionCapacity" or "CheckBuffer" or "CompleteReservedRecord") return SyntaxFactory.EmptyStatement().WithTriviaFrom(node);
            if (name == "FlushCurrentIndex")
                return SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, access.Expression, SyntaxFactory.IdentifierName("CurrentIndex")),
                    call.ArgumentList.Arguments[0].Expression)).WithTriviaFrom(node);
        }
        return base.VisitExpressionStatement(node);
    }

    /// <summary>Uses unchecked string copies and the same advancing cores after a complete reservation.</summary>
    /// <param name="node">A compiler-generated state invocation.</param>
    /// <returns>The reserved support operation or the unchanged invocation.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (_reserved != 0 && node.Expression is MemberAccessExpressionSyntax access && IsWriter(access))
        {
            string name = access.Name.Identifier.ValueText;
            if (name == "WriteStringRecord" && node.ArgumentList.Arguments.Count == 1)
                node = node.WithArgumentList(node.ArgumentList.AddArguments(SyntaxFactory.Argument(
                    SyntaxFactory.LiteralExpression(SyntaxKind.TrueLiteralExpression))));
            if (name == "CheckBuffer" && node.ArgumentList.Arguments.Count == 1) return Visit(node.ArgumentList.Arguments[0].Expression);
            if (name == "WriteString") node = node.WithExpression(access.WithName(access.Name is GenericNameSyntax generic
                ? generic.WithIdentifier(SyntaxFactory.Identifier("WriteReservedString")) : SyntaxFactory.IdentifierName("WriteReservedString")));
            if (name == "WriteNullStringHeader") node = node.WithExpression(access.WithName(access.Name is GenericNameSyntax nullProfile
                ? nullProfile.WithIdentifier(SyntaxFactory.Identifier("WriteReservedNullStringHeader")) : SyntaxFactory.IdentifierName("WriteReservedNullStringHeader")));
            if (name == "AdvanceSafe") node = node.WithExpression(access.WithName(SyntaxFactory.IdentifierName("Advance")));
        }
        return base.VisitInvocationExpression(node);
    }

    /// <summary>Recognizes only the generated binary writer receiver, excluding source members with matching method names.</summary>
    /// <param name="access">The generated state member access.</param>
    /// <returns>Whether the access is to the original writer parameter.</returns>
    private static bool IsWriter(MemberAccessExpressionSyntax access) => access.Expression is IdentifierNameSyntax receiver &&
        (receiver.Identifier.ValueText == "writer" || receiver.Identifier.HasAnnotations(BinaryCollectionCapacity.WriterAnnotation));
}
