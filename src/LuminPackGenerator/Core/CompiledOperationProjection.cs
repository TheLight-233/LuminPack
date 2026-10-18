using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Propagates compiled profiles through concrete, recursive and ordinary virtual operations.</summary>
internal sealed class CompiledOperationProjection : CSharpSyntaxRewriter
{
    private readonly Compilation _compilation;
    private readonly CompiledOptions _default;
    private readonly IReadOnlyDictionary<string, ITypeSymbol> _types;
    internal const string UnionRootAnnotation = "LuminPack.CompiledUnionRoot";

    /// <summary>Captures current type identities only for this syntax projection.</summary>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <param name="fallback">The declaration profile for a facade or generic fallback.</param>
    internal CompiledOperationProjection(Compilation compilation, CompiledOptions fallback)
    {
        _compilation = compilation; _default = fallback;
        _types = CompilationTypeAnalysisCache.GetOrCreate(compilation).FormatterTypesByName;
    }

    /// <summary>Moves each complete operation body into a profile-generic operation, retaining thin declaration-default entries.</summary>
    /// <param name="node">A Serializer, dispatch, JSON cache or generated Union declaration.</param>
    /// <returns>The original declarations with one complete body per operation.</returns>
    public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        => RewriteMembers(node);

    /// <summary>Preserves interface Union dispatch while adding a compiled document profile to its operation slots.</summary>
    /// <param name="node">The generated partial Union interface.</param>
    /// <returns>The original interface with compiled operation slots.</returns>
    public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        => RewriteMembers(node);

    /// <summary>Preserves value-type implementations of interface Union slots.</summary>
    /// <param name="node">The generated partial value type.</param>
    /// <returns>The same value type with ordinary compiled slots.</returns>
    public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        => RewriteMembers(node);

    /// <summary>Projects one generated operation container without changing its type or virtual dispatch policy.</summary>
    /// <param name="node">The original class or interface declaration.</param>
    /// <returns>The same declaration with its compiled operation family.</returns>
    private TypeDeclarationSyntax RewriteMembers(TypeDeclarationSyntax node)
    {
        var members = new List<MemberDeclarationSyntax>();
        foreach (var member in node.Members)
        {
            if (member is not MethodDeclarationSyntax method || method.ParameterList.Parameters.Count == 0 ||
                method.Body == null && method.ExpressionBody == null)
            { members.Add((MemberDeclarationSyntax)Visit(member)); continue; }
            string identifier = method.Identifier.ValueText;
            if (identifier == "Register" && method.TypeParameterList?.Parameters.Count == 1)
            {
                members.Add(method);
                var registration = method.WithTypeParameterList(method.TypeParameterList.AddParameters(SyntaxFactory.TypeParameter("TOptions")))
                    .AddConstraintClauses(SyntaxFactory.TypeParameterConstraintClause("TOptions").AddConstraints(
                        SyntaxFactory.ClassOrStructConstraint(SyntaxKind.StructConstraint), SyntaxFactory.TypeConstraint(SyntaxFactory.ParseTypeName("global::LuminPack.Option.ILuminPackOptions"))));
                registration = (MethodDeclarationSyntax)new RegistryProfile("TOptions").Visit(registration);
                registration = registration.WithLeadingTrivia(registration.GetLeadingTrivia().AddRange(SyntaxFactory.ParseLeadingTrivia(
                    "/// <typeparam name=\"TOptions\">The stateless profile used by these closed method pointers.</typeparam>\n")));
                members.Add(registration.NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
                continue;
            }
            bool stateEntry = TryState(method.ParameterList.Parameters[0].Type?.ToString(), out bool writer);
            bool facade = !stateEntry && IsFacadeOperation(identifier);
            if (!stateEntry && !facade) { members.Add((MemberDeclarationSyntax)Visit(member)); continue; }
            bool json = facade ? identifier.Contains("Json") : method.ParameterList.Parameters[0].Type.ToString().Contains("Json");
            if (facade) writer = identifier.StartsWith("Serialize", StringComparison.Ordinal);
            string stateType = facade ? "LuminPack.Core.LuminPack" + (identifier == "Sizeof" ? "Evaluator" : json ? writer ? "JsonWriter" : "JsonReader" : writer ? "Writer" : "Reader") : method.ParameterList.Parameters[0].Type.ToString();
            string stateName = facade ? identifier == "Sizeof" ? "evaluator" : writer ? "writer" : "reader" : method.ParameterList.Parameters[0].Identifier.ValueText;
            bool serializer = identifier is "SerializeJson" or "DeserializeJson" or "Serialize" or "Deserialize" or "Sizeof" or
                "SerializeUnion" or "DeserializeUnion" or "DeserializeFresh" or "SerializeWithCompress" or "DeserializeWithCompress";
            bool slot = identifier.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) ||
                identifier.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) || identifier.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal);
            bool lookup = identifier == "TryReadOrderedJsonProperty";
            if (!serializer && !slot && !lookup && !facade) { members.Add(method); continue; }

            if (slot)
            {
                AppendUnionSlots(members, method, node, writer);
                continue;
            }

            string profile = "__LuminProfile";
            var identifiers = new HashSet<string>(method.DescendantTokens().Where(token => token.IsKind(SyntaxKind.IdentifierToken)).Select(token => token.ValueText), StringComparer.Ordinal);
            foreach (var ancestor in method.Ancestors().OfType<TypeDeclarationSyntax>())
                if (ancestor.TypeParameterList != null) foreach (var parameter in ancestor.TypeParameterList.Parameters) identifiers.Add(parameter.Identifier.ValueText);
            while (identifiers.Contains(profile)) profile += "_";
            bool payloadGeneric = method.TypeParameterList != null && method.ParameterList.Parameters.Count > 1 &&
                method.ParameterList.Parameters[1].Type is IdentifierNameSyntax payload && method.TypeParameterList.Parameters.Any(parameter => parameter.Identifier.ValueText == payload.Identifier.ValueText);
            if (facade) payloadGeneric = method.TypeParameterList != null;
            bool dispatch = node.Identifier.ValueText == "FormatterDispatch";
            CompiledOptions declaration = _default;
            if (method.ParameterList.Parameters.Count == 2 && _types.TryGetValue(method.ParameterList.Parameters[1].Type.ToString().TrimEnd('?'), out var type))
                declaration = CompiledOptions.Resolve(type, _compilation);
            if (slot && _compilation.GetTypeByMetadataName(ContainingTypeName(node)) is INamedTypeSymbol owner)
                declaration = CompiledOptions.Resolve(owner, _compilation);
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
        return node.WithMembers(SyntaxFactory.List(members));
    }

    /// <summary>Recognizes convenience operations sharing the same compiled profile as their state core.</summary>
    /// <param name="name">The generated operation name.</param>
    /// <returns>Whether the facade can propagate a stateless profile.</returns>
    private static bool IsFacadeOperation(string name) => name is "Serialize" or "Deserialize" or "Sizeof" or "SerializeJson" or "DeserializeJson" or
        "SerializeAsync" or "DeserializeAsync" or "SerializeCore" or "DeserializeCore" or "SerializeJsonCore" or "DeserializeJsonCore";

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

    /// <summary>Uses ordinary virtual slots for each declared profile, avoiding generic virtual method dispatch.</summary>
    /// <param name="members">The generated type's member list.</param>
    /// <param name="method">The original hidden virtual or interface slot.</param>
    /// <param name="owner">The original generated partial type.</param>
    /// <param name="writer">Whether this slot writes a binary or JSON payload.</param>
    private void AppendUnionSlots(List<MemberDeclarationSyntax> members, MethodDeclarationSyntax method, TypeDeclarationSyntax owner, bool writer)
    {
        string operation = method.Identifier.ValueText.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) ? "__LuminPackUnionSerializeJson_" :
            method.Identifier.ValueText.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal) ? "__LuminPackUnionCalculateOffset_" : "__LuminPackUnionSerialize_";
        string suffix = method.Identifier.ValueText.Substring(operation.Length);
        var root = _types.Values.OfType<INamedTypeSymbol>().FirstOrDefault(type =>
            (type.IsAbstract || type.TypeKind == TypeKind.Interface) && LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(type.OriginalDefinition) == suffix)?.OriginalDefinition;
        bool json = method.ParameterList.Parameters[0].Type.ToString().Contains("Json");
        var declared = (root == null ? _default : CompiledOptions.Resolve(root, _compilation, importedScope: false)).UnionProfile(json);
        var profiles = CompiledOptions.UnionProfiles(json).ToArray();
        string state = method.ParameterList.Parameters[0].Identifier.ValueText;
        foreach (var profile in profiles)
        {
            string name = method.Identifier.ValueText + (profile.Identity == declared.Identity ? "" : "_Options_" + profile.Identity);
            var body = new Body(profile.TypeName, method.ParameterList.Parameters[0].Type.ToString(), writer, state, false, null).Visit(method.Body ?? (SyntaxNode)method.ExpressionBody);
            var slot = method.WithIdentifier(SyntaxFactory.Identifier(name));
            slot = method.Body != null ? slot.WithBody((BlockSyntax)body) : slot.WithExpressionBody((ArrowExpressionClauseSyntax)body);
            members.Add(slot.NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
        }
        if (method.Modifiers.Any(SyntaxKind.OverrideKeyword) || owner is not InterfaceDeclarationSyntax && !method.Modifiers.Any(SyntaxKind.VirtualKeyword)) return;
        string rootName = root == null ? owner.Identifier.ValueText : FormatterTypeName.Get(root);
        var source = new System.Text.StringBuilder();
        string profileParameter = "__LuminProfile";
        while (owner.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Any(type =>
            type.TypeParameterList?.Parameters.Any(parameter => parameter.Identifier.ValueText == profileParameter) == true)) profileParameter += "_";
        source.AppendLine("/// <summary>Selects the compiled wire profile through an ordinary virtual slot.</summary>");
        source.AppendLine("/// <typeparam name=\"" + profileParameter + "\">The stateless wire profile.</typeparam>");
        source.AppendLine("/// <param name=\"writer\">The existing operation state.</param>");
        source.AppendLine("/// <param name=\"value\">The original Union reference.</param>");
        source.AppendLine("[global::System.ComponentModel.EditorBrowsable(global::System.ComponentModel.EditorBrowsableState.Never)]");
        source.AppendLine("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]");
        source.Append("public static void ").Append(method.Identifier.ValueText).Append("_Compiled<").Append(profileParameter).Append(">(ref ").Append(method.ParameterList.Parameters[0].Type).Append(" writer, ")
            .Append(rootName).Append(" value) where ").Append(profileParameter).AppendLine(" : struct, global::LuminPack.Option.ILuminPackOptions {");
        foreach (var profile in profiles)
        {
            string name = method.Identifier.ValueText + (profile.Identity == declared.Identity ? "" : "_Options_" + profile.Identity);
            source.Append("if (default(").Append(profileParameter).Append(").StringEncoding == (global::LuminPack.Option.LuminPackStringEncoding)").Append(profile.Encoding);
            if (!json) source.Append(" && default(").Append(profileParameter).Append(").StringRecording == (global::LuminPack.Option.LuminPackStringRecording)").Append(profile.Recording);
            source.Append(") { value.").Append(name).AppendLine("(ref writer); return; }");
        }
        source.AppendLine("global::LuminPack.Code.LuminPackExceptionHelper.ThrowInvalidOperationException(\"The compiled profile has an invalid string encoding or recording mode.\"); }");
        members.Add(SyntaxFactory.ParseMemberDeclaration(source.ToString()).NormalizeWhitespace().WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
    }

    /// <summary>Recognizes operation-state spellings without inspecting unrelated methods or function-pointer parameters.</summary>
    /// <param name="type">The generated first parameter's type spelling.</param>
    /// <param name="writer">Receives whether the state writes JSON.</param>
    /// <returns>Whether this is an actual Writer, Reader or Evaluator parameter.</returns>
    private static bool TryState(string type, out bool writer)
    {
        string name = type?.Replace("global::", string.Empty).Replace("LuminPack.Core.", string.Empty);
        writer = name is "LuminPackJsonWriter" or "LuminPackWriter";
        return writer || name is "LuminPackJsonReader" or "LuminPackReader" or "LuminPackEvaluator";
    }

    /// <summary>Keeps fixed-value native bodies in their existing nongeneric method.</summary>
    /// <param name="method">The existing complete state operation.</param>
    /// <returns>Whether an ambient profile can affect its generated work.</returns>
    private static bool HasProfileWork(MethodDeclarationSyntax method) => method.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(call =>
        call.Expression is MemberAccessExpressionSyntax access &&
        (access.Name.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) ||
         access.Name.Identifier.ValueText.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal) ||
         access.Name.Identifier.ValueText is "Serialize" or "Deserialize" or "Sizeof" ||
         access.Expression.ToString() == "global::LuminPack.Code.LuminPackFormatterRegistry" ||
         (CompiledOptionProjection.WriterMethods.Contains(access.Name.Identifier.ValueText) || CompiledOptionProjection.ReaderMethods.Contains(access.Name.Identifier.ValueText) ||
          CompiledOptionProjection.EvaluatorMethods.Contains(access.Name.Identifier.ValueText)) &&
         !(access.Name is GenericNameSyntax generic && generic.TypeArgumentList.Arguments[0].ToString().Contains("LuminPackFixedOptions"))));

    /// <summary>Builds a metadata lookup name for a generated partial Union type.</summary>
    /// <param name="type">The current generated declaration.</param>
    /// <returns>Its namespace and nested metadata identity.</returns>
    private static string ContainingTypeName(TypeDeclarationSyntax type)
    {
        string name = string.Join("+", type.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().Reverse().Select(owner => owner.Identifier.ValueText +
            (owner.TypeParameterList == null ? "" : "`" + owner.TypeParameterList.Parameters.Count)));
        var space = type.Ancestors().OfType<NamespaceDeclarationSyntax>().FirstOrDefault();
        return space == null ? name : space.Name + "." + name;
    }

    /// <summary>Selects document-generic kernels and preserves existing virtual and registry boundaries.</summary>
    private sealed class Body : CSharpSyntaxRewriter
    {
        private string _profile;
        private readonly bool _writer;
        private readonly string _state;
        private readonly bool _dispatch;
        private readonly string _payload;
        private readonly string _stateType;
        private readonly string _fallback;
        /// <summary>Captures one generic operation context.</summary>
        /// <param name="profile">The collision-free generated profile parameter.</param>
        /// <param name="stateType">The actual binary, JSON or evaluator state family.</param>
        /// <param name="writer">Whether the state writes a binary or JSON payload.</param>
        /// <param name="state">The original state receiver identifier.</param>
        /// <param name="dispatch">Whether known types select their declaration default.</param>
        /// <param name="payload">The generic fallback's payload parameter, when present.</param>
        /// <param name="fallback">The consuming assembly's complete profile for unknown registered types.</param>
        internal Body(string profile, string stateType, bool writer, string state, bool dispatch, string payload, string fallback = "global::LuminPack.Option.LuminPackDefaultOptions")
        { _profile = profile; _stateType = stateType; _writer = writer; _state = state; _dispatch = dispatch; _payload = payload; _fallback = fallback; }

        /// <summary>Retains an explicitly declared binary member profile through opaque and virtual boundaries.</summary>
        /// <param name="node">The original expanded operation scope.</param>
        /// <returns>The same scope with its statically selected profile.</returns>
        public override SyntaxNode VisitBlock(BlockSyntax node)
        {
            string enclosing = _profile;
            string scope = node.GetAnnotations(BinaryEntryInlining.OptionScopeAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
            if (scope != null && !_stateType.Contains("Json")) _profile = scope;
            try { return base.VisitBlock(node); }
            finally { _profile = enclosing; }
        }
        /// <summary>Replaces configuration constants with the enclosing document's compile-time profile.</summary>
        /// <param name="node">A generated option expression.</param>
        /// <returns>The generic profile expression or unchanged member access.</returns>
        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            if (node.Name.Identifier.ValueText == "Option" && node.Expression.ToString() == _state)
                return SyntaxFactory.ParseExpression("default(" + _profile + ")").WithTriviaFrom(node);
            if (node.Expression is DefaultExpressionSyntax && node.Name.Identifier.ValueText is
                "StringEncoding" or "StringRecording" or "MaxJsonDepth" or "ValidateJsonSyntax" or "StandardFormat")
                return node.WithExpression(SyntaxFactory.DefaultExpression(SyntaxFactory.IdentifierName(_profile)));
            return base.VisitMemberAccessExpression(node);
        }
        /// <summary>Propagates profiles through kernels, concrete overloads, recursive calls and virtual Union slots.</summary>
        /// <param name="node">An existing generated call.</param>
        /// <returns>The same operation bound to its document profile.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var result = (InvocationExpressionSyntax)base.VisitInvocationExpression(node);
            if (result.Expression is SimpleNameSyntax local && IsFacadeOperation(local.Identifier.ValueText))
            {
                var localArguments = local is GenericNameSyntax localGeneric ? localGeneric.TypeArgumentList.Arguments : default;
                if (localArguments.Count == 0 && _payload != null) localArguments = localArguments.Add(SyntaxFactory.ParseTypeName(_payload));
                localArguments = localArguments.Add(SyntaxFactory.ParseTypeName(_profile));
                return result.WithExpression(SyntaxFactory.GenericName(local.Identifier, SyntaxFactory.TypeArgumentList(localArguments)));
            }
            if (result.Expression is not MemberAccessExpressionSyntax access) return result;
            string name = access.Name.Identifier.ValueText;
            if (name == "TryGetValue" && access.Expression.ToString() == "global::LuminPack.Code.LuminPackFormatterRegistry")
                return result.WithExpression(access.WithExpression(SyntaxFactory.ParseExpression("global::LuminPack.Code.LuminPackFormatterRegistry<" +
                    (_dispatch ? "global::LuminPack.Option.LuminPackFallbackOptions<" + _profile + ", " + _fallback + ">" : _profile) + ">"))
                    .WithName(SyntaxFactory.IdentifierName(_stateType.Contains("Json") ? "TryGetJsonValue" : "TryGetValue")));
            bool json = _stateType.Contains("Json");
            string stateType = _stateType.Replace("global::", string.Empty);
            if (!stateType.StartsWith("LuminPack.Core.", StringComparison.Ordinal)) stateType = "LuminPack.Core." + stateType;
            bool state = access.Expression.ToString() == _state || access.Expression is IdentifierNameSyntax receiver &&
                receiver.Identifier.GetAnnotations(OperationTemplateRewriter.StateAnnotation).Any(annotation => annotation.Data == stateType);
            if (state && !_dispatch && _payload != null && name is "WriteValue" or "ReadValue" or "CalculateOffset")
            {
                string operation = name == "CalculateOffset" ? "Sizeof" : name == "WriteValue" ? json ? "SerializeJson" : "Serialize" : json ? "DeserializeJson" : "Deserialize";
                var forwarded = result.ArgumentList.Arguments.Insert(0, SyntaxFactory.Argument(access.Expression).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)));
                return result.WithExpression(SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer." + operation + "<" + _payload + ", " + _profile + ">"))
                    .WithArgumentList(result.ArgumentList.WithArguments(forwarded));
            }
            var kernels = json ? _writer ? CompiledOptionProjection.JsonWriterMethods : CompiledOptionProjection.JsonReaderMethods :
                _stateType.Contains("Evaluator") ? CompiledOptionProjection.EvaluatorMethods : _writer ? CompiledOptionProjection.WriterMethods : CompiledOptionProjection.ReaderMethods;
            bool kernel = state && kernels.Contains(name);
            if (kernel && !json && access.Name is GenericNameSyntax fixedProfile && fixedProfile.TypeArgumentList.Arguments[0].ToString().Contains("LuminPackFixedOptions")) return result;
            bool formatter = access.Expression.ToString().EndsWith("FormatterDispatch", StringComparison.Ordinal);
            bool serializer = (IsFacadeOperation(name) || name is "SerializeUnion" or "DeserializeUnion" or "DeserializeFresh" or "SerializeWithCompress" or "DeserializeWithCompress") &&
                (access.Expression.ToString().Contains("LuminPackSerializer") || formatter);
            if (formatter && name is "__LuminPackJsonEncoding" or "__LuminPackJsonReadBoundary") serializer = true;
            bool union = name.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) || name.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) ||
                name.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal);
            bool boundary = union || name == "TryReadOrderedJsonProperty";
            if (!kernel && !serializer && !boundary) return result;
            var arguments = access.Name is GenericNameSyntax generic ? generic.TypeArgumentList.Arguments : default;
            if (serializer && !formatter && !_dispatch && arguments.Count == 0 && _payload != null) arguments = arguments.Add(SyntaxFactory.ParseTypeName(_payload));
            if (union)
            {
                string root = node.GetAnnotations(UnionRootAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
                if (root != null)
                    return SyntaxFactory.ParseExpression(root + "." + name + "_Compiled<" + _profile + ">(" + node.ArgumentList.Arguments[0] + ", " + access.Expression + ")").WithTriviaFrom(node);
            }
            if (formatter && arguments.Count == 0 && _payload != null) arguments = arguments.Add(SyntaxFactory.ParseTypeName(_payload));
            if (kernel && access.Name is GenericNameSyntax) arguments = arguments.RemoveAt(0);
            arguments = kernel ? arguments.Insert(0, SyntaxFactory.IdentifierName(_profile)) : arguments.Add(SyntaxFactory.IdentifierName(_profile));
            result = result.WithExpression(access.WithName(SyntaxFactory.GenericName(access.Name.Identifier, SyntaxFactory.TypeArgumentList(arguments))));
            return result;
        }

        /// <summary>Folds declaration-default selection inside the existing generic type dispatch.</summary>
        /// <param name="node">The existing case's concrete Serializer call.</param>
        /// <returns>A type-constant profile choice or the unchanged statement.</returns>
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            string enclosing = _profile;
            string scope = node.GetAnnotations(BinaryEntryInlining.OptionScopeAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
            if (scope != null && !_stateType.Contains("Json")) _profile = scope;
            ExpressionStatementSyntax rewritten;
            try { rewritten = (ExpressionStatementSyntax)base.VisitExpressionStatement(node); }
            finally { _profile = enclosing; }
            if (!_dispatch || node.Expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } ||
                access.Name.Identifier.ValueText is not ("SerializeJson" or "DeserializeJson" or "Serialize" or "Deserialize" or "Sizeof")) return rewritten;
            return SyntaxFactory.IfStatement(SyntaxFactory.ParseExpression("typeof(" + _profile + ") == typeof(global::LuminPack.Option.LuminPackDeclaredOptions)"),
                SyntaxFactory.Block(node), SyntaxFactory.ElseClause(SyntaxFactory.Block(rewritten)));
        }
    }
}
