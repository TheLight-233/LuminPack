using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Keeps legacy default calls concrete without adding profile overloads to the public facade.</summary>
internal static class LegacyProfileContainers
{
    /// <summary>Moves one payload's profile bodies into its existing uniquely named entry container.</summary>
    /// <param name="source">The completed legacy facade contribution.</param>
    /// <param name="payload">The exact payload identity.</param>
    /// <param name="metadata">The consumer's language settings.</param>
    /// <returns>The same default entry points and their profile bodies in a nested container.</returns>
    internal static string Apply(string source, string payload, MetaInfo metadata)
    {
        var root = CSharpSyntaxTree.ParseText("class Container {\n" + source + "}",
            metadata.ParseOptions.WithDocumentationMode(DocumentationMode.Parse)).GetCompilationUnitRoot();
        var container = root.Members.OfType<ClassDeclarationSyntax>().Single();
        var methods = container.Members.OfType<MethodDeclarationSyntax>().Where(IsProfileBody).ToArray();
        if (methods.Length == 0)
            return source;
        var names = new HashSet<string>(methods.Select(method => method.Identifier.ValueText), StringComparer.Ordinal);
        string entry = StaticSerializerBinding.EntryName(payload);
        var rewritten = (ClassDeclarationSyntax)new Calls(names, entry).Visit(container);
        var moved = rewritten.Members.OfType<MethodDeclarationSyntax>().Where(IsProfileBody).ToArray();
        var keep = rewritten.Members.Except(moved).ToList();
        var nested = SyntaxFactory.ClassDeclaration(entry).WithModifiers(SyntaxFactory.TokenList(
                SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(moved.Select(method => method.WithModifiers(
                SyntaxFactory.TokenList(method.Modifiers.Select(token => token.IsKind(SyntaxKind.PublicKeyword)
                    ? SyntaxFactory.Token(SyntaxKind.InternalKeyword) : token)))
                .WithLeadingTrivia(method.GetLeadingTrivia().Where(trivia =>
                    !trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) &&
                    !trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia))))));
        keep.Add(nested);
        return string.Join("\n", keep.Select(member => GeneratedSourceIndentation.At(member.NormalizeWhitespace(), 8)));
    }

    /// <summary>Identifies a concrete convenience method with a compiled profile.</summary>
    /// <param name="method">A facade method declaration.</param>
    /// <returns>Whether the method belongs in its payload's entry container.</returns>
    private static bool IsProfileBody(MethodDeclarationSyntax method)
        => method.TypeParameterList?.Parameters.Count == 1 &&
            method.TypeParameterList.Parameters[0].Identifier.ValueText == "TLuminProfile" &&
            method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.RefKeyword) != true &&
            method.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or "SerializeAsync" or "DeserializeAsync";

    /// <summary>Retargets profile convenience calls while preserving state-entry calls.</summary>
    private sealed class Calls : NodeRewriter
    {
        private readonly HashSet<string> _names;
        private readonly string _entry;

        /// <summary>Captures the selected convenience families and the concrete entry owner.</summary>
        /// <param name="names">The profile method names in this contribution.</param>
        /// <param name="entry">The payload-specific entry container.</param>
        internal Calls(HashSet<string> names, string entry)
        {
            _names = names;
            _entry = entry;
        }

        /// <summary>Rewrites a concrete convenience call to its payload container.</summary>
        /// <param name="node">The original invocation.</param>
        /// <returns>The retargeted invocation or the unchanged state call.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            var name = node.Expression switch
            {
                GenericNameSyntax simple => simple,
                MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } access when SyntaxText.Is(access.Expression, "global::LuminPack.LuminPackSerializer") => generic,
                _ => null
            };
            if (name == null || name.TypeArgumentList.Arguments.Count != 1 || !_names.Contains(name.Identifier.ValueText) ||
                node.ArgumentList.Arguments.FirstOrDefault()?.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) == true)
                return visited;
            return visited.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.IdentifierName(_entry), name.WithoutTrivia()).WithTriviaFrom(visited.Expression));
        }
    }
}
