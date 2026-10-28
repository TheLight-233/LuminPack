using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Eliminates storage views when every actual member access can use the original field.</summary>
internal sealed class DirectStorageProjection : CSharpSyntaxRewriter
{
    private readonly Compilation _compilation;
    private readonly IReadOnlyDictionary<string, ITypeSymbol> _types;

    /// <summary>Captures accessibility and original type identities from the consumer compilation.</summary>
    /// <param name="compilation">The compilation owning the generated operations.</param>
    internal DirectStorageProjection(Compilation compilation)
    {
        _compilation = compilation;
        _types = CompilationTypeAnalysisCache.GetOrCreate(compilation).FormatterTypesByName;
    }

    /// <summary>Replaces only aliases whose complete uses are accessible, writable fields.</summary>
    /// <param name="node">The original generated storage scope.</param>
    /// <returns>The same scope with unnecessary storage aliases removed.</returns>
    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        var block = (BlockSyntax)base.VisitBlock(node);
        foreach (var declaration in block.Statements.OfType<LocalDeclarationStatementSyntax>().ToArray())
        {
            if (declaration.Declaration.Variables.Count != 1) continue;
            var variable = declaration.Declaration.Variables[0];
            ExpressionSyntax initializer = variable.Initializer?.Value;
            if (initializer is RefExpressionSyntax reference) initializer = reference.Expression;
            if (initializer is not InvocationExpressionSyntax call || call.Expression is not MemberAccessExpressionSyntax
                { Name: GenericNameSyntax { Identifier.ValueText: "As", TypeArgumentList.Arguments.Count: 2 } method } access ||
                !access.Expression.ToString().EndsWith("LuminPackMarshal", StringComparison.Ordinal) || call.ArgumentList.Arguments.Count != 1 ||
                !_types.TryGetValue(method.TypeArgumentList.Arguments[0].ToString(), out var type)) continue;
            string name = variable.Identifier.ValueText;
            if (block.DescendantNodes().OfType<VariableDeclaratorSyntax>().Count(item => item.Identifier.ValueText == name) != 1) continue;
            var uses = block.DescendantNodes().OfType<IdentifierNameSyntax>().Where(identifier => identifier.Identifier.ValueText == name &&
                !(identifier.Parent is MemberAccessExpressionSyntax member && member.Name == identifier)).ToArray();
            if (uses.Any(use => use.Parent is not MemberAccessExpressionSyntax member || member.Expression != use ||
                    !AccessibleField(type, member.Name.Identifier.ValueText))) continue;
            ExpressionSyntax source = call.ArgumentList.Arguments[0].Expression;
            if (source is InvocationExpressionSyntax unwrap && unwrap.Expression is MemberAccessExpressionSyntax
                { Name.Identifier.ValueText: "AsRef" } && unwrap.ArgumentList.Arguments.Count == 1)
                source = unwrap.ArgumentList.Arguments[0].Expression;
            block = block.ReplaceNodes(uses, (original, rewritten) => source.WithTriviaFrom(rewritten));
            var retained = block.Statements.OfType<LocalDeclarationStatementSyntax>()
                .First(item => item.Declaration.Variables.Count == 1 && item.Declaration.Variables[0].Identifier.ValueText == name);
            block = block.RemoveNode(retained, SyntaxRemoveOptions.KeepExteriorTrivia);
        }
        return block;
    }

    /// <summary>Checks actual field accessibility, retaining storage access for readonly and hidden members.</summary>
    /// <param name="type">The original storage type.</param>
    /// <param name="name">The member selected by the generated view.</param>
    /// <returns>Whether the original field can replace the view access.</returns>
    private bool AccessibleField(ITypeSymbol type, string name)
    {
        for (var current = type as INamedTypeSymbol; current != null; current = current.BaseType)
        {
            var members = current.GetMembers(name);
            if (members.Length == 0) continue;
            return members.Length == 1 && members[0] is IFieldSymbol { IsStatic: false, IsReadOnly: false } field &&
                _compilation.IsSymbolAccessibleWithin(field, _compilation.Assembly);
        }
        return false;
    }

}
