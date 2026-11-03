using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Stores fixed literal markers directly while retaining the original checked Debug advancement.</summary>
internal sealed class BinaryLiteralWriteProjection : CSharpSyntaxRewriter
{
    private HashSet<string> _writers = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>Selects complete binary write roots only after their capacity and state projections have finished.</summary>
    /// <param name="node">A final generated method.</param>
    /// <returns>The same root with literal stores independent of the scalar helper's inline decision.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        if (node.Identifier.ValueText is not ("RootWrite" or "RootWriteUnion") || node.Body == null)
            return base.VisitMethodDeclaration(node);
        var previous = _writers;
        _writers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in node.DescendantNodes().OfType<ParameterSyntax>())
            if (IsWriter(parameter.Type)) _writers.Add(parameter.Identifier.ValueText);
        foreach (var declaration in node.DescendantNodes().OfType<VariableDeclarationSyntax>())
            if (IsWriter(declaration.Type))
                foreach (var variable in declaration.Variables) _writers.Add(variable.Identifier.ValueText);
        var result = base.VisitMethodDeclaration(node);
        _writers = previous;
        return result;
    }

    /// <summary>Matches the existing binary Writer's canonical emitted type spelling.</summary>
    /// <param name="type">A generated state declaration.</param>
    /// <returns>Whether this declaration owns the managed binary write cursor.</returns>
    private static bool IsWriter(TypeSyntax type) => type?.ToString().Replace("global::", string.Empty)
        .Replace("LuminPack.Core.", string.Empty) == "LuminPackWriter";

    /// <summary>Replaces side-effect-free fixed markers and leaves their original advancement in place.</summary>
    /// <param name="node">The complete original scalar write.</param>
    /// <returns>A fixed store followed by its original advancement, or the untouched operation.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (node.Expression is not InvocationExpressionSyntax
            { Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver,
                Name: IdentifierNameSyntax operation } } call || !_writers.Contains(receiver.Identifier.ValueText))
            return base.VisitExpressionStatement(node);
        ExpressionSyntax value;
        int width = 1;
        var arguments = call.ArgumentList.Arguments;
        if (operation.Identifier.ValueText == "Write" && arguments.Count == 1 && arguments[0].Expression is CastExpressionSyntax
            { Type: PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.ByteKeyword }, Expression: LiteralExpressionSyntax } literal)
            value = literal;
        else if (arguments.Count == 0 && operation.Identifier.ValueText is "WriteNullObjectHeader" or "WriteNullUnionHeader")
            value = SyntaxFactory.ParseExpression("global::LuminPack.Code.LuminPackCode.NullObject");
        else if (arguments.Count == 0 && operation.Identifier.ValueText == "WriteNullCollectionHeader")
        {
            value = SyntaxFactory.ParseExpression("global::LuminPack.Code.LuminPackCode.NullCollection");
            width = 4;
        }
        else if (arguments.Count == 1 && operation.Identifier.ValueText is "WriteObjectHeader" or "WriteUnionHeader" &&
            arguments[0].Expression is LiteralExpressionSyntax)
            value = SyntaxFactory.ParseExpression("unchecked((byte)(" + arguments[0].Expression + "))");
        else return base.VisitExpressionStatement(node);
        var destination = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
            receiver, SyntaxFactory.IdentifierName("Cursor"));
        var store = width == 1 ? SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(
            SyntaxKind.SimpleAssignmentExpression, destination, value)) : SyntaxFactory.ExpressionStatement(
            SyntaxFactory.InvocationExpression(SyntaxFactory.ParseExpression("global::System.Runtime.CompilerServices.Unsafe.WriteUnaligned"),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(new[] {
                    SyntaxFactory.Argument(destination).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)),
                    SyntaxFactory.Argument(value) }))));
        var advance = SyntaxFactory.ParseStatement(receiver.Identifier.ValueText + ".Advance(" + width + ");");
        return SyntaxFactory.Block(store, advance).WithTriviaFrom(node);
    }
}
