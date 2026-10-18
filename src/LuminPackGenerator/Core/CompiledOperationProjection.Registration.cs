using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

internal sealed partial class CompiledOperationProjection
{
    /// <summary>Retains declaration-owned registration and adds its existing closed-profile counterpart.</summary>
    /// <param name="members">The destination registration container.</param>
    /// <param name="method">The original typed method-pointer registration.</param>
    private static void AppendRegistration(List<MemberDeclarationSyntax> members, MethodDeclarationSyntax method)
    {
        members.Add(method);
        var registration = method.WithTypeParameterList(method.TypeParameterList.AddParameters(SyntaxFactory.TypeParameter("TOptions")))
            .AddConstraintClauses(SyntaxFactory.TypeParameterConstraintClause("TOptions").AddConstraints(
                SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint), SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("global::LuminPack.Option.ILuminPackOptions"))));
        registration = (MethodDeclarationSyntax)new RegistryProfile("TOptions").Visit(registration);
        registration = registration.WithLeadingTrivia(registration.GetLeadingTrivia().AddRange(SyntaxFactory.ParseLeadingTrivia(
            "/// <typeparam name=\"TOptions\">The stateless profile used by these closed method pointers.</typeparam>\n")));
        members.Add(registration.NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
    }

    /// <summary>Targets the closed-profile table without changing the original function-pointer registration signature.</summary>
    private sealed class RegistryProfile : CSharpSyntaxRewriter
    {
        private readonly string _profile;
        /// <summary>Captures the stateless profile parameter.</summary>
        /// <param name="profile">The profile parameter spelling.</param>
        internal RegistryProfile(string profile) => _profile = profile;
        /// <summary>Selects the profile-specific registry while preserving every supplied method pointer.</summary>
        /// <param name="node">An original registration call.</param>
        /// <returns>The same call targeting the closed-profile table.</returns>
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            => node.Expression.ToString() == "global::LuminPack.Code.LuminPackFormatterRegistry"
                ? node.WithExpression(SyntaxFactory.ParseExpression("global::LuminPack.Code.LuminPackFormatterRegistry<" + _profile + ">")) : base.VisitMemberAccessExpression(node);
    }

}
