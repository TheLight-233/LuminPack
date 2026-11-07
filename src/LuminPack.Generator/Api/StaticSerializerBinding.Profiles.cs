using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

internal sealed partial class StaticSerializerBinding
{
    private SyntaxTree _profileSignatures;

    /// <summary>Retains the profile-only spelling for the payloads that actually use it.</summary>
    /// <param name="signatures">The fixed generic facade signatures.</param>
    /// <param name="options">The consumer's parse options.</param>
    private void BindProfileSignatures(SyntaxTree signatures, CSharpParseOptions options)
    {
        var requests = new HashSet<string>(StringComparer.Ordinal);
        var methods = new List<MethodDeclarationSyntax>();
        var templates = signatures.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(method => method.TypeParameterList?.Parameters.Count == 2 &&
                method.ParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == "value")).ToArray();
        foreach (var tree in _input.SyntaxTrees)
        {
            var model = _binding.GetSemanticModel(tree);
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var name = call.Expression switch { MemberAccessExpressionSyntax access => access.Name, SimpleNameSyntax simple => simple, _ => null };
                if (name is not GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } generic ||
                    !IsProfile(model.GetTypeInfo(generic.TypeArgumentList.Arguments[0]).Type))
                    continue;
                var info = model.GetSymbolInfo(call);
                if (!(info.Symbol is IMethodSymbol selected && IsSerializer(selected)) &&
                    !info.CandidateSymbols.OfType<IMethodSymbol>().Any(IsSerializer))
                    continue;
                var argument = call.ArgumentList.Arguments.FirstOrDefault(item => item.NameColon?.Name.Identifier.ValueText == "value");
                if (argument == null)
                {
                    if (name.Identifier.ValueText is "Deserialize" or "DeserializeJson")
                        argument = call.ArgumentList.Arguments.LastOrDefault(item => item.RefKindKeyword.IsKind(SyntaxKind.RefKeyword));
                    else
                    {
                        int index = name.Identifier.ValueText is "SerializeAsync" or "DeserializeAsync" or "Sizeof" ||
                            call.ArgumentList.Arguments.FirstOrDefault()?.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) == true ? 1 : 0;
                        argument = call.ArgumentList.Arguments.ElementAtOrDefault(index);
                    }
                }
                var payload = argument == null ? null : model.GetTypeInfo(argument.Expression).Type;
                if (payload == null || payload.TypeKind == TypeKind.Error || LuminPackExtensionGenerator.ContainsTypeParameter(payload))
                    continue;
                string key = name.Identifier.ValueText + "|" + TypeName(payload);
                if (!requests.Add(key))
                    continue;
                foreach (var template in templates.Where(template => template.Identifier.ValueText == name.Identifier.ValueText))
                {
                    string parameter = template.TypeParameterList.Parameters[0].Identifier.ValueText;
                    var projected = (MethodDeclarationSyntax)new ProfilePayload(parameter, TypeName(payload)).Visit(template);
                    projected = projected.WithTypeParameterList(projected.TypeParameterList.WithParameters(
                        projected.TypeParameterList.Parameters.RemoveAt(0))).WithConstraintClauses(SyntaxFactory.List(
                            projected.ConstraintClauses.Where(clause => clause.Name.Identifier.ValueText != parameter)));
                    methods.Add(projected);
                }
            }
        }
        if (methods.Count == 0)
            return;
        var root = (CompilationUnitSyntax)signatures.GetRoot();
        var container = root.DescendantNodes().OfType<ClassDeclarationSyntax>().Single();
        root = root.ReplaceNode(container, container.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(methods)));
        _profileSignatures = CSharpSyntaxTree.Create(root, options, "LuminPack.ProfileSignatures.analysis.cs");
        _binding = _binding.AddSyntaxTrees(_profileSignatures);
    }

    /// <summary>Recognizes both concrete and constrained profile types.</summary>
    /// <param name="type">The explicit source type argument.</param>
    /// <returns>Whether it implements the stateless options contract.</returns>
    private static bool IsProfile(ITypeSymbol type)
        => type is INamedTypeSymbol named && named.AllInterfaces.Any(item => item.ToDisplayString() == "LuminPack.Option.ILuminPackOptions") ||
            type is ITypeParameterSymbol parameter && parameter.ConstraintTypes.Any(item =>
                item.ToDisplayString() == "LuminPack.Option.ILuminPackOptions" || IsProfile(item));

    /// <summary>Identifies a compatibility profile signature rather than a payload-generic entry.</summary>
    /// <param name="method">The bound facade method.</param>
    /// <returns>Whether its sole type parameter selects a profile.</returns>
    private bool IsProfileSignature(IMethodSymbol method)
        => _profileSignatures != null && method.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree == _profileSignatures);

    /// <summary>Gets the payload regardless of the public profile spelling.</summary>
    /// <param name="method">The facade operation.</param>
    /// <returns>The payload type.</returns>
    private ITypeSymbol Payload(IMethodSymbol method)
        => IsProfileSignature(method) ? method.Parameters.First(parameter => parameter.Name == "value").Type : method.TypeArguments[0];

    /// <summary>Emits only the profile-only compatibility signatures observed in source.</summary>
    /// <param name="text">The common generated source builder.</param>
    private void EmitProfiles(StringBuilder text)
    {
        if (_profileSignatures == null)
            return;
        var model = _binding.GetSemanticModel(_profileSignatures);
        foreach (var syntax in _profileSignatures.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var method = model.GetDeclaredSymbol(syntax);
            var body = new StringBuilder("{\n");
            if (!method.ReturnsVoid)
                body.Append("return ");
            body.Append("global::LuminPack.LuminPackSerializer.").Append(method.Name).Append('<').Append(TypeName(Payload(method)))
                .Append(", ").Append(method.TypeParameters.Single().Name).Append(">(")
                .Append(string.Join(", ", method.Parameters.Select(parameter => Passing(parameter.RefKind) + Escape(parameter.Name)))).AppendLine(");");
            body.AppendLine("}");
            text.AppendLine(GeneratedSourceIndentation.At(syntax.WithModifiers(SyntaxFactory.TokenList(syntax.Modifiers.Where(token => !token.IsKind(SyntaxKind.ExternKeyword))))
                .WithBody((BlockSyntax)SyntaxFactory.ParseStatement(body.ToString())).WithSemicolonToken(default)
                .NormalizeWhitespace(), 8)).AppendLine();
        }
    }

    /// <summary>Substitutes one payload parameter in a body-free profile declaration.</summary>
    private sealed class ProfilePayload : CSharpSyntaxRewriter
    {
        private readonly string _parameter;
        private readonly string _payload;

        /// <summary>Captures the payload substitution.</summary>
        /// <param name="parameter">The original payload type parameter.</param>
        /// <param name="payload">The closed payload spelling.</param>
        internal ProfilePayload(string parameter, string payload)
        {
            _parameter = parameter;
            _payload = payload;
        }

        /// <summary>Substitutes payload identifiers in a method signature.</summary>
        /// <param name="node">The signature identifier.</param>
        /// <returns>The concrete payload type or original name.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
            => node.Identifier.ValueText == _parameter ? SyntaxFactory.ParseTypeName(_payload).WithTriviaFrom(node) : node;
    }
}
