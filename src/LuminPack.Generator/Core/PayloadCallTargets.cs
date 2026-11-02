using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Points generated advancing calls of a known payload at that payload's own extension class.</summary>
/// <remarks>Every closed payload declares same-named <c>Write</c> and <c>Read</c> extensions, each in its own class, and
/// extension lookup gathers all of them, so <c>writer.Write&lt;P&gt;(x)</c> makes C# overload resolution examine one candidate
/// per payload in the assembly. A call whose payload the operation binding recorded instead names the payload's class and
/// binds against a handful of candidates. The extensions keep their names and signatures for user code. The pass runs after
/// every other projection, so their pattern matching still sees the extension spelling.</remarks>
internal static class PayloadCallTargets
{
    /// <summary>Retargets the advancing calls of one generated file.</summary>
    /// <param name="root">The completely projected file.</param>
    /// <param name="container">The assembly's extension container identity.</param>
    /// <returns>The file with payload-specific call targets.</returns>
    internal static SyntaxNode Apply(SyntaxNode root, string container) => new Rewriter(container).Visit(root);

    /// <summary>Rewrites known-payload extension calls.</summary>
    private sealed class Rewriter : CSharpSyntaxRewriter
    {
        private readonly string _container;

        /// <summary>Captures the extension container identity.</summary>
        /// <param name="container">The assembly's extension container identity.</param>
        internal Rewriter(string container) => _container = container;

        /// <summary>Calls the payload's extension through its class, passing the receiver as the first argument.</summary>
        /// <param name="node">An invocation.</param>
        /// <returns>The retargeted or unchanged invocation.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            // Only the profile overload is retargeted: a raw scalar payload has no default-profile extension of its own.
            if (result.Expression is not MemberAccessExpressionSyntax { Name: GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } name } access ||
                name.Identifier.ValueText is not ("Write" or "Read") || access.Expression is not IdentifierNameSyntax receiver ||
                result.ArgumentList.Arguments.Count != 1 ||
                node.GetAnnotations(BinaryExtensionProjection.KnownPayloadAnnotation).Select(annotation => annotation.Data).SingleOrDefault() is not { } payload)
                return result;
            return result.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression("global::LuminPack." + BinaryExtensionProjection.Owner(_container, payload)), name).WithTriviaFrom(access))
                .WithArgumentList(result.ArgumentList.WithArguments(result.ArgumentList.Arguments.Insert(0,
                    SyntaxFactory.Argument(receiver.WithoutTrivia()).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)))));
        }
    }
}
