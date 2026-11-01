using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace LuminPack.CodeFixes;

/// <summary>Edits source declarations without depending on the generator or runtime assemblies.</summary>
internal static class CodeFixSyntax
{
    internal const string AttributeNamespace = "LuminPack.Attribute.";

    /// <summary>Checks an attribute's bound identity, including aliases and qualified names.</summary>
    /// <param name="attribute">The source attribute.</param>
    /// <param name="model">The owning semantic model.</param>
    /// <param name="names">The supported attribute names without their suffix.</param>
    /// <returns>Whether the attribute belongs to LuminPack and matches a name.</returns>
    internal static bool IsAttribute(AttributeSyntax attribute, SemanticModel model, params string[] names)
    {
        var constructor = model.GetSymbolInfo(attribute).Symbol as IMethodSymbol;
        string? identity = constructor?.ContainingType.ToDisplayString();
        return names.Any(name => identity == AttributeNamespace + name + "Attribute");
    }

    /// <summary>Adds a fully qualified attribute while keeping declaration comments.</summary>
    /// <param name="declaration">The declaration to annotate.</param>
    /// <param name="name">The attribute name without its suffix.</param>
    /// <returns>The annotated declaration.</returns>
    internal static MemberDeclarationSyntax AddAttribute(MemberDeclarationSyntax declaration, string name)
    {
        var attribute = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(
            SyntaxFactory.Attribute(SyntaxFactory.ParseName("global::" + AttributeNamespace + name + "Attribute"))));
        return declaration.WithoutLeadingTrivia().AddAttributeLists(attribute)
            .WithLeadingTrivia(declaration.GetLeadingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Removes only selected attributes, preserving other attributes and surrounding trivia.</summary>
    /// <param name="declaration">The original declaration.</param>
    /// <param name="attributes">The attributes to remove.</param>
    /// <returns>The edited declaration.</returns>
    internal static MemberDeclarationSyntax RemoveAttributes(MemberDeclarationSyntax declaration, IEnumerable<AttributeSyntax> attributes)
    {
        var targets = attributes.ToArray();
        var nodes = declaration.AttributeLists.SelectMany(list => list.Attributes.All(targets.Contains)
            ? new SyntaxNode[] { list }
            : list.Attributes.Where(targets.Contains).Cast<SyntaxNode>()).ToArray();
        return declaration.RemoveNodes(nodes, SyntaxRemoveOptions.KeepExteriorTrivia)!
            .WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Replaces access modifiers while retaining other modifiers and comments.</summary>
    /// <param name="declaration">The original declaration.</param>
    /// <param name="accessibility">The desired access keyword.</param>
    /// <returns>The edited declaration.</returns>
    internal static MemberDeclarationSyntax WithAccessibility(MemberDeclarationSyntax declaration, SyntaxKind accessibility)
    {
        var modifiers = declaration.WithoutLeadingTrivia().Modifiers;
        var access = modifiers.Where(token => token.IsKind(SyntaxKind.PublicKeyword) || token.IsKind(SyntaxKind.PrivateKeyword) ||
            token.IsKind(SyntaxKind.ProtectedKeyword) || token.IsKind(SyntaxKind.InternalKeyword)).ToArray();
        var trivia = SyntaxFactory.TriviaList(access.SelectMany(token => token.LeadingTrivia.Concat(token.TrailingTrivia)));
        var replacement = declaration.WithModifiers(SyntaxFactory.TokenList(modifiers.Except(access))
            .Insert(0, SyntaxFactory.Token(accessibility).WithTrailingTrivia(trivia)));
        return replacement.WithLeadingTrivia(declaration.GetLeadingTrivia()).WithAdditionalAnnotations(Formatter.Annotation);
    }

    /// <summary>Rewrites declarations grouped by document, including nested replacements.</summary>
    /// <param name="solution">The original solution.</param>
    /// <param name="nodes">The declarations to edit.</param>
    /// <param name="rewrite">The transformation receiving both original and rewritten nodes.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The edited solution.</returns>
    internal static async Task<Solution> RewriteAsync(Solution solution, IEnumerable<SyntaxNode> nodes,
        Func<SyntaxNode, SyntaxNode, SyntaxNode> rewrite, CancellationToken cancellationToken)
    {
        foreach (var group in nodes.Distinct().GroupBy(node => node.SyntaxTree))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var document = solution.GetDocument(group.Key);
            if (document == null) continue;
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            if (root == null) continue;
            solution = solution.WithDocumentSyntaxRoot(document.Id, root.ReplaceNodes(group, rewrite));
        }
        return solution;
    }

    /// <summary>Collects all partial declarations of a type and its containing types.</summary>
    /// <param name="type">The type requiring generated partial declarations.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The editable declarations.</returns>
    internal static IEnumerable<SyntaxNode> TypeDeclarations(INamedTypeSymbol type, CancellationToken cancellationToken)
    {
        for (INamedTypeSymbol? current = type.OriginalDefinition; current != null; current = current.ContainingType)
        {
            foreach (var reference in current.DeclaringSyntaxReferences)
            {
                var node = reference.GetSyntax(cancellationToken);
                if (node is TypeDeclarationSyntax) yield return node;
            }
        }
    }

    /// <summary>Collects both halves of a partial method so their modifiers remain consistent.</summary>
    /// <param name="method">The reported method declaration.</param>
    /// <param name="model">The owning semantic model.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The source method declarations.</returns>
    internal static IEnumerable<SyntaxNode> MethodDeclarations(MethodDeclarationSyntax method, SemanticModel model, CancellationToken cancellationToken)
    {
        var symbol = model.GetDeclaredSymbol(method, cancellationToken);
        if (symbol == null) return new SyntaxNode[] { method };
        return new[] { symbol, symbol.PartialDefinitionPart, symbol.PartialImplementationPart }
            .Where(part => part != null).SelectMany(part => part!.DeclaringSyntaxReferences)
            .Select(reference => reference.GetSyntax(cancellationToken)).Distinct();
    }
}
