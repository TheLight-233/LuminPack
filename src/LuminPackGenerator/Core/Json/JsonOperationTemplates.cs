using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>
/// Exposes a packable type's JSON operation bodies at the operation-container level so concrete
/// JSON entries can be expanded in place. Unlike the discarded per-root lifting path, the shared
/// property-name cache members deliberately stay owned by the per-type <c>Json_*</c> helper: the
/// expanded body references them through that helper instead of copying them into every root.
/// </summary>
internal static class JsonOperationTemplates
{
    /// <summary>Projects one type's generated JSON helper into directly expandable operations.</summary>
    /// <param name="source">This type's temporary formatter source.</param>
    /// <param name="metadata">The consuming parse settings.</param>
    /// <param name="retainHelper">Whether this output owns a helper unavailable from a source-declared root.</param>
    /// <returns>Direct JSON operations that reference the shared per-type helper members.</returns>
    internal static string Prepare(string source, MetaInfo metadata, bool retainHelper)
    {
        var root = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions).GetRoot();
        var container = root.DescendantNodes(node => node is not BaseMethodDeclarationSyntax).OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(type => type.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal));
        if (container == null) return source;
        var helper = container.Members.OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(type => type.Identifier.ValueText.StartsWith("Json_", StringComparison.Ordinal));
        if (helper == null) return source;
        // One generic helper is shared by every closed construction. CLR generic statics keep
        // each closed type's cache separate while the generated helper implementation is emitted once.
        string qualified = "global::LuminPack.Generated." + container.Identifier.ValueText + "." +
            helper.Identifier.ValueText + (helper.TypeParameterList?.ToString() ?? string.Empty);
        var members = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in helper.Members)
        {
            switch (member)
            {
                case FieldDeclarationSyntax field:
                    foreach (var variable in field.Declaration.Variables) members.Add(variable.Identifier.ValueText);
                    break;
                case PropertyDeclarationSyntax property:
                    members.Add(property.Identifier.ValueText);
                    break;
                case MethodDeclarationSyntax method:
                    members.Add(method.Identifier.ValueText);
                    break;
            }
        }
        var rewriter = new MemberQualifier(members, qualified);
        var containerMembers = container.Members.Where(member => member != helper &&
            !(member is MethodDeclarationSyntax method &&
              method.ParameterList.Parameters.FirstOrDefault()?.Type?.ToString().Contains("LuminPackJson") == true)).ToList();
        if (retainHelper)
        {
            containerMembers.Add(helper.WithMembers(SyntaxFactory.List(helper.Members.Where(member =>
                member is not MethodDeclarationSyntax method || method.Identifier.ValueText is not ("WriteValue" or "ReadValue")))));
        }
        foreach (var method in helper.Members.OfType<MethodDeclarationSyntax>())
        {
            if (method.Body == null || method.Identifier.ValueText is not ("WriteValue" or "ReadValue")) continue;
            containerMembers.Add(((MethodDeclarationSyntax)rewriter.Visit(method))
                .WithTypeParameterList(helper.TypeParameterList).WithConstraintClauses(helper.ConstraintClauses));
        }
        return root.ReplaceNode(container, container.WithMembers(SyntaxFactory.List(containerMembers))).ToFullString();
    }

    /// <summary>Extracts the shared JSON cache from a completed operation template.</summary>
    /// <param name="source">The template containing the retained helper.</param>
    /// <param name="metadata">The consuming parse settings.</param>
    /// <param name="compilation">The consuming compilation's profile and type declarations.</param>
    /// <returns>Only the helper and its original enclosing declarations.</returns>
    internal static string Support(string source, MetaInfo metadata, Compilation compilation)
    {
        var root = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions.WithDocumentationMode(DocumentationMode.Parse)).GetRoot();
        var container = root.DescendantNodes(node => node is not BaseMethodDeclarationSyntax).OfType<ClassDeclarationSyntax>()
            .First(type => type.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal));
        var helpers = container.Members.OfType<ClassDeclarationSyntax>()
            .Where(type => type.Identifier.ValueText.StartsWith("Json_", StringComparison.Ordinal));
        var support = root.ReplaceNode(container, container.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(helpers)));
        support = new CompiledOperationProjection(compilation, CompiledOptions.Resolve(null, compilation))
            .Visit(support).NormalizeWhitespace("    ", Environment.NewLine);
        return GeneratedSourceDocumentation.CompleteRoot(support).ToFullString();
    }

    /// <summary>Qualifies every reference to a helper-owned member with the helper's own type identity.</summary>
    private sealed class MemberQualifier : CSharpSyntaxRewriter
    {
        private readonly HashSet<string> _members;
        private readonly ExpressionSyntax _qualified;

        /// <summary>Captures the helper's member names and its fully qualified type identity.</summary>
        /// <param name="members">Shared cache fields, properties, and lookup methods of the helper.</param>
        /// <param name="qualified">The assembly-qualified helper type name.</param>
        internal MemberQualifier(HashSet<string> members, string qualified)
        {
            _members = members;
            _qualified = SyntaxFactory.ParseExpression(qualified);
        }

        /// <summary>Rewrites a helper member reference without disturbing declarations or member-access names.</summary>
        /// <param name="node">The original template identifier.</param>
        /// <returns>The helper-qualified reference or the unchanged identifier.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (!_members.Contains(node.Identifier.ValueText) || node.Parent is QualifiedNameSyntax or AliasQualifiedNameSyntax ||
                node.Parent is MemberAccessExpressionSyntax access && access.Name == node)
                return base.VisitIdentifierName(node);
            return SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, _qualified, node.WithoutTrivia())
                .WithTriviaFrom(node);
        }
    }
}
