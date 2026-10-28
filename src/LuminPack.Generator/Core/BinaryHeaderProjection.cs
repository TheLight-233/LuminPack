using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Combines legacy header memory operations and their cursor advances into complete operations.</summary>
internal sealed class BinaryHeaderProjection : CSharpSyntaxRewriter
{
    private readonly HashSet<string> _states;

    /// <summary>Captures the binary states declared by the enclosing operation.</summary>
    /// <param name="states">Receiver names owning advancing cursors.</param>
    internal BinaryHeaderProjection(HashSet<string> states) => _states = states;

    /// <summary>Removes advances already performed by a complete header operation.</summary>
    /// <param name="node">The current execution scope.</param>
    /// <returns>The scope using cursor-owned header operations.</returns>
    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        var statements = new List<StatementSyntax>();
        for (int index = 0; index < node.Statements.Count; index++)
        {
            var original = node.Statements[index];
            var call = original is ExpressionStatementSyntax expression ? expression.Expression as InvocationExpressionSyntax
                : original is IfStatementSyntax branch ? branch.Condition.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                    .FirstOrDefault(candidate => Header(candidate, out _, out _) != 0) : null;
            int width = Header(call, out string receiver, out string name);
            if (width != 0)
            {
                bool followingAdvance = index + 1 < node.Statements.Count && IsAdvance(node.Statements[index + 1], receiver, width);
                if (original is IfStatementSyntax conditional)
                {
                    if (followingAdvance)
                        original = conditional.WithStatement(RemoveAdvance(conditional.Statement, receiver, width));
                    else if (name is "TryReadObjectHead" or "TryReadCollectionHead")
                    {
                        var access = (MemberAccessExpressionSyntax)call.Expression;
                        string peek = name == "TryReadObjectHead" ? "PeekObjectHeader" : "PeekCollectionHeader";
                        original = conditional.ReplaceNode(call, call.WithExpression(access.WithName(SyntaxFactory.IdentifierName(peek))));
                    }
                }
                if (followingAdvance) index++;
            }
            statements.Add((StatementSyntax)Visit(original));
        }
        return node.WithStatements(SyntaxFactory.List(statements));
    }

    /// <summary>Identifies former nonadvancing header calls at the current cursor.</summary>
    /// <param name="call">A possible header invocation.</param>
    /// <param name="receiver">Receives the owning state name.</param>
    /// <param name="name">Receives the selected header operation.</param>
    /// <returns>The original separately published header width, or zero.</returns>
    private int Header(InvocationExpressionSyntax call, out string receiver, out string name)
    {
        receiver = name = string.Empty;
        if (call?.Expression is not MemberAccessExpressionSyntax access || !_states.Contains(access.Expression.ToString()) ||
            call.ArgumentList.Arguments.FirstOrDefault() is not { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Position" } } first ||
            !first.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)) return 0;
        receiver = access.Expression.ToString();
        name = access.Name.Identifier.ValueText;
        return name switch
        {
            "WriteNullObjectHeader" or "WriteObjectHeader" or "WriteNullUnionHeader" or "TryReadObjectHead" or "ReadObjectHeader" => 1,
            "WriteCollectionHeader" or "WriteNullCollectionHeader" or "TryReadCollectionHead" => 4,
            _ => 0
        };
    }

    /// <summary>Recognizes a standalone publication of exactly this header's width.</summary>
    /// <param name="statement">The candidate statement.</param>
    /// <param name="receiver">The state owning the header.</param>
    /// <param name="width">The encoded header length.</param>
    /// <returns>Whether the complete operation now owns this publication.</returns>
    private static bool IsAdvance(StatementSyntax statement, string receiver, int width)
    {
        string text = statement.WithoutTrivia().ToString().Replace(" ", string.Empty);
        text = text.Replace("sizeof(int)", "4").Replace("sizeof(byte)", "1").Replace("sizeof(ushort)", "2");
        if (width == 1 && text == receiver + ".Position++;") return true;
        return text == receiver + ".Advance(" + width + ");" || text == receiver + ".Position+=" + width + ";";
    }

    /// <summary>Removes the null branch's duplicate header publication.</summary>
    /// <param name="statement">The null or missing-object branch.</param>
    /// <param name="receiver">The state owning the header.</param>
    /// <param name="width">The header length already consumed.</param>
    /// <returns>The branch retaining assignments, callbacks and its original return.</returns>
    private static StatementSyntax RemoveAdvance(StatementSyntax statement, string receiver, int width)
        => statement is BlockSyntax block ? block.WithStatements(SyntaxFactory.List(block.Statements.Where(item => !IsAdvance(item, receiver, width))))
            : IsAdvance(statement, receiver, width) ? SyntaxFactory.EmptyStatement() : statement;

    /// <summary>Passes headers directly to the state, without an absolute-position temporary.</summary>
    /// <param name="node">A generated header or peek invocation.</param>
    /// <returns>The advancing operation or nonadvancing peek at the current cursor.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax access && _states.Contains(access.Expression.ToString()) &&
            node.ArgumentList.Arguments.FirstOrDefault() is { Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "Position" } } first &&
            first.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) && access.Name.Identifier.ValueText is
                "WriteNullObjectHeader" or "WriteObjectHeader" or "WriteNullUnionHeader" or "WriteCollectionHeader" or "WriteNullCollectionHeader" or
                "WriteObjectReferenceId" or "WriteUnionHeader" or "WriteWideUnionHeader" or "TryReadCollectionHead" or "TryReadObjectHead" or
                "ReadObjectHeader" or "PeekObjectHeader" or "PeekCollectionHeader" or "PeekIsNullObject" or "PeekIsNullCollection")
            return base.VisitInvocationExpression(node.WithArgumentList(node.ArgumentList.WithArguments(node.ArgumentList.Arguments.Remove(first))));
        return base.VisitInvocationExpression(node);
    }
}
