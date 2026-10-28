using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Lowers fixed batches to the single-value memory core without emitting arity-specific runtime APIs.</summary>
internal sealed class SequentialFixedProjection : CSharpSyntaxRewriter
{
    private int _next;

    /// <summary>Splits a fixed batch while retaining its original cursor publication and output storage.</summary>
    /// <param name="node">The generated statement block.</param>
    /// <returns>The same storage operations using only the scalar architecture-aware kernel.</returns>
    public override SyntaxNode VisitBlock(BlockSyntax node)
        => node.WithStatements(RewriteStatements(node.Statements));

    /// <summary>Keeps batch reads and writes inside their selected switch arm.</summary>
    /// <param name="node">The selected section and its original statements.</param>
    /// <returns>The same section with scalar operations at their original execution boundary.</returns>
    public override SyntaxNode VisitSwitchSection(SwitchSectionSyntax node)
        => node.WithStatements(RewriteStatements(node.Statements));

    /// <summary>Lowers only direct statement expressions without hoisting work out of nested branches.</summary>
    /// <param name="originalStatements">The statements in one execution scope.</param>
    /// <returns>The scalar operations and original cursor publications in that scope.</returns>
    private SyntaxList<StatementSyntax> RewriteStatements(SyntaxList<StatementSyntax> originalStatements)
    {
        var statements = new List<StatementSyntax>();
        foreach (var original in originalStatements)
        {
            var statement = (StatementSyntax)Visit(original);
            while (true)
            {
                var call = statement.DescendantNodesAndSelf(descendIntoChildren: child => (child == statement || child is not StatementSyntax) &&
                    child is not LocalFunctionStatementSyntax && child is not AnonymousFunctionExpressionSyntax)
                    .OfType<InvocationExpressionSyntax>().FirstOrDefault(candidate =>
                        candidate.Expression is MemberAccessExpressionSyntax access &&
                        access.Expression.ToString() is "global::LuminPack.Core.LuminPackWriter" or "global::LuminPack.Core.LuminPackReader" &&
                        access.Name.Identifier.ValueText is "Write" or "Read" && candidate.ArgumentList.Arguments.Count > 2 &&
                        candidate.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword));
                if (call == null) break;
                var selected = (MemberAccessExpressionSyntax)call.Expression;
                string identity = "__fixed" + _next++;
                string reference = identity + "Reference", length = identity + "Length";
                statements.Add(SyntaxFactory.ParseStatement("ref byte " + reference + " = ref " + call.ArgumentList.Arguments[0].Expression + ";"));
                statements.Add(SyntaxFactory.ParseStatement("int " + length + " = 0;"));
                var types = selected.Name is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments : default;
                for (int index = 1; index < call.ArgumentList.Arguments.Count; index++)
                {
                    SimpleNameSyntax method = types.Count == call.ArgumentList.Arguments.Count - 1
                        ? SyntaxFactory.GenericName(selected.Name.Identifier, SyntaxFactory.TypeArgumentList(SyntaxFactory.SingletonSeparatedList(types[index - 1])))
                        : SyntaxFactory.IdentifierName(selected.Name.Identifier);
                    var source = SyntaxFactory.Argument(SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.Unsafe.Add(ref " + reference + ", " + length + ")"))
                        .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword));
                    var scalar = call.WithExpression(selected.WithName(method)).WithArgumentList(SyntaxFactory.ArgumentList(
                        SyntaxFactory.SeparatedList(new[] { source, call.ArgumentList.Arguments[index] })));
                    statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.AddAssignmentExpression,
                        SyntaxFactory.IdentifierName(length), scalar)));
                }
                if (statement is ExpressionStatementSyntax expression && expression.Expression == call)
                {
                    statement = null;
                    break;
                }
                statement = statement.ReplaceNode(call, SyntaxFactory.IdentifierName(length));
            }
            if (statement != null) statements.Add(statement);
        }
        return SyntaxFactory.List(statements);
    }
}
