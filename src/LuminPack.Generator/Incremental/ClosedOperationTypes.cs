using System.Collections.Generic;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Substitutes closed type arguments without reclassifying the original generic wire operations.</summary>
internal sealed class ClosedOperationTypes : CSharpSyntaxRewriter
{
    private readonly IReadOnlyDictionary<string, string> _arguments;

    /// <summary>Creates a syntax-only type substitution.</summary>
    /// <param name="arguments">Original parameter names and fully qualified closed arguments.</param>
    private ClosedOperationTypes(IReadOnlyDictionary<string, string> arguments) { _arguments = arguments; }

    /// <summary>Produces a canonical operation identity with all original parameters substituted.</summary>
    /// <param name="type">The original bound operation type.</param>
    /// <param name="arguments">Closed arguments for the concrete entry.</param>
    /// <returns>A fully closed type name.</returns>
    internal static string Name(ITypeSymbol type, IReadOnlyDictionary<string, string> arguments)
        => new ClosedOperationTypes(arguments).Visit(SyntaxFactory.ParseTypeName(FormatterTypeName.Get(type))).ToString();

    /// <summary>Replaces only unqualified type-parameter references, preserving namespaces with the same spelling.</summary>
    /// <param name="node">The original type identifier.</param>
    /// <returns>The closed type argument or the unchanged qualified name.</returns>
    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        => node.Parent is not (QualifiedNameSyntax or AliasQualifiedNameSyntax) && _arguments.TryGetValue(node.Identifier.ValueText, out string type)
            ? SyntaxFactory.ParseTypeName(type).WithTriviaFrom(node) : base.VisitIdentifierName(node);
}
