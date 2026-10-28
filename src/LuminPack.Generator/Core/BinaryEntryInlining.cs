using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>Expands typed operation plans directly into one concrete serializer contribution.</summary>
internal sealed class BinaryEntryInlining
{
    internal const string OperationLocalAnnotation = "LuminPack.ExpandedOperationLocal";
    internal const string ExpandedIdentityAnnotation = "LuminPack.ExpandedIdentity";
    internal const string OptionScopeAnnotation = "LuminPack.CompiledMemberProfile";
    internal const string PayloadAnnotation = "LuminPack.OperationPayload";
    private static readonly SyntaxAnnotation CursorLocal = new SyntaxAnnotation(OperationLocalAnnotation);
    private static readonly SyntaxAnnotation ExpandedIdentity = new SyntaxAnnotation(ExpandedIdentityAnnotation);
    private readonly Compilation _compilation;
    private readonly MetaInfo _metadata;
    private readonly BinaryOperationPlanCache _cache;
    private readonly ITypeSymbol _root;
    private readonly bool _streaming;
    private int _reservedExpansionDepth;
    private int _inputExpansionDepth;
    private readonly Dictionary<string, ITypeSymbol> _types = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
    private readonly Dictionary<string, BinaryOperationPlan> _plans = new Dictionary<string, BinaryOperationPlan>(StringComparer.Ordinal);
    private readonly Dictionary<string, MemberDeclarationSyntax> _layouts = new Dictionary<string, MemberDeclarationSyntax>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _inputReaders = new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly string _layoutPrefix;
    private int _next;

    /// <summary>Creates an emission-local context; no semantic objects are retained by completed facade output.</summary>
    /// <param name="root">The concrete overload type.</param>
    /// <param name="compilation">The current input compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="cache">Dependency-guarded immutable operation plans.</param>
    internal BinaryEntryInlining(ITypeSymbol root, Compilation compilation, MetaInfo metadata, BinaryOperationPlanCache cache)
    {
        _root = root; _compilation = compilation; _metadata = metadata; _cache = cache;
        _streaming = SafeReadProjection.ContainsPolicy(root, compilation, metadata);
        _types.Add(FormatterTypeName.Get(root), root);
        using (var hash = SHA256.Create())
            _layoutPrefix = "__LuminLayout_" + string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(FormatterTypeName.Get(root)))
                .Select(value => value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))) + "_";
    }

    /// <summary>Expands a retained formatter operation into its unique state-based serializer implementation.</summary>
    /// <param name="operation">The original formatter operation identity.</param>
    /// <returns>The expanded syntax block, or null when the original fallback must be retained.</returns>
    internal BlockSyntax ExpandCore(string operation)
    {
        string state = operation == "CalculateOffset" ? "evaluator" : operation.StartsWith("Read", StringComparison.Ordinal) ? "reader" : "writer";
        var expanded = Expand(GetPlan(_root), operation, new ExpressionSyntax[] { SyntaxFactory.IdentifierName(state), SyntaxFactory.IdentifierName("value") },
            new[] { true, true }, new HashSet<string>(StringComparer.Ordinal), root: true);
        if (expanded == null) return null;
        if (operation.StartsWith("Read", StringComparison.Ordinal) && !operation.EndsWith(":json", StringComparison.Ordinal))
            expanded = BinaryEntryPayloadAliases.Rewrite(expanded);
        expanded = (BlockSyntax)new ReservedCapacityRegions().Visit(expanded);
        return CompiledOptionProjection.Apply(expanded, CompiledOptions.Resolve(_root, _compilation), state,
            operation == "CalculateOffset" ? "LuminPackEvaluator" : operation.EndsWith(":json", StringComparison.Ordinal)
                ? operation.StartsWith("Read", StringComparison.Ordinal) ? "LuminPackJsonReader" : "LuminPackJsonWriter"
                : operation.StartsWith("Read", StringComparison.Ordinal) ? "LuminPackReader" : "LuminPackWriter");
    }

    /// <summary>Validates an operation plan once within this immutable emission context.</summary>
    /// <param name="type">The current root or nested operation type.</param>
    /// <returns>The dependency-guarded plan shared by all entries for this root.</returns>
    private BinaryOperationPlan GetPlan(ITypeSymbol type)
    {
        string name = FormatterTypeName.Get(type);
        if (!_plans.TryGetValue(name, out var plan))
            _plans.Add(name, plan = _cache.Get(type, _compilation, _metadata, _streaming));
        return plan;
    }

    /// <summary>Gets private layout syntax after all selected operation bodies have been expanded.</summary>
    internal IEnumerable<MemberDeclarationSyntax> Layouts => _layouts.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Value);

    /// <summary>Retains a guarded recursive boundary without forwarding into an unguarded trusted reader.</summary>
    /// <param name="plan">The selected streaming template.</param><param name="operationName">The original read operation.</param>
    /// <returns>A private generated helper name.</returns>
    private string InputReader(BinaryOperationPlan plan, string operationName)
    {
        string key = plan.TypeName + "|" + operationName;
        if (_inputReaders.TryGetValue(key, out string existing)) return existing;
        string name = "__InputRead_" + _layoutPrefix.Substring(14, 16) + "_" + _inputReaders.Count;
        _inputReaders.Add(key, name);
        string selected = operationName;
        if (plan.Options.Input.Enabled)
        {
            var inner = plan.Operations.Where(pair => pair.Key.StartsWith(operationName + ":local:", StringComparison.Ordinal))
                .OrderBy(pair => int.Parse(pair.Key.Substring(pair.Key.LastIndexOf(':') + 1), System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault();
            if (inner.Value != null) selected = inner.Key;
        }
        var operation = plan.Operations[selected];
        var body = Expand(plan, selected, new ExpressionSyntax[] { SyntaxFactory.IdentifierName("reader"), SyntaxFactory.IdentifierName("value") },
            new[] { true, true }, new HashSet<string>(StringComparer.Ordinal), root: true);
        string scoped = _metadata.IsNet8 ? "scoped " : "";
        var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("private static void " + name + "(" + scoped + "ref " + operation.Parameters[0].Type + " reader, " +
            scoped + "ref " + operation.Parameters[1].Type + " value) {} ");
        _layouts.Add(name, method.WithBody(body));
        return name;
    }

    /// <summary>Instantiates an operation with independent locals, reference aliases, and a local return target.</summary>
    /// <param name="plan">The closed type's reusable template set.</param>
    /// <param name="operationName">The normal, specialized, or local operation.</param>
    /// <param name="arguments">Already evaluated caller expressions in original argument order.</param>
    /// <param name="aliases">Arguments with referenceable storage.</param>
    /// <param name="stack">The current recursive expansion path.</param>
    /// <param name="optionProfile">A containing member's statically selected profile, if present.</param>
    /// <param name="root">Whether the operation already owns the serializer's parameter bindings and return boundary.</param>
    /// <returns>The expanded block, or null at a retained recursive/custom boundary.</returns>
    private BlockSyntax Expand(BinaryOperationPlan plan, string operationName, IReadOnlyList<ExpressionSyntax> arguments,
        IReadOnlyList<bool> aliases, HashSet<string> stack, string optionProfile = null, bool root = false)
    {
        string key = plan.TypeName + "|" + operationName;
        if (stack.Contains(key) || !plan.Operations.TryGetValue(operationName, out var operation)) return null;
        if (!root && _inputExpansionDepth > 0 && plan.Options.Input.Enabled && !operationName.Contains(":local:"))
        {
            var inner = plan.Operations.Where(pair => pair.Key.StartsWith(operationName + ":local:", StringComparison.Ordinal))
                .OrderBy(pair => int.Parse(pair.Key.Substring(pair.Key.LastIndexOf(':') + 1), System.Globalization.CultureInfo.InvariantCulture)).FirstOrDefault();
            if (inner.Value != null) operation = inner.Value;
        }
        if (!_streaming && !root && (GeneratedExecutionPolicy.Reuse(operationName, operation) ||
            _reservedExpansionDepth == 0 && !operationName.Contains(":local:") &&
            CodeEmitterRegistry.GetEmitter(plan.TypeName).Read != null)) return null;
        if (arguments.Count != operation.Parameters.Length)
            return LuminPackExceptionHelper.ThrowInvalidOperationException<BlockSyntax>("Inline operation parameter mismatch: " + key);
        var requiredSupport = new HashSet<string>(operation.Body.DescendantTokens().Where(token => token.GetAnnotations(BinaryOperationPlan.NameAnnotation).Any())
            .Select(token => token.ValueText), StringComparer.Ordinal);
        var pendingSupport = plan.Layouts.ToList();
        for (int index = 0; index < pendingSupport.Count;)
        {
            var layout = pendingSupport[index];
            string name = SupportName(layout);
            if (!requiredSupport.Contains(name)) { index++; continue; }
            string identity = layout switch
            {
                TypeDeclarationSyntax type => type.Identifier.ValueText + "`" + (type.TypeParameterList?.Parameters.Count ?? 0),
                MethodDeclarationSyntax method => method.Identifier.ValueText,
                FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
                _ => LuminPackExceptionHelper.ThrowInvalidOperationException<string>("Unsupported binary operation support declaration.")
            };
            if (!_layouts.ContainsKey(identity)) _layouts.Add(identity, (MemberDeclarationSyntax)new InstanceNames(string.Empty, _layoutPrefix).Visit(layout));
            foreach (var token in layout.DescendantTokens().Where(token => token.GetAnnotations(BinaryOperationPlan.NameAnnotation).Any()))
                requiredSupport.Add(token.ValueText);
            pendingSupport.RemoveAt(index);
            index = 0;
        }
        string prefix = "__lp" + _next++ + "_";
        bool guaranteed = plan.GuaranteedMaxBytes > 0 && operation.Parameters[0].Type == "global::LuminPack.Core.LuminPackWriter" &&
            !operationName.Contains(":local:");
        string exit = root && !guaranteed ? null : prefix + "exit";
        var statements = new List<StatementSyntax>();
        var rootBindings = root ? new Dictionary<string, ExpressionSyntax>(StringComparer.Ordinal) : null;
        for (int index = 0; index < arguments.Count; index++)
        {
            var parameter = operation.Parameters[index];
            if (root)
            {
                rootBindings.Add(parameter.Name, arguments[index]);
                continue;
            }
            if (index == 0 && IsCoreState(parameter)) continue;
            string name = prefix + parameter.Name.Substring(BinaryOperationPlan.Prefix.Length);
            string argument = arguments[index].WithoutTrivia().ToString();
            string declaration = parameter.RefKind switch
            {
                RefKind.Ref => "ref " + parameter.Type + " " + name + " = ref " + argument + ";",
                RefKind.In when aliases[index] => "ref readonly " + parameter.Type + " " + name + " = ref " + argument + ";",
                RefKind.None or RefKind.In => parameter.Type + " " + name + " = " + argument + ";",
                _ => LuminPackExceptionHelper.ThrowInvalidOperationException<string>("Unsupported inline parameter passing convention: " + parameter.RefKind)
            };
            var binding = (LocalDeclarationStatementSyntax)SyntaxFactory.ParseStatement(declaration);
            var variable = binding.Declaration.Variables[0];
            variable = variable.WithIdentifier(variable.Identifier.WithAdditionalAnnotations(ExpandedIdentity,
                new SyntaxAnnotation(GeneratedOperationNames.RoleAnnotation, parameter.OriginalName)))
                .WithInitializer(SyntaxFactory.EqualsValueClause(parameter.RefKind == RefKind.Ref || parameter.RefKind == RefKind.In && aliases[index]
                    ? SyntaxFactory.RefExpression(arguments[index]) : arguments[index]));
            if (parameter.RefKind == RefKind.Ref)
                variable = variable.WithIdentifier(variable.Identifier.WithAdditionalAnnotations(new SyntaxAnnotation(BinaryEntryPayloadAliases.BindingAnnotation)));
            binding = binding.WithDeclaration(binding.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable)));
            statements.Add(binding);
        }
        var path = new HashSet<string>(stack, StringComparer.Ordinal) { key };
        var template = operation.Body;
        // The write entry already snapshots its input once. Reuse that storage only when
        // the template's first statement is its sole read of the original formal parameter.
        if (root && operation.Parameters.Length == 2 && operation.Parameters[1].RefKind == RefKind.In &&
            template.Statements.FirstOrDefault() is LocalDeclarationStatementSyntax snapshot &&
            snapshot.Declaration.Type is not RefTypeSyntax && snapshot.Declaration.Variables.Count == 1 &&
            snapshot.Declaration.Variables[0].Initializer?.Value is IdentifierNameSyntax input &&
            input.Identifier.ValueText == operation.Parameters[1].Name && input.Identifier.HasAnnotations(BinaryOperationPlan.NameAnnotation) &&
            template.DescendantNodes().OfType<IdentifierNameSyntax>().Count(identifier => identifier.Identifier.ValueText == input.Identifier.ValueText &&
                identifier.Identifier.HasAnnotations(BinaryOperationPlan.NameAnnotation)) == 1)
        {
            rootBindings.Add(snapshot.Declaration.Variables[0].Identifier.ValueText, arguments[1]);
            template = template.WithStatements(template.Statements.RemoveAt(0));
        }
        BlockSyntax body;
        if (guaranteed) _reservedExpansionDepth++;
        bool inputBoundary = plan.Options.Input.Enabled;
        if (inputBoundary) _inputExpansionDepth++;
        try { body = (BlockSyntax)new OperationExpansion(this, plan, operation, prefix, exit, path, arguments[0], rootBindings).Visit(template); }
        finally { if (guaranteed) _reservedExpansionDepth--; if (inputBoundary) _inputExpansionDepth--; }
        if (optionProfile != null && !operationName.EndsWith(":json", StringComparison.Ordinal))
            body = CompiledOptionProjection.Apply(body, plan.Options, arguments[0].ToString(), operation.Parameters[0].Type, optionProfile);
        if (guaranteed)
        {
            string state = arguments[0].ToString();
            statements.Add(SyntaxFactory.ParseStatement("int " + prefix + "boundedStart = " + state + ".GetCurrentSpanIndex();"));
            statements.Add(SyntaxFactory.ParseStatement(state + ".EnsureAdditionalCapacity(" + plan.GuaranteedMaxBytes + ");"));
            statements.Add(body.WithAdditionalAnnotations(new SyntaxAnnotation(BinaryCollectionCapacity.ReservedAnnotation),
                new SyntaxAnnotation(BinaryCollectionCapacity.GuaranteedAnnotation, plan.GuaranteedMaxBytes.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
        else statements.AddRange(body.Statements);
        if (body.DescendantNodes().OfType<GotoStatementSyntax>().Any(jump => jump.Expression is IdentifierNameSyntax target && target.Identifier.ValueText == exit))
            statements.Add(SyntaxFactory.LabeledStatement(SyntaxFactory.Identifier(exit).WithAdditionalAnnotations(
                new SyntaxAnnotation(GeneratedOperationNames.RoleAnnotation, "end")), SyntaxFactory.EmptyStatement()));
        if (guaranteed)
        {
            string state = arguments[0].ToString();
            statements.Add(SyntaxFactory.ParseStatement("global::System.Diagnostics.Debug.Assert(" + state + ".GetCurrentSpanIndex() - " + prefix +
                "boundedStart <= " + plan.GuaranteedMaxBytes + ", \"The LuminPackBounded complete-record guarantee was violated.\");"));
            statements.Add(SyntaxFactory.ParseStatement(state + ".CompleteReservedRecord();"));
        }
        Interlocked.Increment(ref IncrementalGenerationMetrics.BinaryOperationExpansions);
        var result = SyntaxFactory.Block(statements);
        return optionProfile == null ? result : result.WithAdditionalAnnotations(new SyntaxAnnotation(OptionScopeAnnotation, optionProfile));
    }

    /// <summary>Obtains the compiler-marked identity of one private operation support declaration.</summary>
    /// <param name="declaration">A mirror, constant field, or initialization helper.</param>
    /// <returns>The declared support identity.</returns>
    private static string SupportName(MemberDeclarationSyntax declaration) => declaration switch
    {
        TypeDeclarationSyntax type => type.Identifier.ValueText,
        MethodDeclarationSyntax method => method.Identifier.ValueText,
        FieldDeclarationSyntax field => field.Declaration.Variables[0].Identifier.ValueText,
        _ => LuminPackExceptionHelper.ThrowInvalidOperationException<string>("Unsupported operation support declaration.")
    };

    /// <summary>Identifies the ref-struct state whose original escape scope must survive expansion.</summary>
    /// <param name="parameter">The original operation parameter.</param>
    /// <returns>Whether the parameter is the writer or reader state.</returns>
    private static bool IsCoreState(BinaryOperationParameter parameter) => parameter.RefKind == RefKind.Ref &&
        parameter.Type is "global::LuminPack.Core.LuminPackWriter" or "global::LuminPack.Core.LuminPackReader" or "global::LuminPack.Core.LuminPackEvaluator" or
            "global::LuminPack.Core.LuminPackJsonWriter" or "global::LuminPack.Core.LuminPackJsonReader";

    /// <summary>Resolves a cached canonical call identity back to a current symbol only when an affected entry is rebuilt.</summary>
    /// <param name="name">The frozen fully qualified operation type.</param>
    /// <returns>The current type symbol.</returns>
    private ITypeSymbol Resolve(string name)
    {
        if (_types.TryGetValue(name, out var type)) return type;
        var tree = _compilation.SyntaxTrees.FirstOrDefault();
        type = tree == null ? null : _compilation.GetSemanticModel(tree).GetSpeculativeTypeInfo(0,
            SyntaxFactory.ParseTypeName(name), SpeculativeBindingOption.BindAsTypeOrNamespace).Type;
        if (type == null || type.TypeKind == TypeKind.Error)
            return LuminPackExceptionHelper.ThrowInvalidOperationException<ITypeSymbol>("Cannot resolve a cached inline operation type: " + name);
        _types.Add(name, type);
        return type;
    }

    /// <summary>Assigns entry-local identities to formal/local slots and private layout references.</summary>
    private class InstanceNames : CSharpSyntaxRewriter
    {
        private readonly string _prefix;
        private readonly string _layoutPrefix;
        private readonly System.Collections.Immutable.ImmutableHashSet<string> _cursorLocals;

        /// <summary>Captures the operation instance and concrete root's layout namespace.</summary>
        /// <param name="prefix">The unique operation instance prefix.</param>
        /// <param name="layoutPrefix">The stable root-specific private mirror prefix.</param>
        /// <param name="cursorLocals">Only integer cursor identities requiring later lifetime normalization.</param>
        internal InstanceNames(string prefix, string layoutPrefix, System.Collections.Immutable.ImmutableHashSet<string> cursorLocals = null)
        { _prefix = prefix; _layoutPrefix = layoutPrefix; _cursorLocals = cursorLocals; }

        /// <summary>Renames only compiler-created template identities.</summary>
        /// <param name="token">The template token.</param>
        /// <returns>The concrete operation or layout identity.</returns>
        public override SyntaxToken VisitToken(SyntaxToken token)
        {
            string name = token.ValueText;
            if (!token.IsKind(SyntaxKind.IdentifierToken) || !token.GetAnnotations(BinaryOperationPlan.NameAnnotation).Any()) return base.VisitToken(token);
            string replacement = name.StartsWith(BinaryOperationPlan.Prefix + "layout_", StringComparison.Ordinal)
                ? _layoutPrefix + name.Substring((BinaryOperationPlan.Prefix + "layout_").Length)
                : _prefix + name.Substring(BinaryOperationPlan.Prefix.Length);
            var renamed = SyntaxFactory.Identifier(replacement).WithTriviaFrom(token).WithAdditionalAnnotations(ExpandedIdentity);
            if (!name.StartsWith(BinaryOperationPlan.Prefix + "layout_", StringComparison.Ordinal))
            {
                string role = token.GetAnnotations(BinaryOperationPlan.NameAnnotation).Select(annotation => annotation.Data).FirstOrDefault();
                if (!string.IsNullOrEmpty(role)) renamed = renamed.WithAdditionalAnnotations(new SyntaxAnnotation(GeneratedOperationNames.RoleAnnotation, role));
            }
            if (token.HasAnnotations(BinaryCollectionCapacity.WriterAnnotation))
                renamed = renamed.WithAdditionalAnnotations(new SyntaxAnnotation(BinaryCollectionCapacity.WriterAnnotation));
            renamed = renamed.WithAdditionalAnnotations(token.GetAnnotations(OperationTemplateRewriter.StateAnnotation).ToArray());
            return _cursorLocals?.Contains(name) == true ? renamed.WithAdditionalAnnotations(CursorLocal) : renamed;
        }
    }

    /// <summary>Expands typed slots without consulting a semantic model or revisiting unrelated generated files.</summary>
    private sealed class OperationExpansion : InstanceNames
    {
        private readonly BinaryEntryInlining _owner;
        private readonly BinaryOperationPlan _plan;
        private readonly BinaryOperation _operation;
        private readonly string _exit;
        private readonly HashSet<string> _stack;
        private readonly ExpressionSyntax _state;
        private readonly IReadOnlyDictionary<string, ExpressionSyntax> _rootBindings;

        /// <summary>Creates one operation instance's syntax emitter.</summary>
        /// <param name="owner">The current concrete contribution.</param>
        /// <param name="plan">The containing template set.</param>
        /// <param name="operation">The operation being instantiated.</param>
        /// <param name="prefix">Unique local identity prefix.</param>
        /// <param name="exit">The formatter-local return label.</param>
        /// <param name="stack">The current recursive path.</param>
        /// <param name="state">The original writer or reader storage, preserving its escape scope.</param>
        /// <param name="rootBindings">Original root parameter storage and any proven equivalent entry snapshot.</param>
        internal OperationExpansion(BinaryEntryInlining owner, BinaryOperationPlan plan, BinaryOperation operation, string prefix, string exit, HashSet<string> stack,
            ExpressionSyntax state, IReadOnlyDictionary<string, ExpressionSyntax> rootBindings = null)
            : base(prefix, owner._layoutPrefix, operation.CursorLocals)
        { _owner = owner; _plan = plan; _operation = operation; _exit = exit; _stack = stack; _state = state; _rootBindings = rootBindings; }

        /// <summary>Uses the caller's writer or reader directly without narrowing its ref-escape scope through a local alias.</summary>
        /// <param name="node">A template identifier.</param>
        /// <returns>The original state expression or an independently named local.</returns>
        public override SyntaxNode VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.HasAnnotations(BinaryOperationPlan.NameAnnotation))
            {
                if (_rootBindings != null && _rootBindings.TryGetValue(node.Identifier.ValueText, out var binding))
                    return binding.WithTriviaFrom(node);
                if (node.Identifier.ValueText == BinaryOperationPlan.Prefix + "p0" && IsCoreState(_operation.Parameters[0]))
                    return _state.WithTriviaFrom(node);
            }
            return base.VisitIdentifierName(node);
        }

        /// <summary>Expands ordinary formatter and static local helper calls; recursive/custom calls retain their original boundary.</summary>
        /// <param name="node">The frozen expression statement.</param>
        /// <returns>The complete nested operation block or the retained boundary call.</returns>
        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            var annotation = node.GetAnnotations(BinaryOperationPlan.CallAnnotation).SingleOrDefault();
            if (annotation == null) return base.VisitExpressionStatement(node);
            var call = (InvocationExpressionSyntax)node.Expression;
            var slot = _operation.Calls[annotation.Data];
            var arguments = call.ArgumentList.Arguments.Select(argument => (ExpressionSyntax)Visit(argument.Expression)).ToList();
            if (slot.Reduced) arguments.Insert(0, (ExpressionSyntax)Visit(((MemberAccessExpressionSyntax)call.Expression).Expression));
            var plan = slot.Local ? _plan : _owner.GetPlan(_owner.Resolve(slot.TypeName));
            var expanded = _owner.Expand(plan, slot.Operation, arguments, slot.Aliases, _stack, slot.OptionProfile);
            if (expanded != null) return expanded.WithTriviaFrom(node);
            if (_owner._streaming && !slot.Local && slot.Operation.StartsWith("Read", StringComparison.Ordinal) && plan.Operations.ContainsKey(slot.Operation))
            {
                string helper = _owner.InputReader(plan, slot.Operation);
                var guarded = SyntaxFactory.ExpressionStatement(SyntaxFactory.InvocationExpression(SyntaxFactory.IdentifierName(helper),
                    SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(arguments.Select(argument => SyntaxFactory.Argument(argument).WithRefKindKeyword(SyntaxFactory.Token(SyntaxKind.RefKeyword)))))));
                return slot.OptionProfile == null ? guarded : guarded.WithAdditionalAnnotations(new SyntaxAnnotation(OptionScopeAnnotation, slot.OptionProfile));
            }
            var retained = (ExpressionStatementSyntax)base.VisitExpressionStatement(node);
            retained = retained.WithExpression(retained.Expression.WithAdditionalAnnotations(new SyntaxAnnotation(PayloadAnnotation, slot.TypeName)));
            return slot.OptionProfile == null ? retained : retained.WithAdditionalAnnotations(new SyntaxAnnotation(OptionScopeAnnotation, slot.OptionProfile));
        }

        /// <summary>Converts formatter returns to local exits so entry completion and cleanup still execute.</summary>
        /// <param name="node">The formatter's return statement.</param>
        /// <returns>A formatter-local jump, or an unchanged nested-function return.</returns>
        public override SyntaxNode VisitReturnStatement(ReturnStatementSyntax node)
        {
            if (_exit == null || node.Ancestors().Any(parent => parent is LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)) return base.VisitReturnStatement(node);
            if (node.Expression != null)
                return LuminPackExceptionHelper.ThrowInvalidOperationException<SyntaxNode>("A void binary operation returned a value.");
            return SyntaxFactory.GotoStatement(SyntaxKind.GotoStatement, SyntaxFactory.IdentifierName(SyntaxFactory.Identifier(_exit)
                .WithAdditionalAnnotations(new SyntaxAnnotation(GeneratedOperationNames.RoleAnnotation, "end")))).WithTriviaFrom(node);
        }
    }
}
