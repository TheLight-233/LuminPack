using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Keeps managed input stationary for the lifetime of a pointer-based compatibility-target reader.</summary>
internal sealed class PinnedReaderProjection : CSharpSyntaxRewriter
{
    private readonly bool _allowUnsafe;
    private bool _pinned;
    private bool _async;
    private int _next;

    /// <summary>Selects a fixed region or reusable native storage according to the consumer's compiler settings.</summary>
    /// <param name="allowUnsafe">Whether the consumer permits a generated fixed region.</param>
    internal PinnedReaderProjection(bool allowUnsafe) => _allowUnsafe = allowUnsafe;

    /// <summary>Marks only synchronous methods that actually own a fixed input region.</summary>
    /// <param name="node">The generated convenience method.</param>
    /// <returns>The same method with its required unsafe context.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        bool previous = _pinned;
        bool previousAsync = _async;
        _pinned = false;
        _async = node.Modifiers.Any(SyntaxKind.AsyncKeyword);
        var result = (MethodDeclarationSyntax)base.VisitMethodDeclaration(node);
        if (_pinned && !result.Modifiers.Any(SyntaxKind.UnsafeKeyword))
            result = result.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        _pinned = previous;
        _async = previousAsync;
        return result;
    }

    /// <summary>Keeps synchronous reader helpers independent from an enclosing async method's unsafe context.</summary>
    /// <param name="node">The synchronous local helper.</param>
    /// <returns>The helper with its own required unsafe context.</returns>
    public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
    {
        bool previous = _pinned;
        bool previousAsync = _async;
        _pinned = false;
        _async = node.Modifiers.Any(SyntaxKind.AsyncKeyword);
        var result = (LocalFunctionStatementSyntax)base.VisitLocalFunctionStatement(node);
        if (_pinned && !result.Modifiers.Any(SyntaxKind.UnsafeKeyword))
            result = result.AddModifiers(SyntaxFactory.Token(SyntaxKind.UnsafeKeyword).WithTrailingTrivia(SyntaxFactory.Space));
        _pinned = previous;
        _async = previousAsync;
        return result;
    }

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
                creation.ArgumentList.Arguments.Count == 0 || !creation.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword)) continue;
            string input = creation.ArgumentList.Arguments[0].Expression.ToString();
            string identity = "__luminPinnedInput" + _next++;
            StatementSyntax lifetime;
            if (_allowUnsafe)
            {
                var pinned = (FixedStatementSyntax)SyntaxFactory.ParseStatement("fixed (byte* " + identity + " = " + input + ") { }");
                string stationary = identity + "Span";
                var arguments = creation.ArgumentList.Arguments;
                var stationaryCreation = creation.WithArgumentList(creation.ArgumentList.WithArguments(arguments.Replace(arguments[0],
                    arguments[0].WithExpression(SyntaxFactory.IdentifierName(stationary)))));
                var statements = new List<StatementSyntax>
                {
                    SyntaxFactory.ParseStatement("var " + stationary + " = new global::System.ReadOnlySpan<byte>(" + identity + ", " + input + ".Length);"),
                    declaration.ReplaceNode(creation, stationaryCreation)
                };
                statements.AddRange(block.Statements.Skip(index + 1));
                lifetime = pinned.WithStatement(SyntaxFactory.Block(statements));
                if (_async)
                    lifetime = SyntaxFactory.UnsafeStatement(SyntaxFactory.Block(lifetime));
                else
                    _pinned = true;
            }
            else
            {
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
                lifetime = SyntaxFactory.Block(SyntaxFactory.ParseStatement("var " + owner + " = global::LuminPack.Utility.LuminBufferWriterPool.Rent();"),
                    SyntaxFactory.TryStatement(SyntaxFactory.Block(statements), default, cleanup));
            }
            return block.WithStatements(SyntaxFactory.List(block.Statements.Take(index).Concat(new[] { lifetime })));
        }
        return block;
    }
}
