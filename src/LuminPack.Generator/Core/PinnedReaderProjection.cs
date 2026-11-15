using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Keeps managed input stationary for the lifetime of a pointer-based compatibility-target reader.</summary>
internal sealed class PinnedReaderProjection : CSharpSyntaxRewriter
{
    private int _next;

    /// <summary>Retains the input storage across all allocation and callback boundaries in a complete read.</summary>
    /// <param name="node">The generated reader lifetime scope.</param>
    /// <returns>The same read inside a pin or a pooled native-input lifetime.</returns>
    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        var block = (BlockSyntax)base.VisitBlock(node);
        for (int index = 0; index < block.Statements.Count; index++)
        {
            if (block.Statements[index] is not LocalDeclarationStatementSyntax declaration || declaration.Declaration.Variables.Count != 1 ||
                declaration.Declaration.Variables[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation ||
                creation.Type.ToString().Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty) is not ("LuminPackReader" or "LuminPackJsonReader") ||
                creation.ArgumentList.Arguments.Count == 0 || !creation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
                continue;
            string input = creation.ArgumentList.Arguments[0].Expression.ToString();
            string identity = "__luminPinnedInput" + _next++;
            string owner = identity + "Owner", span = identity + "Span";
            var arguments = creation.ArgumentList.Arguments;
            var nativeCreation = creation.WithArgumentList(creation.ArgumentList.WithArguments(arguments.Replace(arguments[0],
                arguments[0].WithExpression(SyntaxFactory.IdentifierName(span)))));
            var nativeDeclaration = declaration.ReplaceNode(creation, nativeCreation);
            var statements = new List<StatementSyntax>
            {
                SyntaxFactory.ParseStatement(owner + ".LoadSpan(" + input + ");"),
                SyntaxFactory.ParseStatement("var " + span + " = " + owner + ".GetSpan();")
            };
            statements.Add(nativeDeclaration);
            statements.AddRange(block.Statements.Skip(index + 1));
            var cleanup = SyntaxFactory.FinallyClause(SyntaxFactory.Block(SyntaxFactory.ParseStatement(
                "global::LuminPack.Utility.LuminBufferWriterPool.Return(" + owner + ");")));
            var lifetime = SyntaxFactory.Block(SyntaxFactory.ParseStatement("var " + owner + " = global::LuminPack.Utility.LuminBufferWriterPool.Rent();"),
                SyntaxFactory.TryStatement(SyntaxFactory.Block(statements), default, cleanup));
            return block.WithStatements(SyntaxFactory.List(block.Statements.Take(index).Concat(new[] { lifetime })));
        }
        return block;
    }
}
