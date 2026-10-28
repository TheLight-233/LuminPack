using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.CodeEmitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Restores compile-time null-header handling for generic members of closed specializations.</summary>
/// <remarks>An open template resets a reusable generic reference member before its opaque registered formatter reads it.
/// Once a closed specialization substitutes a concrete ordinary object for the member type, the member is read by that
/// object's own expanded body, whose null rule is the ordinary object header: the member is cleared only when the next
/// record is a null object, so a nonnull record refills the existing child. Other member types keep the reset.</remarks>
internal sealed class ClosedMemberResetProjection : CSharpSyntaxRewriter
{
    private const string Reset = "global::LuminPack.LuminPackSerializer.FormatterDispatch.__LuminPackShouldResetMember";
    private readonly IReadOnlyDictionary<string, ITypeSymbol> _types;

    /// <summary>Captures the compilation's canonical formatter types.</summary>
    /// <param name="types">Formatter types by canonical name.</param>
    internal ClosedMemberResetProjection(IReadOnlyDictionary<string, ITypeSymbol> types) => _types = types;

    /// <summary>Replaces the opaque reset of a closed ordinary member with its null-object peek.</summary>
    /// <param name="node">A generated invocation.</param>
    /// <returns>The null-object peek, or the unchanged invocation.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (node.Expression is MemberAccessExpressionSyntax { Name: GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } name } access &&
            name.Identifier.ValueText == "__LuminPackShouldResetMember" && access.Expression.ToString() + "." + name.Identifier.ValueText == Reset &&
            node.ArgumentList.Arguments.Count == 1 && node.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax reader &&
            _types.TryGetValue(name.TypeArgumentList.Arguments[0].ToString().TrimEnd('?'), out var type))
        {
            if (EmitterTypeTraits.UsesOrdinaryObjectHeader(type))
                return SyntaxFactory.InvocationExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    reader.WithoutTrivia(), SyntaxFactory.IdentifierName("PeekIsNullObject"))).WithTriviaFrom(node);
            // A version-tolerant record assigns its own null header to the member, so its storage needs no reset.
            if (IsVersionTolerantObject(type))
                return SyntaxFactory.LiteralExpression(SyntaxKind.FalseLiteralExpression).WithTriviaFrom(node);
        }
        return base.VisitInvocationExpression(node);
    }

    /// <summary>Identifies a closed version-tolerant class whose framing carries its own null record.</summary>
    /// <param name="type">The canonical member type.</param>
    /// <returns>Whether the type is a version-tolerant class without Custom or Union framing.</returns>
    private static bool IsVersionTolerantObject(ITypeSymbol type)
        => type is INamedTypeSymbol { IsReferenceType: true, TypeKind: TypeKind.Class } named &&
           SourceGenerator.TypeMetaChecker.TryCheckPackableAttribute(named) &&
           SourceGenerator.TypeMetaChecker.CheckGeneratorType(named) == GeneratorType.VersionTolerant &&
           !SourceGenerator.TypeMetaChecker.TryCheckUnionAttribute(named) &&
           !LuminPackExtensionGenerator.IsCustomGeneratorType(named);
}
