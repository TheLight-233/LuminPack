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
    /// <param name="method">The original typed delegate registration.</param>
    private void AppendRegistration(List<MemberDeclarationSyntax> members, MethodDeclarationSyntax method)
    {
        members.Add(method);
        var registration = method.WithTypeParameterList(method.TypeParameterList.AddParameters(SyntaxFactory.TypeParameter("TOptions")))
            .AddConstraintClauses(SyntaxFactory.TypeParameterConstraintClause("TOptions").AddConstraints(
                SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint), SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("global::LuminPack.Option.ILuminPackOptions"))));
        var profiled = (MethodDeclarationSyntax)new RegistryProfile("TOptions").Visit(registration);
        // A registry that no profile selects, such as a Union root's member table, makes the profile overload a plain forwarder.
        registration = profiled.IsEquivalentTo(registration) ? Forwarder(registration, method) : profiled;
        registration = registration.WithLeadingTrivia(registration.GetLeadingTrivia().AddRange(SyntaxFactory.ParseLeadingTrivia(
            "/// <typeparam name=\"TOptions\">The stateless profile used by these closed delegates.</typeparam>\n")));
        members.Add(Normalize(registration).WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
    }

    /// <summary>Forwards a profile overload whose body is identical to the original registration.</summary>
    /// <param name="registration">The profile overload.</param>
    /// <param name="original">The original registration.</param>
    /// <returns>The overload with an expression body calling the original registration.</returns>
    private static MethodDeclarationSyntax Forwarder(MethodDeclarationSyntax registration, MethodDeclarationSyntax original)
    {
        string arguments = string.Join(", ", original.ParameterList.Parameters.Select(parameter =>
            (parameter.Modifiers.Any(SyntaxKind.RefKeyword) ? "ref " : parameter.Modifiers.Any(SyntaxKind.OutKeyword) ? "out " : "") +
            parameter.Identifier.ValueText));
        string call = original.Identifier.ValueText + original.TypeParameterList + "(" + arguments + ")";
        return registration.WithBody(null).WithExpressionBody(SyntaxFactory.ArrowExpressionClause(SyntaxFactory.ParseExpression(call)))
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));
    }

    /// <summary>Targets the closed-profile table without changing the original delegate registration signature.</summary>
    private sealed class RegistryProfile : NodeRewriter
    {
        private readonly string _profile;
        /// <summary>Captures the stateless profile parameter.</summary>
        /// <param name="profile">The profile parameter spelling.</param>
        internal RegistryProfile(string profile) => _profile = profile;
        /// <summary>Selects the profile-specific registry while preserving every supplied delegate.</summary>
        /// <param name="node">An original registration call.</param>
        /// <returns>The same call targeting the closed-profile table.</returns>
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
            => node.Expression.ToString() == "global::LuminPack.Code.LuminPackFormatterRegistry"
                ? node.WithExpression(SyntaxFactory.ParseExpression("global::LuminPack.Code.LuminPackFormatterRegistry<" + _profile + ">")) : base.VisitMemberAccessExpression(node);
    }

}
