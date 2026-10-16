using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Publishes formatter implementations as state-based Serializer overloads with shared legacy bridges.</summary>
internal static class SerializerCoreProjection
{
    /// <summary>Moves concrete operations into the Serializer while retaining per-type cache ownership.</summary>
    /// <param name="source">Legacy emitter output used as the operation and support contract.</param>
    /// <param name="compilation">The unchanged consumer input compilation.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <param name="plans">Generator-instance dependency-guarded operation plans.</param>
    /// <param name="closedRoot">An optional closed generic specialization of the root template.</param>
    /// <returns>State overloads, shared dispatch, and per-type cache declarations.</returns>
    internal static string Project(string source, Compilation compilation, MetaInfo metadata, BinaryOperationPlanCache plans, ITypeSymbol closedRoot = null)
    {
        if (string.IsNullOrEmpty(source)) return source;
        var tree = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions.WithDocumentationMode(DocumentationMode.Parse));
        var root = tree.GetCompilationUnitRoot();
        var container = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(type => type.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal));
        if (container == null) return source;
        SemanticModel model = null;
        var types = CompilationTypeAnalysisCache.GetOrCreate(compilation).FormatterTypesByName;
        var contexts = new Dictionary<string, BinaryEntryInlining>(StringComparer.Ordinal);
        var core = new List<MemberDeclarationSyntax>();
        var dispatch = new List<MemberDeclarationSyntax>();
        var support = new List<MemberDeclarationSyntax>();
        foreach (var member in container.Members)
        {
            if (member is ConstructorDeclarationSyntax constructor)
            {
                dispatch.Add(constructor.WithIdentifier(SyntaxFactory.Identifier("FormatterDispatch")));
                continue;
            }
            if (member is not MethodDeclarationSyntax method || method.ParameterList.Parameters.Count != 2 ||
                !BinaryOperationPlan.IsOperation(method.Identifier.ValueText))
            {
                if (member is ClassDeclarationSyntax helper && helper.Identifier.ValueText.StartsWith("Json_", StringComparison.Ordinal))
                    support.Add(helper.WithMembers(SyntaxFactory.List(helper.Members.Where(item => item is not MethodDeclarationSyntax operation ||
                        operation.Identifier.ValueText is not ("WriteValue" or "ReadValue")))));
                else if (closedRoot == null) support.Add(member);
                continue;
            }
            string operationName = method.Identifier.ValueText;
            string state = StateName(method.ParameterList.Parameters[0].Type.ToString());
            if (!IsState(state)) { support.Add(member); continue; }
            string name = MethodName(method.Identifier.ValueText, state.Contains("Json"));
            if (closedRoot == null && method.Identifier.ValueText is not ("WriteValue" or "ReadValue" or "CalculateOffset"))
            {
                var arguments = SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(method.ParameterList.Parameters.Select((parameter, index) =>
                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName(parameter.Identifier)).WithRefKindKeyword(SyntaxFactory.Token(
                        index == 0 || parameter.Modifiers.Any(SyntaxKind.RefKeyword) ? SyntaxKind.RefKeyword : SyntaxKind.InKeyword)))));
                SimpleNameSyntax target = method.TypeParameterList == null ? SyntaxFactory.IdentifierName(name) :
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier(name), SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(
                        method.TypeParameterList.Parameters.Select(parameter => SyntaxFactory.IdentifierName(parameter.Identifier)))));
                support.Add(method.WithBody(SyntaxFactory.Block(SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(
                    SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer"), target), arguments))))
                    .WithExpressionBody(null).WithSemicolonToken(default));
            }
            var parameters = method.ParameterList.Parameters;
            var first = parameters[0].WithModifiers(SyntaxFactory.TokenList(parameters[0].Modifiers.Where(token => !token.IsKind(SyntaxKind.ThisKeyword))));
            if (metadata.IsNet8) first = first.WithModifiers(first.Modifiers.Insert(0, SyntaxFactory.Token(SyntaxKind.ScopedKeyword)));
            method = method.WithIdentifier(SyntaxFactory.Identifier(name)).WithParameterList(method.ParameterList.WithParameters(parameters.Replace(parameters[0], first)))
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword)));
            string payloadName = parameters[1].Type.ToString();
            if (method.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.ValueText == payloadName) == true)
            {
                dispatch.Add(method);
                continue;
            }
            string typeName = closedRoot == null ? payloadName : FormatterTypeName.Get(closedRoot);
            var type = closedRoot;
            if (type == null && !types.TryGetValue(typeName, out type) && !types.TryGetValue(typeName.TrimEnd('?'), out type))
            {
                // Emitter spellings normally match the canonical index. Bind only exceptional
                // aliases or tuple spellings, preserving the existing semantic fallback.
                model ??= compilation.AddSyntaxTrees(tree).GetSemanticModel(tree);
                typeName = FormatterTypeName.Get(model.GetDeclaredSymbol((MethodDeclarationSyntax)member).Parameters[1].Type);
                types.TryGetValue(typeName, out type);
            }
            if (type != null)
            {
                typeName = FormatterTypeName.Get(type);
                if (closedRoot != null)
                {
                    var payload = method.ParameterList.Parameters[1];
                    bool writing = operationName.StartsWith("Write", StringComparison.Ordinal);
                    payload = payload.WithType(SyntaxFactory.ParseTypeName(typeName + (closedRoot.IsReferenceType ? "?" : string.Empty)))
                        .WithModifiers(SyntaxFactory.TokenList((metadata.IsNet8 ? new[] { SyntaxFactory.Token(SyntaxKind.ScopedKeyword) } : Array.Empty<SyntaxToken>())
                            .Concat(new[] { SyntaxFactory.Token(writing ? SyntaxKind.InKeyword : SyntaxKind.RefKeyword) })));
                    method = method.WithTypeParameterList(null).WithConstraintClauses(default)
                        .WithParameterList(method.ParameterList.WithParameters(method.ParameterList.Parameters.Replace(method.ParameterList.Parameters[1], payload)));
                }
                if (!contexts.TryGetValue(typeName, out var context))
                    contexts.Add(typeName, context = new BinaryEntryInlining(type, compilation, metadata, plans));
                bool write = closedRoot != null ? operationName.StartsWith("Write", StringComparison.Ordinal) : parameters[1].Modifiers.Any(SyntaxKind.InKeyword);
                string operation = operationName + (state.Contains("Json") ? ":json" : string.Empty);
                var expanded = context.ExpandCore(operation);
                if (expanded != null)
                {
                    BlockSyntax body;
                    if (write)
                    {
                        var value = method.ParameterList.Parameters[1];
                        method = method.WithParameterList(method.ParameterList.WithParameters(method.ParameterList.Parameters.Replace(value,
                            value.WithIdentifier(SyntaxFactory.Identifier("input")))));
                        body = SyntaxFactory.Block(SyntaxFactory.ParseStatement("var value = input;"), expanded);
                    }
                    else body = SyntaxFactory.Block(expanded);
                    method = method.WithBody(body).WithExpressionBody(null).WithSemicolonToken(default);
                }
            }
            core.Add(method);
        }
        foreach (var context in contexts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value))
            core.AddRange(context.Layouts);
        if (dispatch.Count != 0)
            core.Add(SyntaxFactory.ClassDeclaration("FormatterDispatch")
                .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.InternalKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
                .WithMembers(SyntaxFactory.List(dispatch)));
        var serializer = SyntaxFactory.ClassDeclaration("LuminPackSerializer")
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword), SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.PartialKeyword)))
            .WithMembers(SyntaxFactory.List(core));
        var namespaces = new List<MemberDeclarationSyntax>();
        if (core.Count != 0) namespaces.Add(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("LuminPack")).AddMembers(serializer));
        if (support.Count != 0) namespaces.Add(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("LuminPack.Generated"))
            .AddMembers(container.WithMembers(SyntaxFactory.List(support))));
        var projected = root.WithMembers(SyntaxFactory.List(namespaces))
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("global::LuminPack.Generated")));
        var projectedTree = CSharpSyntaxTree.Create(projected, metadata.ParseOptions);
        var rewritten = new CallRewriter(compilation.AddSyntaxTrees(projectedTree).GetSemanticModel(projectedTree)).Visit(projectedTree.GetRoot());
        var normalized = (CompilationUnitSyntax)BinaryEntryLocalScopes.NormalizeRoot(rewritten).NormalizeWhitespace("    ", Environment.NewLine);
        normalized = normalized.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia("#nullable enable" + Environment.NewLine).AddRange(normalized.GetLeadingTrivia()));
        return LuminPackSerializerDocumentation.Enhance(normalized, closedRoot != null ? new[] { closedRoot } : contexts.Keys.Select(name => types[name]).ToArray(), compilation, metadata);
    }

    /// <summary>Normalizes the known state spellings produced by the operation emitters.</summary>
    /// <param name="name">A generated state parameter type.</param>
    /// <returns>The fully qualified state name, or the unchanged unknown spelling.</returns>
    private static string StateName(string name)
    {
        if (name.StartsWith("global::", StringComparison.Ordinal)) name = name.Substring(8);
        return name is "LuminPackWriter" or "LuminPackReader" or "LuminPackEvaluator" or "LuminPackJsonWriter" or "LuminPackJsonReader"
            ? "LuminPack.Core." + name : name;
    }

    /// <summary>Rewrites generated formatter calls using the original writer, reader, or evaluator state.</summary>
    /// <param name="source">A projected formatter, facade, or Union source file.</param>
    /// <param name="compilation">The consumer input used to resolve state expressions.</param>
    /// <param name="metadata">Current parse settings.</param>
    /// <returns>Calls to the unified state overloads with unchanged argument storage.</returns>
    internal static string RewriteCalls(string source, Compilation compilation, MetaInfo metadata)
    {
        var tree = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions);
        return new CallRewriter(compilation.AddSyntaxTrees(tree).GetSemanticModel(tree)).Visit(tree.GetRoot()).ToFullString();
    }

    /// <summary>Preserves direct state extension calls through five shared generic forwarding methods.</summary>
    /// <param name="container">The assembly-specific compatibility container.</param>
    /// <param name="metadata">The consumer's reference-safety settings.</param>
    /// <returns>Compatibility declarations without per-type formatter implementations.</returns>
    internal static string Bridges(string container, MetaInfo metadata)
    {
        var builder = new StringBuilder("// <auto-generated/>\n#nullable enable\nnamespace LuminPack.Generated { internal static partial class " + container + " {\n");
        foreach (var operation in new[] { "Serialize", "Deserialize", "Sizeof", "SerializeJson", "DeserializeJson" })
        {
            bool write = operation.StartsWith("Serialize", StringComparison.Ordinal);
            string state = operation == "Sizeof" ? "Evaluator" : operation.EndsWith("Json", StringComparison.Ordinal)
                ? write ? "JsonWriter" : "JsonReader" : write ? "Writer" : "Reader";
            string name = operation == "Sizeof" ? "CalculateOffset" : write ? "WriteValue" : "ReadValue";
            string modifier = write ? "in" : "ref";
            builder.Append("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\npublic static void ")
                .Append(name).Append("<T>(this ref global::LuminPack.Core.LuminPack").Append(state).Append(" state, ")
                .Append(metadata.IsNet8 ? "scoped " : string.Empty).Append(modifier).Append(" T value) => global::LuminPack.LuminPackSerializer.")
                .Append(operation).Append("(ref state, ").Append(modifier).AppendLine(" value);");
        }
        builder.AppendLine("} }");
        return GeneratedSourceDocumentation.Complete(builder.ToString(), metadata.ParseOptions);
    }

    /// <summary>Identifies the runtime operation state structs.</summary>
    /// <param name="name">A bound type name.</param>
    /// <returns>Whether the type is a supported operation context.</returns>
    private static bool IsState(string name) => name is "LuminPack.Core.LuminPackWriter" or "LuminPack.Core.LuminPackReader" or
        "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader" or "LuminPack.Core.LuminPackEvaluator";

    /// <summary>Maps the existing operation semantics to Serializer state overload names.</summary>
    /// <param name="name">The legacy operation.</param>
    /// <param name="json">Whether the state processes JSON.</param>
    /// <returns>The unified overload family.</returns>
    private static string MethodName(string name, bool json) => name switch
    {
        "WriteValue" => json ? "SerializeJson" : "Serialize",
        "ReadValue" => json ? "DeserializeJson" : "Deserialize",
        "CalculateOffset" => "Sizeof",
        "WritePolymorphismValue" => "SerializeUnion",
        "ReadPolymorphismValue" => "DeserializeUnion",
        "WriteValueWithCompress" => "SerializeWithCompress",
        "ReadValueWithCompress" => "DeserializeWithCompress",
        "ReadFreshValue" => "DeserializeFresh",
        _ => name
    };

    /// <summary>Changes calls only when their receiver or explicit first argument is a runtime state struct.</summary>
    private sealed class CallRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;

        /// <summary>Captures the temporary model without retaining it in generated results.</summary>
        /// <param name="model">The current projected file binding.</param>
        internal CallRewriter(SemanticModel model) { _model = model; }

        /// <summary>Projects a generated operation invocation to its state-based overload.</summary>
        /// <param name="node">The original invocation.</param>
        /// <returns>The unified call or the unchanged invocation.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is not MemberAccessExpressionSyntax access || !BinaryOperationPlan.IsOperation(access.Name.Identifier.ValueText))
                return base.VisitInvocationExpression(node);
            if (_model.GetSymbolInfo(node).Symbol is IMethodSymbol { IsExtensionMethod: true } existing &&
                existing.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree != _model.SyntaxTree))
                return base.VisitInvocationExpression(node);
            string state = _model.GetTypeInfo(access.Expression).Type?.ToDisplayString();
            bool reduced = IsState(state);
            if (!reduced && access.Expression.ToString().StartsWith("global::LuminPack.Generated.LuminPackExtensions_", StringComparison.Ordinal) && node.ArgumentList.Arguments.Count != 0)
                state = _model.GetTypeInfo(node.ArgumentList.Arguments[0].Expression).Type?.ToDisplayString();
            if (!IsState(state)) return base.VisitInvocationExpression(node);
            var arguments = (ArgumentListSyntax)Visit(node.ArgumentList);
            if (reduced) arguments = arguments.WithArguments(arguments.Arguments.Insert(0,
                SyntaxFactory.Argument((ExpressionSyntax)Visit(access.Expression)).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword))));
            string name = MethodName(access.Name.Identifier.ValueText, state.Contains("Json"));
            SimpleNameSyntax target = access.Name is GenericNameSyntax generic ? generic.WithIdentifier(SyntaxFactory.Identifier(name)) : SyntaxFactory.IdentifierName(name);
            return node.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer"), target)).WithArgumentList(arguments);
        }
    }
}
