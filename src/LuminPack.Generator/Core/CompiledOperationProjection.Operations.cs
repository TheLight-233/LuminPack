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
    /// <summary>Publishes one implementation with declaration-default and explicit-profile entries.</summary>
    /// <param name="members">The destination operation container.</param>
    /// <param name="method">The existing operation and its complete body.</param>
    /// <param name="owner">The Serializer, dispatch, or lookup declaration owning the method.</param>
    /// <param name="stateEntry">Whether the first parameter is an operation state.</param>
    /// <param name="writer">Whether that state writes values.</param>
    /// <param name="facade">Whether this is a convenience entry rather than a state body.</param>
    private void AppendOperation(List<MemberDeclarationSyntax> members, MethodDeclarationSyntax method,
        TypeDeclarationSyntax owner, bool stateEntry, bool writer, bool facade)
    {
        string identifier = method.Identifier.ValueText;
        bool json = facade ? identifier.Contains("Json") : method.ParameterList.Parameters[0].Type.ToString().Contains("Json");
        if (facade) writer = identifier.StartsWith("Serialize", StringComparison.Ordinal);
        string stateType = facade ? "LuminPack.Core.LuminPack" + (identifier == "Sizeof" ? "Evaluator" : json ? writer ? "JsonWriter" : "JsonReader" : writer ? "Writer" : "Reader") : method.ParameterList.Parameters[0].Type.ToString();
        string stateName = facade ? identifier == "Sizeof" ? "evaluator" : writer ? "writer" : "reader" : method.ParameterList.Parameters[0].Identifier.ValueText;
        string profile = "__LuminProfile";
        var identifiers = new HashSet<string>(method.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText), StringComparer.Ordinal);
        foreach (var ancestor in method.Ancestors().OfType<TypeDeclarationSyntax>())
            if (ancestor.TypeParameterList != null) foreach (var parameter in ancestor.TypeParameterList.Parameters) identifiers.Add(parameter.Identifier.ValueText);
        while (identifiers.Contains(profile)) profile += "_";
        bool payloadGeneric = method.TypeParameterList != null && method.ParameterList.Parameters.Count > 1 &&
            method.ParameterList.Parameters[1].Type is IdentifierNameSyntax payload && method.TypeParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == payload.Identifier.ValueText);
        if (facade) payloadGeneric = method.TypeParameterList != null;
        bool dispatch = owner.Identifier.ValueText == "FormatterDispatch";
        CompiledOptions declaration = _default;
        if (method.ParameterList.Parameters.Count == 2 && _types.TryGetValue(method.ParameterList.Parameters[1].Type.ToString().TrimEnd('?'), out var type))
            declaration = CompiledOptions.Resolve(type, _compilation);
        string defaultProfile = payloadGeneric ? "global::LuminPack.Option.LuminPackDeclaredOptions" : declaration.TypeName;
        var arguments = method.ParameterList.Parameters.Select(parameter => SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier))
            .WithRefKindKeyword(parameter.Modifiers.Any(SyntaxKind.RefKeyword) ? SyntaxFactory.Token(SyntaxKind.RefKeyword) :
                parameter.Modifiers.Any(SyntaxKind.InKeyword) ? SyntaxFactory.Token(SyntaxKind.InKeyword) : default)).ToArray();
        var typeArguments = method.TypeParameterList?.Parameters.Select(parameter => (TypeSyntax)SyntaxFactory.IdentifierName(parameter.Identifier)).ToList() ?? new List<TypeSyntax>();
        typeArguments.Add(SyntaxFactory.ParseTypeName(defaultProfile));
        var target = SyntaxFactory.GenericName(method.Identifier, SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(typeArguments)));
        var call = SyntaxFactory.InvocationExpression(target, SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
        StatementSyntax forward = method.ReturnType is PredefinedTypeSyntax returned && returned.Keyword.IsKind(SyntaxKind.VoidKeyword)
            ? SyntaxFactory.ExpressionStatement(call) : SyntaxFactory.ReturnStatement(call);
        bool fixedBody = stateEntry && !json && !payloadGeneric && !HasProfileWork(method);
        members.Add(fixedBody ? method : method.WithBody(SyntaxFactory.Block(forward)).WithExpressionBody(null).WithSemicolonToken(default)
            .WithModifiers(SyntaxFactory.TokenList(method.Modifiers.Where(modifier => !modifier.IsKind(SyntaxKind.AsyncKeyword))))
            .NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));

        var parameters = (method.TypeParameterList?.Parameters ?? default).Add(SyntaxFactory.TypeParameter(profile));
        var generic = method.WithTypeParameterList(SyntaxFactory.TypeParameterList(parameters));
        if (!method.Modifiers.Any(SyntaxKind.OverrideKeyword))
            generic = generic.AddConstraintClauses(SyntaxFactory.TypeParameterConstraintClause(profile).AddConstraints(
                SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint),
                SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("global::LuminPack.Option.ILuminPackOptions"))));
        SyntaxNode body = method.Body ?? (SyntaxNode)method.ExpressionBody;
        string facadePayload = facade ? method.ParameterList.Parameters.FirstOrDefault(parameter => parameter.Identifier.ValueText == "value")?.Type.ToString() : null;
        bool missingCore = facadePayload != null && (!_types.TryGetValue(facadePayload.TrimEnd('?'), out var facadeType) ||
            !(identifier == "Sizeof" ? LuminPackExtensionGenerator.CanRegisterCalculateFormatter(facadeType, _compilation) :
                LuminPackExtensionGenerator.CanRegisterFormatter(facadeType, _compilation, json)));
        body = new Body(profile, stateType, writer, stateName, dispatch,
            payloadGeneric ? facade ? method.TypeParameterList.Parameters[0].Identifier.ValueText : method.ParameterList.Parameters[1].Type.ToString() :
            missingCore ? facadePayload : null, _default.TypeName).Visit(body);
        if (fixedBody)
        {
            var fixedTarget = method.TypeParameterList == null ? (SimpleNameSyntax)SyntaxFactory.IdentifierName(method.Identifier) :
                SyntaxFactory.GenericName(method.Identifier, SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(method.TypeParameterList.Parameters.Select(parameter => SyntaxFactory.IdentifierName(parameter.Identifier)))));
            var fixedCall = SyntaxFactory.InvocationExpression(fixedTarget, SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments)));
            StatementSyntax statement = method.ReturnType is PredefinedTypeSyntax fixedReturn && fixedReturn.Keyword.IsKind(SyntaxKind.VoidKeyword)
                ? SyntaxFactory.ExpressionStatement(fixedCall) : SyntaxFactory.ReturnStatement(fixedCall);
            generic = generic.WithBody(SyntaxFactory.Block(statement)).WithExpressionBody(null).WithSemicolonToken(default);
        }
        else generic = method.Body != null ? generic.WithBody((BlockSyntax)body) : generic.WithExpressionBody((ArrowExpressionClauseSyntax)body);
        generic = generic.WithLeadingTrivia(generic.GetLeadingTrivia().AddRange(SyntaxFactory.ParseLeadingTrivia(
            "/// <typeparam name=\"" + profile + "\">The stateless profile for this serialization operation.</typeparam>\n")));
        members.Add(generic.NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
    }
}
