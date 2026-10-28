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
        var tree = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions.WithDocumentationMode(DocumentationMode.None));
        var root = (CompilationUnitSyntax)new BinaryCursorProjection(BinaryCursorProjection.UsesPointerCursor(compilation, metadata), metadata.AllowUnsafe).Visit(new ContiguousMemoryProjection().Visit(new SequentialFixedProjection().Visit(new BinaryMemoryProjection().Visit(tree.GetCompilationUnitRoot()))));
        tree = CSharpSyntaxTree.Create(root, metadata.ParseOptions);
        root = tree.GetCompilationUnitRoot();
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
            if (member is not MethodDeclarationSyntax method || method.ParameterList.Parameters.Count != 2 ||
                !BinaryOperationPlan.IsOperation(method.Identifier.ValueText))
            {
                if (closedRoot != null) continue;
                if (member is ClassDeclarationSyntax helper && helper.Identifier.ValueText.StartsWith("Json_", StringComparison.Ordinal))
                    support.Add(helper.WithMembers(SyntaxFactory.List(helper.Members.Where(item => item is not MethodDeclarationSyntax operation ||
                        operation.Identifier.ValueText is not ("WriteValue" or "ReadValue")))));
                else support.Add(member);
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
                if (type.IsReferenceType) method = method.WithAdditionalAnnotations(new SyntaxAnnotation("LuminPack.ReferencePayload"));
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
                if (name == "SerializeUnion" && type.IsReferenceType)
                {
                    // A class reference already fits in one argument register. The Union
                    // wrapper's local must not escape to a stack slot solely to pass `in`.
                    // The operation still snapshots input before touching any fields.
                    var payload = method.ParameterList.Parameters[1];
                    method = method.WithParameterList(method.ParameterList.WithParameters(method.ParameterList.Parameters.Replace(payload,
                        payload.WithModifiers(default))));
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
                        body = expanded.WithStatements(expanded.Statements.Insert(0, SyntaxFactory.ParseStatement("var value = input;")));
                    }
                    else body = expanded;
                    method = method.WithBody(body).WithExpressionBody(null).WithSemicolonToken(default);
                }
            }
            // Field expansion and JIT inlining have different budgets. Let the
            // runtime size loop-bearing APIs instead of forcing their entire
            // graph into every convenience entry and Union caller.
            if (state is "LuminPack.Core.LuminPackWriter" or "LuminPack.Core.LuminPackReader" or
                    "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader" &&
                GeneratedExecutionPolicy.LetRuntimeChooseInlining(method.Body, state.Contains("Json")))
            {
                method = method.WithAttributeLists(SyntaxFactory.List(method.AttributeLists.Select(list =>
                    list.WithAttributes(SyntaxFactory.SeparatedList(list.Attributes.Where(attribute =>
                        !attribute.Name.ToString().EndsWith("MethodImpl", StringComparison.Ordinal) ||
                        attribute.ArgumentList?.ToString().Contains("AggressiveInlining") != true))))
                    .Where(list => list.Attributes.Count != 0)));
            }
            if (state == "LuminPack.Core.LuminPackWriter" && name is "Serialize" or "SerializeUnion")
                method = method.WithBody(BinaryEntryCursorAliases.Rewrite(method.Body));
            if (state == "LuminPack.Core.LuminPackReader" && name is "Deserialize" or "DeserializeUnion")
                method = method.WithBody(BinaryEntryCursorAliases.RewriteReader(method.Body));
            if (name == "SerializeUnion" && state == "LuminPack.Core.LuminPackWriter" && method.TypeParameterList == null &&
                !CompiledOptions.Resolve(type, compilation).Input.Enabled &&
                FixedUnionCursorPlan.CanUsePayload(type, compilation, metadata))
            {
                FixedUnionCursorProjection.Append(method, core);
                continue;
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
        if (core.Count != 0) namespaces.Add(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("LuminPack"))
            .AddMembers(new MemberDeclarationSyntax[] { serializer }.Concat(BinaryExtensionProjection.Create(container.Identifier.ValueText, core, compilation)).ToArray()));
        if (support.Count != 0) namespaces.Add(SyntaxFactory.NamespaceDeclaration(SyntaxFactory.ParseName("LuminPack.Generated"))
            .AddMembers(container.WithMembers(SyntaxFactory.List(support))));
        var projected = root.WithMembers(SyntaxFactory.List(namespaces))
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("global::LuminPack.Generated")));
        var projectedTree = CSharpSyntaxTree.Create(projected, metadata.ParseOptions);
        var rewritten = new CallRewriter(compilation.AddSyntaxTrees(projectedTree).GetSemanticModel(projectedTree), compilation).Visit(projectedTree.GetRoot());
        rewritten = new CompiledOperationProjection(compilation, CompiledOptions.Resolve(closedRoot, compilation)).Visit(rewritten);
        rewritten = BinaryEntryLocalScopes.NormalizeRoot(rewritten);
        rewritten = GeneratedOperationScopes.Rewrite(rewritten);
        rewritten = GeneratedOperationNames.Rewrite(rewritten);
        rewritten = new ContiguousMemoryProjection().Visit(new SequentialFixedProjection().Visit(new BinaryMemoryProjection().Visit(rewritten)));
        rewritten = new BinaryCursorProjection(BinaryCursorProjection.UsesPointerCursor(compilation, metadata), metadata.AllowUnsafe).Visit(rewritten);
        rewritten = new BinaryExtensionProjection.Calls().Visit(rewritten);
        rewritten = new DirectStorageProjection(compilation).Visit(rewritten);
        rewritten = new RootOperationProjection().Visit(rewritten);
        rewritten = new ProvenLeafWriteProjection(compilation, metadata, types).Visit(rewritten);
        rewritten = new ProvenObjectWriteProjection(compilation, metadata, types).Visit(rewritten);
        rewritten = new JsonValueProjection().Visit(rewritten);
        rewritten = new BinaryCursorProjection(BinaryCursorProjection.UsesPointerCursor(compilation, metadata), metadata.AllowUnsafe).Visit(rewritten);
        rewritten = new NativeOffsetProjection().Visit(rewritten);
        rewritten = new RecursionGuardProjection(payload => (closedRoot != null && FormatterTypeName.Get(closedRoot) == payload
            ? closedRoot : types.TryGetValue(payload, out var symbol) ? symbol : null) is { } payloadType &&
            RecursiveTypeAnalysis.IsRecursive(payloadType, compilation)).Visit(rewritten);
        if (closedRoot != null) rewritten = new ClosedMemberResetProjection(types).Visit(rewritten);
        rewritten = RootContainerProjection.Apply(rewritten);
        var normalized = (CompilationUnitSyntax)rewritten.NormalizeWhitespace("    ", Environment.NewLine);
        normalized = normalized.WithLeadingTrivia(SyntaxFactory.ParseLeadingTrivia("#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649" + Environment.NewLine).AddRange(normalized.GetLeadingTrivia()));
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
        var sourceRoot = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions).GetCompilationUnitRoot()
            .AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName("global::LuminPack")).NormalizeWhitespace()
                .WithTrailingTrivia(SyntaxFactory.EndOfLine(Environment.NewLine)));
        var tree = CSharpSyntaxTree.Create(sourceRoot, metadata.ParseOptions);
        var rewritten = new CallRewriter(compilation.AddSyntaxTrees(tree).GetSemanticModel(tree), compilation).Visit(tree.GetRoot());
        return new JsonValueProjection().Visit(new BinaryExtensionProjection.Calls().Visit(new BinaryCursorProjection(BinaryCursorProjection.UsesPointerCursor(compilation, metadata), metadata.AllowUnsafe).Visit(new ContiguousMemoryProjection().Visit(new SequentialFixedProjection().Visit(new BinaryMemoryProjection().Visit(new CompiledOperationProjection(compilation, CompiledOptions.Resolve(null, compilation)).Visit(rewritten))))))).ToFullString();
    }

    /// <summary>Retains the evaluator's shared generic entry without emitting obsolete Writer or Reader aliases.</summary>
    /// <param name="container">The assembly-specific evaluator container.</param>
    /// <param name="metadata">The consumer's reference-safety settings.</param>
    /// <returns>The evaluator forwarding declaration without duplicated formatter implementations.</returns>
    internal static string Bridges(string container, MetaInfo metadata)
    {
        var builder = new StringBuilder("// <auto-generated/>\n#nullable enable annotations\n#pragma warning disable CS0162, CS0168, CS0219, CS0436, CS0649\nnamespace LuminPack.Generated { internal static partial class " + container + " {\n");
        builder.Append("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\npublic static void CalculateOffset<T>(this ref global::LuminPack.Core.LuminPackEvaluator state, ")
            .Append(metadata.IsNet8 ? "scoped " : string.Empty)
            .AppendLine("ref T value) => global::LuminPack.LuminPackSerializer.Sizeof(ref state, ref value);");
        builder.AppendLine("} }");
        builder.Append("namespace LuminPack { internal static partial class Binary_").Append(container).AppendLine(" {");
        foreach (bool json in new[] { false, true })
        foreach (bool read in new[] { false, true })
        {
            string operation = (read ? "Deserialize" : "Serialize") + (json ? "Json" : string.Empty);
            builder.Append("[global::System.Runtime.CompilerServices.MethodImpl(global::System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]\ninternal static void ")
                .Append(read ? "Read" : "Write").Append("<T>(this ref global::LuminPack.Core.LuminPack")
                .Append(json ? "Json" : string.Empty).Append(read ? "Reader" : "Writer").Append(" state, ")
                .Append(metadata.IsNet8 ? "scoped " : string.Empty).Append(read ? "ref" : "in")
                .Append(" T value, bool fallback = true) => global::LuminPack.LuminPackSerializer.").Append(operation)
                .Append("<T>(ref state, ").Append(read ? "ref" : "in").AppendLine(" value);");
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
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Compilation, OperationCatalog> Catalogs = new();
        private readonly SemanticModel _model;
        private readonly HashSet<string> _knownTypes;
        private readonly HashSet<string> _knownJsonTypes;
        private readonly HashSet<string> _knownSizeTypes;
        private int _scalarReadIndex;
        private const string ScalarReadAnnotation = "LuminPack.RawScalarRead";
        private const string SmallScalarReadAnnotation = "LuminPack.SmallScalarRead";
        private bool _lowerScalarReads;

        /// <summary>Isolates fixed-read expansion to pair formatter bodies whose callers exhaust their native inlining budget.</summary>
        /// <param name="node">The generated operation declaration.</param>
        /// <returns>The same method with its original scalar contract.</returns>
        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            bool previous = _lowerScalarReads;
            _lowerScalarReads = node.ParameterList.Parameters.Count == 2 &&
                node.ParameterList.Parameters[0].Type.ToString().EndsWith("LuminPackReader", StringComparison.Ordinal) &&
                node.ParameterList.Parameters[1].Type.ToString().StartsWith("global::System.Collections.Generic.KeyValuePair<", StringComparison.Ordinal);
            try { return base.VisitMethodDeclaration(node); }
            finally { _lowerScalarReads = previous; }
        }

        /// <summary>Spells fixed scalar reads directly so large callers need no helper inlining budget.</summary>
        /// <param name="node">The standalone generated primitive read.</param>
        /// <returns>The same architecture-aware load and cursor advance, or the unchanged statement.</returns>
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var result = (ExpressionStatementSyntax)base.VisitExpressionStatement(node);
            if (result.Expression is not InvocationExpressionSyntax call ||
                call.GetAnnotations(ScalarReadAnnotation).SingleOrDefault()?.Data is not { } type ||
                call.Expression is not MemberAccessExpressionSyntax access || access.Expression is not IdentifierNameSyntax)
                return result;
            string identity = "__luminScalarRead" + _scalarReadIndex++;
            string target = identity + "Target", source = identity + "Source";
            const string memory = "global::System.Runtime.CompilerServices.Unsafe";
            string size = memory + ".SizeOf<" + type + ">()";
            // The checked Advance validates the value's range before the cursor moves; the load reads the captured position.
            string capture = "ref byte " + source + " = ref " + access.Expression + ".GetCurrentSpanReference(); " + access.Expression + ".Advance(" + size + "); ";
            if (call.HasAnnotations(SmallScalarReadAnnotation))
            {
                bool simpleTarget = call.ArgumentList.Arguments[0].Expression is IdentifierNameSyntax;
                string destination = simpleTarget ? call.ArgumentList.Arguments[0].Expression.ToString() : target;
                string declaration = simpleTarget ? string.Empty : "ref " + type + " " + target + " = ref " + call.ArgumentList.Arguments[0].Expression + "; ";
                return SyntaxFactory.ParseStatement("{ " + declaration + capture + destination + " = " + memory + ".ReadUnaligned<" + type + ">(ref " +
                    source + "); }").WithTriviaFrom(node);
            }
            return SyntaxFactory.ParseStatement("{ ref " + type + " " + target + " = ref " + call.ArgumentList.Arguments[0].Expression +
                "; " + capture +
                "if (" + memory + ".SizeOf<global::System.IntPtr>() == 8) " + target + " = " + memory + ".ReadUnaligned<" + type + ">(ref " + source + "); " +
                "else " + memory + ".CopyBlockUnaligned(ref " + memory + ".As<" + type + ", byte>(ref " + target + "), ref " + source + ", (uint)" + size + "); }").WithTriviaFrom(node);
        }

        /// <summary>Captures the temporary model without retaining it in generated results.</summary>
        /// <param name="model">The current projected file binding.</param>
        /// <param name="compilation">The unchanged input compilation sharing the formatter catalog.</param>
        internal CallRewriter(SemanticModel model, Compilation compilation)
        {
            _model = model;
            var catalog = Catalogs.GetValue(compilation, current => new OperationCatalog(current));
            _knownTypes = catalog.Binary;
            _knownJsonTypes = catalog.Json;
            _knownSizeTypes = catalog.Size;
        }

        /// <summary>Shares symbol-free operation capabilities across source contributions in one compilation.</summary>
        private sealed class OperationCatalog
        {
            internal readonly HashSet<string> Binary;
            internal readonly HashSet<string> Json;
            internal readonly HashSet<string> Size;

            /// <summary>Captures formatter spellings once without retaining compiler symbols.</summary>
            /// <param name="compilation">The unchanged consumer compilation.</param>
            internal OperationCatalog(Compilation compilation)
            {
                var types = LuminPackExtensionGenerator.GetOrderedFormatterTypes(compilation);
                Binary = new HashSet<string>(types.Where(type => LuminPackExtensionGenerator.CanRegisterFormatter(type, compilation, false)).Select(FormatterTypeName.Get), StringComparer.Ordinal);
                Json = new HashSet<string>(types.Where(type => LuminPackExtensionGenerator.CanRegisterFormatter(type, compilation, true)).Select(FormatterTypeName.Get), StringComparer.Ordinal);
                Size = new HashSet<string>(types.Where(type => LuminPackExtensionGenerator.CanRegisterCalculateFormatter(type, compilation)).Select(FormatterTypeName.Get), StringComparer.Ordinal);
            }
        }

        /// <summary>Projects a generated operation invocation to its state-based overload.</summary>
        /// <param name="node">The original invocation.</param>
        /// <returns>The unified call or the unchanged invocation.</returns>
        public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax jsonUnion &&
                (jsonUnion.Name.Identifier.ValueText.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) ||
                 jsonUnion.Name.Identifier.ValueText.StartsWith("__LuminPackUnionSerialize_", StringComparison.Ordinal) ||
                 jsonUnion.Name.Identifier.ValueText.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal)))
            {
                string prefix = jsonUnion.Name.Identifier.ValueText.StartsWith("__LuminPackUnionSerializeJson_", StringComparison.Ordinal) ? "__LuminPackUnionSerializeJson_" :
                    jsonUnion.Name.Identifier.ValueText.StartsWith("__LuminPackUnionCalculateOffset_", StringComparison.Ordinal) ? "__LuminPackUnionCalculateOffset_" : "__LuminPackUnionSerialize_";
                string suffix = jsonUnion.Name.Identifier.ValueText.Substring(prefix.Length);
                var pending = new Stack<ITypeSymbol>();
                var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                var receiver = _model.GetTypeInfo(jsonUnion.Expression).Type;
                if (receiver != null) pending.Push(receiver);
                while (pending.Count != 0)
                {
                    var current = pending.Pop();
                    if (!visited.Add(current)) continue;
                    if (current is ITypeParameterSymbol parameter)
                    {
                        foreach (var constraint in parameter.ConstraintTypes) pending.Push(constraint);
                        continue;
                    }
                    if (current is not INamedTypeSymbol named) continue;
                    if (LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(named.OriginalDefinition) == suffix)
                        return base.VisitInvocationExpression(node).WithAdditionalAnnotations(new SyntaxAnnotation(
                            CompiledOperationProjection.UnionRootAnnotation, FormatterTypeName.Get(named)));
                    if (named.BaseType != null) pending.Push(named.BaseType);
                    foreach (var contract in named.Interfaces) pending.Push(contract);
                }
            }
            if (node.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "SerializeUnion" } union &&
                union.Expression.ToString() == "global::LuminPack.LuminPackSerializer")
                return WithoutUnionInputReference((InvocationExpressionSyntax)base.VisitInvocationExpression(node));
            if (node.Expression is not MemberAccessExpressionSyntax access || !BinaryOperationPlan.IsOperation(access.Name.Identifier.ValueText))
                return base.VisitInvocationExpression(node);
            if (_model.GetSymbolInfo(node).Symbol is IMethodSymbol { IsExtensionMethod: true } existing &&
                existing.DeclaringSyntaxReferences.Any(reference => reference.SyntaxTree != _model.SyntaxTree) &&
                !node.Ancestors().OfType<ClassDeclarationSyntax>().Any(type => type.Identifier.ValueText == "FormatterDispatch"))
                return base.VisitInvocationExpression(node);
            string state = _model.GetTypeInfo(access.Expression).Type?.ToDisplayString();
            bool reduced = IsState(state);
            if (!reduced && access.Expression.ToString().StartsWith("global::LuminPack.Generated.LuminPackExtensions_", StringComparison.Ordinal) && node.ArgumentList.Arguments.Count != 0)
                state = _model.GetTypeInfo(node.ArgumentList.Arguments[0].Expression).Type?.ToDisplayString();
            if (!IsState(state)) return base.VisitInvocationExpression(node);
            var arguments = (ArgumentListSyntax)Visit(node.ArgumentList);
            var scalarArgument = node.ArgumentList.Arguments.ElementAtOrDefault(reduced ? 0 : 1);
            var scalarType = scalarArgument == null ? null : _model.GetTypeInfo(scalarArgument.Expression).Type;
            bool rawScalar = !state.Contains("Json") && scalarType != null &&
                !CompiledOptions.Resolve(scalarType, _model.Compilation).Input.Enabled &&
                global::LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits.CanUseUnmanagedSerialization(scalarType) &&
                (scalarType.TypeKind == TypeKind.Enum || CodeEmitterRegistry.KnownValueTypes.Contains(FormatterTypeName.Get(scalarType)));
            if (rawScalar && access.Name.Identifier.ValueText is "WriteValue" or "ReadValue" or "CalculateOffset")
            {
                ExpressionSyntax receiver = reduced ? access.Expression : node.ArgumentList.Arguments[0].Expression;
                if (access.Name.Identifier.ValueText == "CalculateOffset")
                    return SyntaxFactory.ParseExpression(receiver + " += global::System.Runtime.CompilerServices.Unsafe.SizeOf<" + FormatterTypeName.Get(scalarType) + ">()").WithTriviaFrom(node);
                string operation = access.Name.Identifier.ValueText == "WriteValue" ? "Write" : "Read";
                var scalarCall = node.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                    receiver, SyntaxFactory.IdentifierName(operation))).WithArgumentList(reduced ? arguments : arguments.WithArguments(arguments.Arguments.RemoveAt(0)));
                return _lowerScalarReads && operation == "Read" && receiver is IdentifierNameSyntax
                    ? scalarCall.WithAdditionalAnnotations(IsSmallScalar(scalarType)
                        ? new[] { new SyntaxAnnotation(ScalarReadAnnotation, FormatterTypeName.Get(scalarType)), new SyntaxAnnotation(SmallScalarReadAnnotation) }
                        : new[] { new SyntaxAnnotation(ScalarReadAnnotation, FormatterTypeName.Get(scalarType)) }) : scalarCall;
            }
            if (reduced) arguments = arguments.WithArguments(arguments.Arguments.Insert(0,
                SyntaxFactory.Argument((ExpressionSyntax)Visit(access.Expression)).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword))));
            string name = MethodName(access.Name.Identifier.ValueText, state.Contains("Json"));
            SimpleNameSyntax target = access.Name is GenericNameSyntax generic ? generic.WithIdentifier(SyntaxFactory.Identifier(name)) : SyntaxFactory.IdentifierName(name);
            if (access.Name is not GenericNameSyntax && _model.GetSymbolInfo(node).Symbol is IMethodSymbol { IsGenericMethod: true } inferred)
                target = SyntaxFactory.GenericName(SyntaxFactory.Identifier(name), SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(inferred.TypeArguments.Select(type => SyntaxFactory.ParseTypeName(FormatterTypeName.Get(type))))));
            if (target is not GenericNameSyntax && node.ArgumentList.Arguments.Count > (reduced ? 0 : 1))
            {
                var payload = _model.GetTypeInfo(node.ArgumentList.Arguments[reduced ? 0 : 1].Expression).Type;
                string frozenPayload = node.GetAnnotations(BinaryEntryInlining.PayloadAnnotation).Select(annotation => annotation.Data).SingleOrDefault();
                var known = name == "Sizeof" ? _knownSizeTypes : state.Contains("Json") ? _knownJsonTypes : _knownTypes;
                if (frozenPayload != null && !known.Contains(frozenPayload))
                    target = SyntaxFactory.GenericName(SyntaxFactory.Identifier(name), SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.ParseTypeName(frozenPayload))));
                else if (payload != null && (payload.TypeKind == TypeKind.TypeParameter ||
                    !known.Contains(FormatterTypeName.Get(payload))))
                    target = SyntaxFactory.GenericName(SyntaxFactory.Identifier(name), SyntaxFactory.TypeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(SyntaxFactory.ParseTypeName(FormatterTypeName.Get(payload)))));
            }
            if (name is "SerializeUnion" or "DeserializeUnion" or "DeserializeFresh" &&
                _model.GetTypeInfo(node.ArgumentList.Arguments[reduced ? 0 : 1].Expression).Type is INamedTypeSymbol openPayload &&
                LuminPackExtensionGenerator.ContainsTypeParameter(openPayload))
            {
                var owners = new Stack<INamedTypeSymbol>();
                for (var owner = openPayload; owner != null; owner = owner.ContainingType) owners.Push(owner);
                var openArguments = owners.SelectMany(owner => owner.TypeArguments).Select(argument => SyntaxFactory.ParseTypeName(FormatterTypeName.Get(argument)));
                target = SyntaxFactory.GenericName(SyntaxFactory.Identifier(name), SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(openArguments)));
            }
            var rewritten = node.WithExpression(SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer"), target)).WithArgumentList(arguments);
            if (!state.Contains("Json") && node.ArgumentList.Arguments.Count > (reduced ? 0 : 1))
            {
                var payloadType = _model.GetTypeInfo(node.ArgumentList.Arguments[reduced ? 0 : 1].Expression).Type;
                if (global::LuminPack.SourceGenerator.CodeEmitters.EmitterTypeTraits.RequiresExplicitStateContract(payloadType))
                    rewritten = rewritten.WithAdditionalAnnotations(new SyntaxAnnotation("LuminPack.StorageContract"));
            }
            return name == "SerializeUnion" ? WithoutUnionInputReference(rewritten) : rewritten;
        }

        /// <summary>Proves that typed scalar access is safe on both supported pointer widths.</summary>
        /// <param name="type">The primitive or enum storage symbol.</param>
        /// <returns>Whether the value is at most four bytes on 32-bit targets.</returns>
        private static bool IsSmallScalar(ITypeSymbol type)
        {
            if (type is INamedTypeSymbol { TypeKind: TypeKind.Enum } enumeration) type = enumeration.EnumUnderlyingType;
            return type?.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
                SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
                SpecialType.System_UInt32 or SpecialType.System_Single or SpecialType.System_IntPtr or SpecialType.System_UIntPtr;
        }

        /// <summary>Allows class Union payload references to travel directly in an argument register.</summary>
        /// <param name="node">A call to the unique Union payload implementation.</param>
        /// <returns>The same arguments with an implicit input passing convention.</returns>
        private static InvocationExpressionSyntax WithoutUnionInputReference(InvocationExpressionSyntax node)
        {
            var arguments = node.ArgumentList.Arguments;
            if (arguments.Count != 2 || !arguments[1].RefKindKeyword.IsKind(SyntaxKind.InKeyword)) return node;
            return node.WithArgumentList(node.ArgumentList.WithArguments(arguments.Replace(arguments[1], arguments[1].WithRefKindKeyword(default))));
        }
    }
}
