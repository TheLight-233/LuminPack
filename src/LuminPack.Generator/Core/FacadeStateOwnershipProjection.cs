using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Marks an active convenience operation's reference state as leased while it stays in its thread's idle cache.</summary>
internal sealed class FacadeStateOwnershipProjection : CSharpSyntaxRewriter
{
    private Dictionary<string, string> _leases = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Finds only the cache leases introduced by the generated convenience templates.</summary>
    /// <param name="node">The generated method, including older-target synchronous local helpers.</param>
    /// <returns>The method with independent nested operation state and unchanged cleanup boundaries.</returns>
    public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        var previous = _leases;
        _leases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var variable in node.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            if (GetCache(variable) is string cache) _leases[variable.Identifier.ValueText] = cache;
        var result = base.VisitMethodDeclaration(node);
        _leases = previous;
        return result;
    }

    /// <summary>Leases the thread's cached state in place instead of reading the cache with the original lazy allocation.</summary>
    /// <param name="node">The generated state local.</param>
    /// <returns>A lease that leaves the cached instance in its slot and marks it active until release.</returns>
    /// <remarks>A constructor, formatter, callback or await that reenters a convenience entry finds the active lease and
    /// receives an independent state, so the nested operation never shares the outer operation's reference tables.</remarks>
    public override SyntaxNode VisitVariableDeclarator(VariableDeclaratorSyntax node)
    {
        if (GetCache(node) is not string cache) return base.VisitVariableDeclarator(node);
        string stateType = ((ObjectCreationExpressionSyntax)((AssignmentExpressionSyntax)node.Initializer.Value).Right).Type.ToString();
        return node.WithInitializer(node.Initializer.WithValue(SyntaxFactory.ParseExpression(stateType + ".Lease(ref " + cache + ")")));
    }

    /// <summary>Releases only fully reset state at the original successful or exceptional cleanup boundary.</summary>
    /// <param name="node">The original generated state reset statement.</param>
    /// <returns>The combined reset and lease release of that exact state.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (node.Expression is InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner } access,
                ArgumentList.Arguments.Count: 0
            } invocation && access.Name.Identifier.ValueText == "Reset" && _leases.ContainsKey(owner.Identifier.ValueText))
            return node.WithExpression(invocation.WithExpression(access.WithName(SyntaxFactory.IdentifierName("Release"))));
        return base.VisitExpressionStatement(node);
    }

    /// <summary>Recognizes the three exact operation cache acquisitions emitted by the facade templates.</summary>
    /// <param name="variable">A local declaration from generated facade code.</param>
    /// <returns>The owning cache field, or null for caller-owned and unrelated state.</returns>
    private static string GetCache(VariableDeclaratorSyntax variable)
    {
        if (variable.Initializer?.Value is AssignmentExpressionSyntax assignment &&
            assignment.IsKind(SyntaxKind.CoalesceAssignmentExpression) && assignment.Left is IdentifierNameSyntax cache &&
            cache.Identifier.ValueText is "_threadStaticWriterOptionalState" or "_threadStaticReaderOptionalState" or "_threadStaticEvaluatorOptionalState")
            return cache.Identifier.ValueText;
        return null;
    }
}
