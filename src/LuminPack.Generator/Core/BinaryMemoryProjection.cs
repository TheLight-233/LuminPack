using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Separates fixed memory kernels from complete advancing Writer and Reader operations.</summary>
internal sealed class BinaryMemoryProjection : CSharpSyntaxRewriter
{
    /// <summary>Projects the former fixed-storage call shapes to one memory-kernel family.</summary>
    /// <param name="node">The original fixed read or write.</param>
    /// <returns>The same architecture-aware memory operation without a redundant state API.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        var rewritten = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
        var access = rewritten.Expression as MemberAccessExpressionSyntax;
        var selected = access?.Name ?? rewritten.Expression as SimpleNameSyntax;
        if (selected == null) return rewritten;
        string name = selected.Identifier.ValueText;
        bool write = name is "WriteUnmanaged" or "WriteUnmanagedWithoutSizeReturn" or "DangerousWriteUnmanaged" or "DangerousWriteUnmanagedWithoutSizeReturn";
        bool read = name is "ReadUnmanaged" or "ReadUnmanagedWithoutSizeReturn" or "DangerousReadUnmanaged" or "DangerousReadUnmanagedWithoutSizeReturn";
        if (!write && !read) return rewritten;
        string receiver = access == null ? string.Empty : access.Expression + ".";
        if (read && rewritten.ArgumentList.Arguments.Count == 0)
            return rewritten.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression("global::LuminPack.Core.LuminPackReader"), Rename(selected, "Read")))
                .WithArgumentList(SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(SyntaxFactory.ParseExpression(receiver + "GetCurrentSpanReference()"))
                        .WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)))));
        var arguments = rewritten.ArgumentList.Arguments;
        string reference;
        if (arguments.Count != 0 && arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
        {
            reference = receiver + "GetSpanReference(" + arguments[0].Expression + ")";
            arguments = arguments.RemoveAt(0);
        }
        else reference = receiver + "GetCurrentSpanReference()";
        arguments = arguments.Insert(0, SyntaxFactory.Argument(SyntaxFactory.ParseExpression(reference)).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)));
        return rewritten.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression("global::LuminPack.Core.LuminPack" + (write ? "Writer" : "Reader")), Rename(selected, write ? "Write" : "Read")))
            .WithArgumentList(rewritten.ArgumentList.WithArguments(arguments));
    }

    /// <summary>Preserves explicit storage type arguments while selecting the compact kernel family.</summary>
    /// <param name="original">The original method identity.</param>
    /// <param name="name">The compact memory operation name.</param>
    /// <returns>The same type argument list under the new name.</returns>
    private static SimpleNameSyntax Rename(SimpleNameSyntax original, string name) => original is GenericNameSyntax generic
        ? generic.WithIdentifier(SyntaxFactory.Identifier(name)) : SyntaxFactory.IdentifierName(name);
}
