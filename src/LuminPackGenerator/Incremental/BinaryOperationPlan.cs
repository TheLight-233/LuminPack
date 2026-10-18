using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Caches typed operation templates independently of concrete serializer overload selection.</summary>
internal sealed class BinaryOperationPlanCache
{
    private readonly TypeEmissionCache _values = new TypeEmissionCache();

    /// <summary>Obtains a symbol-free template under the existing source and semantic dependency guards.</summary>
    /// <param name="type">The current closed operation type.</param>
    /// <param name="compilation">The current consumer compilation.</param>
    /// <param name="metadata">Current emission settings.</param>
    /// <returns>Typed call slots, local identities, and layout declarations without retained semantic objects.</returns>
    internal BinaryOperationPlan Get(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
        => _values.GetOrBuildValue(type, compilation, metadata, LuminPackGenerationTier.Full,
            () => BinaryOperationPlan.Build(type, compilation, metadata));
}

/// <summary>Describes one closed type's operations before they are expanded into concrete entry points.</summary>
internal sealed class BinaryOperationPlan
{
    internal const string CallAnnotation = "LuminPack.BinaryOperation";
    internal const string NameAnnotation = "LuminPack.OperationIdentity";
    internal const string Prefix = "__lumin_";
    internal readonly string TypeName;
    internal readonly ImmutableDictionary<string, BinaryOperation> Operations;
    internal readonly ImmutableArray<MemberDeclarationSyntax> Layouts;
    internal readonly string TemplateSource;
    internal readonly int GuaranteedMaxBytes;
    internal readonly CompiledOptions Options;

    /// <summary>Captures immutable syntax and value-only call facts.</summary>
    /// <param name="typeName">The canonical closed formatter identity.</param>
    /// <param name="operations">Available normal and specialized binary operations.</param>
    /// <param name="layouts">Private storage mirrors required by these operations.</param>
    /// <param name="templateSource">The already rendered template shared with state-overload projection.</param>
    /// <param name="guaranteedMaxBytes">The explicit complete-record guarantee, or zero when absent.</param>
    /// <param name="options">The declaration's immutable compiled option profile.</param>
    private BinaryOperationPlan(string typeName, ImmutableDictionary<string, BinaryOperation> operations, ImmutableArray<MemberDeclarationSyntax> layouts, string templateSource = null, int guaranteedMaxBytes = 0, CompiledOptions options = null)
    { TypeName = typeName; Operations = operations; Layouts = layouts; TemplateSource = templateSource; GuaranteedMaxBytes = guaranteedMaxBytes; Options = options ?? CompiledOptions.Default; }

    /// <summary>Analyzes only one type's templates; the temporary semantic model never enters the cached result.</summary>
    /// <param name="type">The current operation type.</param>
    /// <param name="compilation">The input compilation.</param>
    /// <param name="metadata">Current language and framework settings.</param>
    /// <returns>A reusable operation plan.</returns>
    internal static BinaryOperationPlan Build(ITypeSymbol type, Compilation compilation, MetaInfo metadata)
    {
        Interlocked.Increment(ref IncrementalGenerationMetrics.BinaryOperationPlanBuilds);
        string name = FormatterTypeName.Get(type);
        var empty = ImmutableDictionary<string, BinaryOperation>.Empty;
        if (LuminPackExtensionGenerator.IsCustomGeneratorType(type) ||
            type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE) &&
            !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly) &&
            !CompilationTypeAnalysisCache.GetOrCreate(compilation).IsAssemblyPackable(type))
            return new BinaryOperationPlan(name, empty, ImmutableArray<MemberDeclarationSyntax>.Empty);

        string source = LuminPackExtensionGenerator.GenerateOperationTemplates(type, compilation, metadata);
        var tree = CSharpSyntaxTree.ParseText(source, metadata.ParseOptions);
        var binding = compilation.AddSyntaxTrees(tree);
        var model = binding.GetSemanticModel(tree);
        var typeArguments = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var current = type as INamedTypeSymbol; current != null; current = current.ContainingType)
            for (int index = 0; index < current.TypeParameters.Length; index++)
                typeArguments[current.TypeParameters[index].Name] = FormatterTypeName.Get(current.TypeArguments[index]);
        var container = tree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
            .FirstOrDefault(declaration => declaration.Identifier.ValueText.StartsWith("LuminPackExtensions_", StringComparison.Ordinal));
        if (container == null) return new BinaryOperationPlan(name, empty, ImmutableArray<MemberDeclarationSyntax>.Empty);

        var layouts = container.Members.Where(declaration => declaration switch
        {
            TypeDeclarationSyntax nested => nested.Identifier.ValueText.StartsWith("Local", StringComparison.Ordinal) || nested.Identifier.ValueText.Contains("CaseMap"),
            FieldDeclarationSyntax => true,
            MethodDeclarationSyntax method => method.Identifier.ValueText.StartsWith("BuildCaseMap_", StringComparison.Ordinal),
            _ => false
        }).ToArray();
        var layoutNames = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        foreach (var layout in layouts)
        {
            var declaration = layout is FieldDeclarationSyntax field ? (SyntaxNode)field.Declaration.Variables[0] : layout;
            var symbol = model.GetDeclaredSymbol(declaration);
            // A base mirror emitted in two different generic roots can carry different
            // substituted constraints. Keep each operation plan's complete layout family
            // independent rather than reusing the first same-spelled declaration.
            if (symbol != null) layoutNames[symbol] = Prefix + "layout_" +
                (type is INamedTypeSymbol owner ? LuminPackUnionDispatchCodeGenerator.GetSlotSuffix(owner) : "array") + "_" + symbol.Name;
        }
        var operations = ImmutableDictionary.CreateBuilder<string, BinaryOperation>(StringComparer.Ordinal);
        foreach (var method in container.Members.OfType<MethodDeclarationSyntax>().Where(method => IsOperation(method.Identifier.ValueText)))
        {
            if (method.Body == null || method.ParameterList.Parameters.Count != 2) continue;
            var symbol = model.GetDeclaredSymbol(method);
            string stateType = symbol?.Parameters[0].Type.ToDisplayString();
            if (stateType is not ("LuminPack.Core.LuminPackWriter" or "LuminPack.Core.LuminPackReader" or "LuminPack.Core.LuminPackEvaluator" or
                "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader")) continue;
            string operationName = method.Identifier.ValueText + (stateType.Contains("Json") ? ":json" : string.Empty);
            var localMethods = new Dictionary<IMethodSymbol, string>(SymbolEqualityComparer.Default);
            foreach (var local in method.Body.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            {
                var localSymbol = model.GetDeclaredSymbol(local) as IMethodSymbol;
                if (localSymbol is { ReturnsVoid: true, IsStatic: true } && local.Body != null &&
                    !localSymbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.MethodImplAttribute" &&
                        attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is int options && (options & (int)MethodImplOptions.NoInlining) != 0))
                    localMethods.Add(localSymbol, operationName + ":local:" + local.SpanStart);
            }
            foreach (var local in method.Body.DescendantNodes().OfType<LocalFunctionStatementSyntax>())
            {
                var localSymbol = model.GetDeclaredSymbol(local) as IMethodSymbol;
                if (localSymbol != null && localMethods.TryGetValue(localSymbol, out string key))
                    operations.Add(key, BinaryOperation.Create(model, localSymbol, local.Body, layoutNames, localMethods, typeArguments));
            }
            operations.Add(operationName, BinaryOperation.Create(model, symbol, method.Body, layoutNames, localMethods, typeArguments));
        }
        var layoutRewriter = new OperationTemplateRewriter(model, layoutNames, new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default),
            new Dictionary<IMethodSymbol, string>(SymbolEqualityComparer.Default));
        return new BinaryOperationPlan(name, operations.ToImmutable(), layouts.Select(layout =>
            (MemberDeclarationSyntax)new ActiveOperationSyntax().Visit(layoutRewriter.Visit(layout))).ToImmutableArray(), source,
            SourceGenerator.CodeEmitters.EmitterTypeTraits.TryGetBoundedMaxBytes(type, out int maximum) &&
            !(SourceGenerator.CodeEmitters.EmitterTypeTraits.CanUseUnmanagedSerialization(type) &&
                type is INamedTypeSymbol raw && TypeMetaChecker.CheckGeneratorType(raw) == LuminPack.Code.GeneratorType.Object) ? maximum : 0, CompiledOptions.Resolve(type, compilation));
    }

    /// <summary>Recognizes generated binary operation names without matching similarly named user methods.</summary>
    /// <param name="name">The generated method name.</param>
    /// <returns>Whether the name denotes a supported binary operation.</returns>
    internal static bool IsOperation(string name) => name is "WriteValue" or "ReadValue" or "ReadFreshValue" or
        "WriteValueWithCompress" or "ReadValueWithCompress" or "WritePolymorphismValue" or "ReadPolymorphismValue" or "CalculateOffset";

    /// <summary>Marks a compiler-created template identity so source member names with the same spelling remain untouched.</summary>
    /// <param name="name">The stable template name.</param>
    /// <param name="original">The original token whose trivia is retained.</param>
    /// <returns>An explicitly marked generated identifier.</returns>
    internal static SyntaxToken Identity(string name, SyntaxToken original)
        => SyntaxFactory.Identifier(name).WithTriviaFrom(original).WithAdditionalAnnotations(new SyntaxAnnotation(NameAnnotation, original.ValueText));
}

/// <summary>Stores a bound operation as immutable syntax, formal parameters, and typed expansion slots.</summary>
internal sealed class BinaryOperation
{
    internal readonly ImmutableHashSet<string> CursorLocals;
    internal readonly BlockSyntax Body;
    internal readonly ImmutableArray<BinaryOperationParameter> Parameters;
    internal readonly ImmutableDictionary<string, BinaryOperationCall> Calls;

    /// <summary>Captures one completed operation.</summary>
    /// <param name="body">The template body with stable local names and call annotations.</param>
    /// <param name="parameters">Formal parameter storage requirements.</param>
    /// <param name="calls">Typed nested operation slots.</param>
    /// <param name="cursorLocals">Compiler-bound integer cursor identities, or null when none exist; no symbols are retained.</param>
    private BinaryOperation(BlockSyntax body, ImmutableArray<BinaryOperationParameter> parameters, ImmutableDictionary<string, BinaryOperationCall> calls,
        ImmutableHashSet<string> cursorLocals)
    { Body = body; Parameters = parameters; Calls = calls; CursorLocals = cursorLocals; }

    /// <summary>Resolves local identities and nested argument types before discarding all semantic objects.</summary>
    /// <param name="model">The temporary template semantic model.</param>
    /// <param name="method">The operation or static local helper.</param>
    /// <param name="body">The original body.</param>
    /// <param name="layouts">Private layout symbol replacements.</param>
    /// <param name="localMethods">Static local helpers expanded as operations.</param>
    /// <param name="typeArguments">Closed arguments substituted into the original generic operation.</param>
    /// <returns>The immutable operation template.</returns>
    internal static BinaryOperation Create(SemanticModel model, IMethodSymbol method, BlockSyntax body,
        Dictionary<ISymbol, string> layouts, Dictionary<IMethodSymbol, string> localMethods, Dictionary<string, string> typeArguments)
    {
        var names = new Dictionary<ISymbol, string>(SymbolEqualityComparer.Default);
        ImmutableHashSet<string>.Builder cursorLocals = null;
        var parameters = ImmutableArray.CreateBuilder<BinaryOperationParameter>();
        for (int index = 0; index < method.Parameters.Length; index++)
        {
            var parameter = method.Parameters[index];
            string name = BinaryOperationPlan.Prefix + "p" + index;
            names.Add(parameter, name);
            parameters.Add(new BinaryOperationParameter(name, ClosedOperationTypes.Name(parameter.Type, typeArguments), parameter.RefKind, parameter.Name));
        }
        foreach (var node in body.DescendantNodes())
        {
            if (node is not (VariableDeclaratorSyntax or ForEachStatementSyntax or SingleVariableDesignationSyntax or CatchDeclarationSyntax or ParameterSyntax or LabeledStatementSyntax or LocalFunctionStatementSyntax)) continue;
            var symbol = model.GetDeclaredSymbol(node);
            if (symbol != null && !names.ContainsKey(symbol)) names.Add(symbol, BinaryOperationPlan.Prefix + "v" + names.Count);
            if (symbol is ILocalSymbol { Type.SpecialType: SpecialType.System_Int32, RefKind: RefKind.None } &&
                node is VariableDeclaratorSyntax { Initializer.Value: InvocationExpressionSyntax { ArgumentList.Arguments.Count: 0,
                    Expression: MemberAccessExpressionSyntax { Name.Identifier.ValueText: "GetCurrentSpanIndex" } } })
                (cursorLocals ??= ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal)).Add(names[symbol]);
        }
        var rewriter = new OperationTemplateRewriter(model, layouts, names, localMethods,
            method.Parameters.FirstOrDefault()?.Type.ToDisplayString() == "LuminPack.Core.LuminPackReader", typeArguments,
            method.Parameters.FirstOrDefault()?.Type.ToDisplayString() == "LuminPack.Core.LuminPackWriter");
        return new BinaryOperation((BlockSyntax)new ActiveOperationSyntax().Visit(rewriter.Visit(body)), parameters.ToImmutable(), rewriter.Calls.ToImmutableDictionary(StringComparer.Ordinal), cursorLocals?.ToImmutable());
    }
}

/// <summary>Preserves a formal parameter's type and reference semantics across static expansion.</summary>
internal sealed class BinaryOperationParameter
{
    internal readonly string Name;
    internal readonly string Type;
    internal readonly RefKind RefKind;
    internal readonly string OriginalName;

    /// <summary>Captures one formal parameter.</summary>
    /// <param name="name">Its stable template identity.</param>
    /// <param name="type">Its fully qualified type.</param>
    /// <param name="refKind">Its original passing convention.</param>
    /// <param name="originalName">Its original role name, retained only for final local presentation.</param>
    internal BinaryOperationParameter(string name, string type, RefKind refKind, string originalName)
    { Name = name; Type = type; RefKind = refKind; OriginalName = originalName; }
}

/// <summary>Identifies a nested operation independently of the compilation that discovered it.</summary>
internal sealed class BinaryOperationCall
{
    internal readonly string TypeName;
    internal readonly string Operation;
    internal readonly bool Reduced;
    internal readonly bool Local;
    internal readonly ImmutableArray<bool> Aliases;
    internal readonly string OptionProfile;

    /// <summary>Captures the typed nested call and argument storage facts.</summary>
    /// <param name="typeName">The canonical target type, or empty for a local helper.</param>
    /// <param name="operation">The target operation identity.</param>
    /// <param name="reduced">Whether the receiver supplies the first argument.</param>
    /// <param name="local">Whether the operation belongs to the same template.</param>
    /// <param name="aliases">Arguments that can preserve reference aliases rather than requiring temporaries.</param>
    /// <param name="optionProfile">A member's statically bound option profile, if present.</param>
    internal BinaryOperationCall(string typeName, string operation, bool reduced, bool local, IEnumerable<bool> aliases, string optionProfile = null)
    { TypeName = typeName; Operation = operation; Reduced = reduced; Local = local; Aliases = aliases.ToImmutableArray(); OptionProfile = optionProfile; }
}

/// <summary>Projects compiler-bound templates to pure syntax before any entry-point expansion occurs.</summary>
internal sealed class OperationTemplateRewriter : CSharpSyntaxRewriter
{
    internal const string StateAnnotation = "LuminPack.GeneratedOperationState";
    private readonly SemanticModel _model;
    private readonly Dictionary<ISymbol, string> _layouts;
    private readonly Dictionary<ISymbol, string> _names;
    private readonly Dictionary<IMethodSymbol, string> _locals;
    private readonly bool _scopeTemporaries;
    private readonly Dictionary<string, string> _typeArguments;
    private int _expressionHelpers;
    private readonly bool _capacityWriter;
    private int _capacityNext;
    internal readonly Dictionary<string, BinaryOperationCall> Calls = new Dictionary<string, BinaryOperationCall>(StringComparer.Ordinal);

    /// <summary>Creates a temporary projection visitor; it is never cached.</summary>
    /// <param name="model">The template binding model.</param>
    /// <param name="layouts">Layout names to relocate into the entry container.</param>
    /// <param name="names">Formal and local symbol identities.</param>
    /// <param name="locals">Static local helper identities.</param>
    /// <param name="scopeTemporaries">Whether reader span references need a common entry lifetime.</param>
    /// <param name="typeArguments">Closed arguments for an original generic operation.</param>
    /// <param name="capacityWriter">Whether binary container reservations may be selected statically.</param>
    internal OperationTemplateRewriter(SemanticModel model, Dictionary<ISymbol, string> layouts,
        Dictionary<ISymbol, string> names, Dictionary<IMethodSymbol, string> locals, bool scopeTemporaries = false,
        Dictionary<string, string> typeArguments = null, bool capacityWriter = false)
    { _model = model; _layouts = layouts; _names = names; _locals = locals; _scopeTemporaries = scopeTemporaries; _typeArguments = typeArguments ?? new Dictionary<string, string>(); _capacityWriter = capacityWriter; }

    /// <summary>Reserves a statically bounded foreach payload before its unchecked element loop.</summary>
    /// <param name="node">The compiler-generated collection traversal.</param>
    /// <returns>A reserved loop with an arithmetic-limit fallback, or the original checked traversal.</returns>
    public override SyntaxNode VisitForEachStatement(ForEachStatementSyntax node)
        => _capacityWriter ? BinaryCollectionCapacity.Rewrite(node, (StatementSyntax)base.VisitForEachStatement(node),
            _model, expression => (ExpressionSyntax)Visit(expression), Replacement, _capacityNext++) : base.VisitForEachStatement(node);

    /// <summary>Reserves bounded entry-array and staged collection traversals without changing their order.</summary>
    /// <param name="node">The compiler-generated indexed traversal.</param>
    /// <returns>A reserved loop and its checked overflow fallback, or the unchanged traversal.</returns>
    public override SyntaxNode VisitForStatement(ForStatementSyntax node)
        => _capacityWriter ? BinaryCollectionCapacity.Rewrite(node, (StatementSyntax)base.VisitForStatement(node),
            _model, expression => (ExpressionSyntax)Visit(expression), Replacement, _capacityNext++) : base.VisitForStatement(node);

    /// <summary>Reserves bounded hash-entry traversals while preserving sparse-slot filtering.</summary>
    /// <param name="node">The compiler-generated occupied-entry traversal.</param>
    /// <returns>A reserved loop and its checked overflow fallback, or the unchanged traversal.</returns>
    public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
        => _capacityWriter ? BinaryCollectionCapacity.Rewrite(node, (StatementSyntax)base.VisitWhileStatement(node),
            _model, expression => (ExpressionSyntax)Visit(expression), Replacement, _capacityNext++) : base.VisitWhileStatement(node);

    /// <summary>Marks typed formatter calls and local helpers for later expansion without retaining symbols.</summary>
    /// <param name="node">The original expression statement.</param>
    /// <returns>The renamed statement, with a typed call annotation when applicable.</returns>
    public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
    {
        if (node.Expression is not InvocationExpressionSyntax call) return base.VisitExpressionStatement(node);
        BinaryOperationCall slot = null;
        var target = _model.GetSymbolInfo(call).Symbol as IMethodSymbol;
        if (target != null && _locals.TryGetValue(target, out string local))
            slot = new BinaryOperationCall(string.Empty, local, false, true, call.ArgumentList.Arguments.Select(argument => CanAlias(argument.Expression)));
        else if (call.Expression is MemberAccessExpressionSyntax access && BinaryOperationPlan.IsOperation(access.Name.Identifier.ValueText))
        {
            string receiver = _model.GetTypeInfo(access.Expression).Type?.ToDisplayString();
            bool reduced = receiver is "LuminPack.Core.LuminPackWriter" or "LuminPack.Core.LuminPackReader" or "LuminPack.Core.LuminPackEvaluator" or
                "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader";
            bool explicitCall = access.Expression.ToString().StartsWith("global::LuminPack.Generated.LuminPackExtensions_", StringComparison.Ordinal);
            if (reduced && call.ArgumentList.Arguments.Count == 1 || explicitCall && call.ArgumentList.Arguments.Count == 2)
            {
                var argument = call.ArgumentList.Arguments[reduced ? 0 : 1].Expression;
                var type = _model.GetTypeInfo(argument).Type;
                if ((type == null || type.TypeKind == TypeKind.Error) && _typeArguments.Any(pair => pair.Key == pair.Value))
                    return base.VisitExpressionStatement(node);
                if (type == null || type.TypeKind == TypeKind.Error)
                    return LuminPackExceptionHelper.ThrowInvalidOperationException<SyntaxNode>("Cannot resolve an inline operation argument: " + call);
                if (IsOpenOperationType(type))
                {
                    var retained = (ExpressionStatementSyntax)base.VisitExpressionStatement(node);
                    return retained.WithExpression(retained.Expression.WithAdditionalAnnotations(new SyntaxAnnotation(
                        BinaryEntryInlining.PayloadAnnotation, ClosedOperationTypes.Name(type, _typeArguments))));
                }
                var aliases = call.ArgumentList.Arguments.Select(item => CanAlias(item.Expression)).ToList();
                if (reduced) aliases.Insert(0, true);
                string state = reduced ? receiver : _model.GetTypeInfo(call.ArgumentList.Arguments[0].Expression).Type?.ToDisplayString();
                string optionProfile = argument is InvocationExpressionSyntax marker && IsOptionMarker(marker)
                    ? ((GenericNameSyntax)((MemberAccessExpressionSyntax)marker.Expression).Name).TypeArgumentList.Arguments[0].ToString() : null;
                slot = new BinaryOperationCall(ClosedOperationTypes.Name(type, _typeArguments), access.Name.Identifier.ValueText +
                    (state?.Contains("Json") == true ? ":json" : string.Empty), reduced, false, aliases, optionProfile);
            }
        }
        var result = base.VisitExpressionStatement(node);
        if (slot == null) return result;
        string id = node.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Calls.Add(id, slot);
        return result.WithAdditionalAnnotations(new SyntaxAnnotation(BinaryOperationPlan.CallAnnotation, id));
    }

    /// <summary>Keeps unknown generic payloads behind the shared state dispatch boundary.</summary>
    /// <param name="type">The original operation argument type.</param>
    /// <returns>Whether a type parameter remains unknown after closing the containing operation.</returns>
    private bool IsOpenOperationType(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol parameter => !_typeArguments.TryGetValue(parameter.Name, out string argument) || argument == parameter.Name,
        IArrayTypeSymbol array => IsOpenOperationType(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(IsOpenOperationType) || named.ContainingType != null && IsOpenOperationType(named.ContainingType),
        _ => false
    };

    /// <summary>Removes static local helper declarations after capturing their independently expandable bodies.</summary>
    /// <param name="node">The original helper declaration.</param>
    /// <returns>No declaration for an expanded helper; other local functions remain unchanged.</returns>
    public override SyntaxNode VisitLocalFunctionStatement(LocalFunctionStatementSyntax node)
        => _model.GetDeclaredSymbol(node) is IMethodSymbol symbol && _locals.ContainsKey(symbol) ? null : base.VisitLocalFunctionStatement(node);

    /// <summary>Gives pre-scoped-language span locals the reader's escape context without adding a method boundary.</summary>
    /// <param name="node">The original operation block.</param>
    /// <returns>The same statements, with zero-length lifetime seeds before original span initialization.</returns>
    public override SyntaxNode VisitBlock(BlockSyntax node)
    {
        if (!_scopeTemporaries || (int)((CSharpParseOptions)_model.SyntaxTree.Options).LanguageVersion >= 1100) return base.VisitBlock(node);
        var statements = new List<StatementSyntax>();
        foreach (var statement in node.Statements)
        {
            var visited = Visit(statement) as StatementSyntax;
            if (visited == null) continue;
            if (statement is LocalDeclarationStatementSyntax { Declaration.Variables.Count: 1 } original &&
                original.Declaration.Type is not RefTypeSyntax && original.Declaration.Variables[0].Initializer != null &&
                !original.Declaration.Variables[0].Initializer.Value.DescendantNodesAndSelf().Any(expression =>
                    expression is StackAllocArrayCreationExpressionSyntax or ImplicitStackAllocArrayCreationExpressionSyntax) &&
                _model.GetDeclaredSymbol(original.Declaration.Variables[0]) is ILocalSymbol { Type: INamedTypeSymbol span } &&
                span.ContainingNamespace.ToDisplayString() == "System" && span.Name is "Span" or "ReadOnlySpan" && span.TypeArguments.Length == 1)
            {
                string element = ClosedOperationTypes.Name(span.TypeArguments[0], _typeArguments);
                var seed = SyntaxFactory.ParseExpression("reader.CreateSpan<" + element + ">(ref global::System.Runtime.CompilerServices.Unsafe.As<byte, " + element + ">(ref reader.GetCurrentSpanReference()), 0)");
                seed = seed.ReplaceNodes(seed.DescendantNodes().OfType<IdentifierNameSyntax>().Where(identifier => identifier.Identifier.ValueText == "reader"),
                    (originalName, current) => current.WithIdentifier(BinaryOperationPlan.Identity(BinaryOperationPlan.Prefix + "p0", originalName.Identifier)));
                var local = (LocalDeclarationStatementSyntax)visited;
                var variable = local.Declaration.Variables[0];
                statements.Add(local.WithDeclaration(local.Declaration.WithType(SyntaxFactory.ParseTypeName(ClosedOperationTypes.Name(span, _typeArguments)))
                    .WithVariables(SyntaxFactory.SingletonSeparatedList(variable.WithInitializer(SyntaxFactory.EqualsValueClause(seed))))));
                statements.Add(SyntaxFactory.ExpressionStatement(SyntaxFactory.AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(variable.Identifier), variable.Initializer.Value)));
            }
            else statements.Add(visited);
        }
        return node.WithStatements(SyntaxFactory.List(statements));
    }

    /// <summary>Keeps inlined ref-struct temporaries within the operation scope instead of giving them a caller-wide escape scope.</summary>
    /// <param name="node">The original local declaration.</param>
    /// <returns>The same local with a scoped lifetime when supported by the consumer language.</returns>
    public override SyntaxNode VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
    {
        var result = (LocalDeclarationStatementSyntax)base.VisitLocalDeclarationStatement(node);
        var parse = (CSharpParseOptions)_model.SyntaxTree.Options;
        if (!_scopeTemporaries || (int)parse.LanguageVersion < 1100 || node.Declaration.Type is RefTypeSyntax ||
            node.Modifiers.Any(token => token.IsKind(SyntaxKind.ScopedKeyword)) ||
            !node.Declaration.Variables.Any(variable => _model.GetDeclaredSymbol(variable) is ILocalSymbol { Type.IsRefLikeType: true })) return result;
        var type = ((ILocalSymbol)_model.GetDeclaredSymbol(node.Declaration.Variables[0])).Type;
        return result.WithDeclaration(result.Declaration.WithType(SyntaxFactory.ParseTypeName(ClosedOperationTypes.Name(type, _typeArguments))))
            .WithModifiers(result.Modifiers.Add(SyntaxFactory.Token(SyntaxKind.ScopedKeyword)));
    }

    /// <summary>Replaces generated private-member accessors with their direct mirror-field expression.</summary>
    /// <param name="node">The original invocation.</param>
    /// <returns>The direct field expression or the unchanged invocation.</returns>
    public override SyntaxNode VisitInvocationExpression(InvocationExpressionSyntax node)
    {
        if (IsOptionMarker(node)) return Visit(node.ArgumentList.Arguments[0].Expression);
        if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" } && _model.GetConstantValue(node) is { HasValue: true, Value: string constant })
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(constant)).WithTriviaFrom(node);
        if (_model.GetSymbolInfo(node).Symbol is IMethodSymbol { ReturnsByRef: true } method &&
            method.ContainingType.Name.StartsWith("LuminPackExtensions_", StringComparison.Ordinal) &&
            method.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax { ExpressionBody.Expression: RefExpressionSyntax expression } &&
            method.Parameters.Length == 1 && node.ArgumentList.Arguments.Count == 1)
        {
            var body = (ExpressionSyntax)new AccessorSubstitution(_model, _layouts, method.Parameters[0],
                (ExpressionSyntax)Visit(node.ArgumentList.Arguments[0].Expression), typeArguments: _typeArguments).Visit(expression.Expression);
            return SyntaxFactory.ParenthesizedExpression(body).WithTriviaFrom(node);
        }
        if (_model.GetSymbolInfo(node).Symbol is IMethodSymbol resolver && resolver.Name.StartsWith("ResolveCase_", StringComparison.Ordinal) &&
            resolver.ContainingType.Name.StartsWith("LuminPackExtensions_", StringComparison.Ordinal) && resolver.Parameters.Length == 1 &&
            resolver.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is MethodDeclarationSyntax { ExpressionBody: not null } declaration)
        {
            var resolvedExpression = new AccessorSubstitution(_model, _layouts, resolver.Parameters[0],
                (ExpressionSyntax)Visit(node.ArgumentList.Arguments[0].Expression), BinaryOperationPlan.Prefix + "helper" + _expressionHelpers++ + "_", _typeArguments)
                .Visit(declaration.ExpressionBody.Expression);
            return SyntaxFactory.ParenthesizedExpression((ExpressionSyntax)resolvedExpression).WithTriviaFrom(node);
        }
        return base.VisitInvocationExpression(node);
    }

    /// <summary>Recognizes the exact generated profile identity without matching user member names.</summary>
    /// <param name="node">A temporary generated identity invocation.</param>
    /// <returns>Whether it is the compiler-bound member scope marker.</returns>
    private static bool IsOptionMarker(InvocationExpressionSyntax node) => node.Expression is MemberAccessExpressionSyntax
    {
        Name: GenericNameSyntax { TypeArgumentList.Arguments.Count: 2 }
    } access && access.Expression.ToString() == "global::LuminPack.Code.LuminPackOptionScope";

    /// <summary>Marks independent binary staging writers before semantic information is discarded.</summary>
    /// <param name="node">A generated state construction, including aliases of the runtime type.</param>
    /// <returns>The unchanged construction with its compiler-proven writer identity when applicable.</returns>
    public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
    {
        var result = base.VisitObjectCreationExpression(node);
        return _model.GetTypeInfo(node).Type?.ToDisplayString() == "LuminPack.Core.LuminPackWriter"
            ? result.WithAdditionalAnnotations(new SyntaxAnnotation(BinaryCollectionCapacity.WriterConstructionAnnotation)) : result;
    }

    /// <summary>Renames local and private-layout references using compiler identity rather than identifier text.</summary>
    /// <param name="node">The original identifier.</param>
    /// <returns>The stable template identifier.</returns>
    public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
    {
        if (node.Identifier.Text == "var") return base.VisitIdentifierName(node);
        var symbol = _model.GetSymbolInfo(node).Symbol;
        if (symbol is ITypeParameterSymbol parameter && _typeArguments.TryGetValue(parameter.Name, out string argument))
            return SyntaxFactory.ParseTypeName(argument).WithTriviaFrom(node);
        string name = Replacement(symbol);
        if (name == null) return base.VisitIdentifierName(node);
        var token = BinaryOperationPlan.Identity(name, node.Identifier);
        var state = symbol is IParameterSymbol parameterState ? parameterState.Type : (symbol as ILocalSymbol)?.Type;
        string stateName = state?.ToDisplayString();
        if (stateName is "LuminPack.Core.LuminPackWriter" or "LuminPack.Core.LuminPackReader" or "LuminPack.Core.LuminPackEvaluator" or
            "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader")
            token = token.WithAdditionalAnnotations(new SyntaxAnnotation(StateAnnotation, stateName));
        if (state?.ToDisplayString() == "LuminPack.Core.LuminPackWriter")
            token = token.WithAdditionalAnnotations(new SyntaxAnnotation(BinaryCollectionCapacity.WriterAnnotation));
        return node.WithIdentifier(token);
    }

    /// <summary>Relocates constructed private mirror type names while preserving type arguments.</summary>
    /// <param name="node">The original constructed type.</param>
    /// <returns>The relocated generic name.</returns>
    public override SyntaxNode VisitGenericName(GenericNameSyntax node)
    {
        string name = Replacement(_model.GetSymbolInfo(node).Symbol);
        var result = (GenericNameSyntax)base.VisitGenericName(node);
        return name == null ? result : result.WithIdentifier(BinaryOperationPlan.Identity(name, node.Identifier));
    }

    /// <summary>Preserves nullable annotations on unconstrained parameters without turning an int substitution into Nullable&lt;int&gt;.</summary>
    /// <param name="node">The original nullable type syntax.</param>
    /// <returns>The original closed parameter type, or a real Nullable construction when the original contract used one.</returns>
    public override SyntaxNode VisitNullableType(NullableTypeSyntax node)
        => _model.GetTypeInfo(node).Type is ITypeParameterSymbol parameter && _typeArguments.TryGetValue(parameter.Name, out string argument)
            ? SyntaxFactory.ParseTypeName(argument).WithTriviaFrom(node) : base.VisitNullableType(node);

    /// <summary>Renames declarations to the same identities as their bound references.</summary>
    /// <param name="token">A token in the original template.</param>
    /// <returns>The renamed declaration token or the original token.</returns>
    public override SyntaxToken VisitToken(SyntaxToken token)
    {
        var parent = token.Parent;
        bool declaration = parent switch
        {
            VariableDeclaratorSyntax value => value.Identifier == token,
            ForEachStatementSyntax value => value.Identifier == token,
            SingleVariableDesignationSyntax value => value.Identifier == token,
            CatchDeclarationSyntax value => value.Identifier == token,
            ParameterSyntax value => value.Identifier == token,
            TypeDeclarationSyntax value => value.Identifier == token,
            MethodDeclarationSyntax value => value.Identifier == token,
            LocalFunctionStatementSyntax value => value.Identifier == token,
            LabeledStatementSyntax value => value.Identifier == token,
            _ => false
        };
        string name = declaration ? Replacement(_model.GetDeclaredSymbol(parent)) : null;
        return name == null ? base.VisitToken(token) : BinaryOperationPlan.Identity(name, token);
    }

    /// <summary>Looks up a frozen name for a local symbol or a constructed layout type.</summary>
    /// <param name="symbol">The bound source symbol.</param>
    /// <returns>A stable replacement name, or null when no relocation is required.</returns>
    private string Replacement(ISymbol symbol)
    {
        if (symbol == null) return null;
        if (_names.TryGetValue(symbol, out string local)) return local;
        if (_layouts.TryGetValue(symbol, out string layout)) return layout;
        return symbol is INamedTypeSymbol named && _layouts.TryGetValue(named.OriginalDefinition, out layout) ? layout : null;
    }

    /// <summary>Records whether an omitted in-modifier still denotes aliasable storage.</summary>
    /// <param name="expression">The original argument expression.</param>
    /// <returns>Whether expansion must preserve a reference alias rather than copy the value.</returns>
    private bool CanAlias(ExpressionSyntax expression)
    {
        if (expression is InvocationExpressionSyntax marker && IsOptionMarker(marker)) return CanAlias(marker.ArgumentList.Arguments[0].Expression);
        if (expression is PostfixUnaryExpressionSyntax suppressed && suppressed.IsKind(SyntaxKind.SuppressNullableWarningExpression)) return CanAlias(suppressed.Operand);
        if (expression is ParenthesizedExpressionSyntax parenthesized) return CanAlias(parenthesized.Expression);
        if (expression is ElementAccessExpressionSyntax or ThisExpressionSyntax) return true;
        return _model.GetSymbolInfo(expression).Symbol is ILocalSymbol or IParameterSymbol or IFieldSymbol or
            IMethodSymbol { ReturnsByRef: true } or IPropertySymbol { ReturnsByRef: true };
    }

    /// <summary>Substitutes the single accessor argument without adding a generated method call.</summary>
    private sealed class AccessorSubstitution : CSharpSyntaxRewriter
    {
        private readonly SemanticModel _model;
        private readonly Dictionary<ISymbol, string> _layouts;
        private readonly IParameterSymbol _parameter;
        private readonly ExpressionSyntax _argument;
        private readonly string _localPrefix;
        private readonly Dictionary<string, string> _typeArguments;

        /// <summary>Captures the temporary accessor binding and its already projected argument.</summary>
        /// <param name="model">The accessor template model.</param>
        /// <param name="layouts">Layout type relocations.</param>
        /// <param name="parameter">The single formal parameter.</param>
        /// <param name="argument">The actual field owner expression.</param>
        /// <param name="localPrefix">An independent identity for expression-helper pattern variables.</param>
        /// <param name="typeArguments">Closed arguments for the containing generic operation.</param>
        internal AccessorSubstitution(SemanticModel model, Dictionary<ISymbol, string> layouts, IParameterSymbol parameter, ExpressionSyntax argument,
            string localPrefix = null, Dictionary<string, string> typeArguments = null)
        { _model = model; _layouts = layouts; _parameter = parameter; _argument = argument; _localPrefix = localPrefix; _typeArguments = typeArguments; }

        /// <summary>Substitutes the field owner and relocates the private mirror type.</summary>
        /// <param name="node">The original accessor identifier.</param>
        /// <returns>The corresponding direct expression or identifier.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            var symbol = _model.GetSymbolInfo(node).Symbol;
            if (SymbolEqualityComparer.Default.Equals(symbol, _parameter)) return _argument.WithTriviaFrom(node);
            if (symbol is ITypeParameterSymbol parameter && _typeArguments != null && _typeArguments.TryGetValue(parameter.Name, out string argument))
                return SyntaxFactory.ParseTypeName(argument).WithTriviaFrom(node);
            if (_localPrefix != null && symbol is ILocalSymbol) return node.WithIdentifier(BinaryOperationPlan.Identity(_localPrefix + symbol.Name, node.Identifier));
            return symbol != null && _layouts.TryGetValue(symbol, out string name)
                ? node.WithIdentifier(BinaryOperationPlan.Identity(name, node.Identifier)) : base.VisitIdentifierName(node);
        }

        /// <summary>Renames an expression helper's out-variable declaration with the matching independent identity.</summary>
        /// <param name="node">The original pattern or out declaration.</param>
        /// <returns>The same declaration with an operation-local name.</returns>
        public override SyntaxNode VisitSingleVariableDesignation(SingleVariableDesignationSyntax node)
            => _localPrefix == null ? base.VisitSingleVariableDesignation(node) : node.WithIdentifier(BinaryOperationPlan.Identity(_localPrefix + node.Identifier.ValueText, node.Identifier));

        /// <summary>Relocates constructed private mirror names used by a ref-return accessor.</summary>
        /// <param name="node">The original mirror type reference.</param>
        /// <returns>The private serializer mirror reference with unchanged arguments.</returns>
        public override SyntaxNode VisitGenericName(GenericNameSyntax node)
        {
            var symbol = _model.GetSymbolInfo(node).Symbol as INamedTypeSymbol;
            var result = (GenericNameSyntax)base.VisitGenericName(node);
            return symbol != null && _layouts.TryGetValue(symbol.OriginalDefinition, out string name)
                ? result.WithIdentifier(BinaryOperationPlan.Identity(name, node.Identifier)) : result;
        }

        /// <summary>Substitutes annotated unconstrained parameters without introducing value-type null framing.</summary>
        /// <param name="node">The accessor's original nullable annotation.</param>
        /// <returns>The actual closed parameter type.</returns>
        public override SyntaxNode VisitNullableType(NullableTypeSyntax node)
            => _model.GetTypeInfo(node).Type is ITypeParameterSymbol parameter && _typeArguments != null && _typeArguments.TryGetValue(parameter.Name, out string argument)
                ? SyntaxFactory.ParseTypeName(argument).WithTriviaFrom(node) : base.VisitNullableType(node);
    }
}

/// <summary>Freezes only the active framework branch so directives cannot become unbalanced when a body is relocated.</summary>
internal sealed class ActiveOperationSyntax : CSharpSyntaxRewriter
{
    /// <summary>Discards conditional directives and inactive source after the consumer's parse settings have selected the branch.</summary>
    /// <param name="trivia">Original template trivia.</param>
    /// <returns>Active comments and whitespace, or empty trivia for preprocessor-only input.</returns>
    public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        => trivia.IsDirective || trivia.IsKind(SyntaxKind.DisabledTextTrivia) ? default : base.VisitTrivia(trivia);
}
