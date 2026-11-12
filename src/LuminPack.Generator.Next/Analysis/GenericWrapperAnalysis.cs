using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Specializes source generic methods whose closed calls reach the serializer facade.</summary>
/// <remarks>This ports the original generator's <c>StaticSerializerBinding.Wrappers</c>. A facade call inside a generic method, such
/// as <c>Read&lt;T&gt;(byte[] b) =&gt; Deserialize&lt;Box&lt;T&gt;&gt;(b)</c>, names an open payload that no interceptor can bind. Each
/// closed source call of such a method, such as <c>Read&lt;double&gt;(bytes)</c>, is redirected to a generated copy of the method
/// whose type parameters are substituted, so its facade calls are closed and bind concrete entries. A method whose body could
/// change meaning when it is relocated or closed (implicit receivers, open operators, lambdas, awaits, inaccessible members,
/// static initialization) stays a boundary and keeps the generic facade, as in the original.</remarks>
internal static class GenericWrapperAnalysis
{
    /// <summary>The deepest chain of specialized generic calls before the expansion stops.</summary>
    private const int MaximumDepth = 64;

    /// <summary>Finds and specializes the wrappers of a compilation.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The valid wrappers and the source calls they replace.</returns>
    internal static WrapperSet Analyze(Compilation compilation, CancellationToken cancellationToken)
    {
        GeneratorOptions options = Pipeline.GeneratorOptionsFactory.From(compilation);
        if (!options.Interceptors || !SerializerCallAnalysis.Supported && !options.IlRewrite)
            return WrapperSet.Empty;
        var session = new Session(compilation, options.IlRewrite, cancellationToken);
        return session.Run();
    }

    /// <summary>One compilation's wrapper graph.</summary>
    private sealed class Session
    {
        private readonly Compilation _compilation;
        private readonly bool _il;
        private readonly CancellationToken _token;
        private readonly Dictionary<string, Wrapper> _wrappers = new(StringComparer.Ordinal);
        private readonly HashSet<string> _names = new(StringComparer.Ordinal);
        private readonly HashSet<IMethodSymbol> _serializing = new(SymbolEqualityComparer.Default);
        private readonly List<(Wrapper Wrapper, string Attribute)> _calls = new();

        internal Session(Compilation compilation, bool il, CancellationToken token)
        {
            _compilation = compilation;
            _il = il;
            _token = token;
        }

        /// <summary>Indexes the source call graph, specializes every closed entry call and validates the relocated bodies.</summary>
        internal WrapperSet Run()
        {
            HashSet<string> relevant = GenericPayloadExpansion.RelevantNames(_compilation, _token);
            var sources = new List<(InvocationExpressionSyntax Syntax, IMethodSymbol Method, SemanticModel Model)>();
            foreach (SyntaxTree tree in _compilation.SyntaxTrees)
            {
                _token.ThrowIfCancellationRequested();
                if (Generated(tree))
                    continue;
                SemanticModel? model = null;
                foreach (InvocationExpressionSyntax call in tree.GetRoot(_token).DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    string? name = GenericPayloadExpansion.InvocationName(call);
                    if (name == null || !relevant.Contains(name))
                        continue;
                    model ??= _compilation.GetSemanticModel(tree);
                    if (model.GetSymbolInfo(call, _token).Symbol is IMethodSymbol method)
                        sources.Add((call, method, model));
                }
            }

            // The methods whose bodies reach the facade, directly or through other source methods.
            var callers = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
            var pending = new Queue<IMethodSymbol>();
            foreach (var call in sources)
            {
                if (call.Model.GetEnclosingSymbol(call.Syntax.SpanStart, _token) is not IMethodSymbol owner)
                    continue;
                owner = owner.OriginalDefinition;
                IMethodSymbol target = call.Method.OriginalDefinition;
                if (!callers.TryGetValue(target, out HashSet<IMethodSymbol>? incoming))
                    callers[target] = incoming = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
                incoming.Add(owner);
                if (SerializerCallAnalysis.IsFacade(call.Method) && call.Method.Name != "Register" && _serializing.Add(owner))
                    pending.Enqueue(owner);
            }
            while (pending.Count != 0)
                if (callers.TryGetValue(pending.Dequeue(), out HashSet<IMethodSymbol>? incoming))
                    foreach (IMethodSymbol owner in incoming)
                        if (_serializing.Add(owner))
                            pending.Enqueue(owner);

            foreach (var call in sources)
            {
                _token.ThrowIfCancellationRequested();
                IMethodSymbol method = call.Method;
                if (!method.IsGenericMethod && method.ContainingType is not { IsGenericType: true } ||
                    !_serializing.Contains(method.OriginalDefinition) || !IsClosed(method))
                    continue;
                string? attribute = _il ? StaticCallKey.Attribute(method, -1) : SerializerCallAnalysis.Location(call.Model, call.Syntax, _token);
                if (attribute != null && Specialize(method, 0) is { } wrapper)
                    _calls.Add((wrapper, attribute));
            }
            Validate();
            RemoveInvalid();
            return Publish();
        }

        /// <summary>Creates one closed body for a safely relocatable source method construction.</summary>
        /// <param name="method">The closed source method.</param>
        /// <param name="depth">The specialization depth, bounding expanding generic recursion.</param>
        /// <returns>The shared wrapper, or null for a boundary.</returns>
        internal Wrapper? Specialize(IMethodSymbol method, int depth)
        {
            string key = TypeName(method.ContainingType) + "|" + Signature(method);
            if (_wrappers.TryGetValue(key, out Wrapper? previous))
                return previous.Valid ? previous : null;
            if (depth >= MaximumDepth || !method.IsStatic || method.IsVirtual || method.IsAbstract || method.IsExtern ||
                method.ContainingType.StaticConstructors.Any(constructor => !constructor.IsImplicitlyDeclared) ||
                method.ContainingType.GetMembers().Any(member => member.IsStatic && member.DeclaringSyntaxReferences.Any(reference =>
                    reference.GetSyntax(_token) is VariableDeclaratorSyntax { Initializer: not null } or PropertyDeclarationSyntax { Initializer: not null })) ||
                method.ReturnsByRef || method.ReturnsByRefReadonly || !IsClosed(method) || method.MethodKind != MethodKind.Ordinary ||
                method.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(_token) is not MethodDeclarationSyntax syntax ||
                syntax.AttributeLists.Count != 0 || syntax.Body == null && syntax.ExpressionBody == null ||
                syntax.DescendantNodes().Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or
                    AwaitExpressionSyntax or YieldStatementSyntax or BaseExpressionSyntax or ThisExpressionSyntax or QueryExpressionSyntax))
                return null;
            if (!Accessible(method.ReturnType) || method.TypeArguments.Any(argument => !Accessible(argument)) ||
                method.Parameters.Any(parameter => !Accessible(parameter.Type)))
                return null;
            var wrapper = new Wrapper(key, method, Name(method));
            _wrappers.Add(key, wrapper);
            var rewriter = new Body(this, wrapper, _compilation.GetSemanticModel(syntax.SyntaxTree), Arguments(method), depth);
            if (syntax.Body != null)
            {
                wrapper.Text = rewriter.Visit(syntax.Body).NormalizeWhitespace().ToFullString();
            }
            else
            {
                var expression = (ExpressionSyntax)rewriter.Visit(syntax.ExpressionBody!.Expression);
                wrapper.Text = SyntaxFactory.Block(method.ReturnsVoid
                    ? SyntaxFactory.ExpressionStatement(expression)
                    : SyntaxFactory.ReturnStatement(expression)).NormalizeWhitespace().ToFullString();
            }
            wrapper.Unsafe = syntax.Modifiers.Any(token => token.IsKind(SyntaxKind.UnsafeKeyword));
            wrapper.Valid = rewriter.Valid;
            return wrapper.Valid ? wrapper : null;
        }

        /// <summary>Names a wrapper after its method and closed arguments, such as <c>LuminWrapper_Read_Double</c>.</summary>
        private string Name(IMethodSymbol method)
        {
            var arguments = new List<string>();
            for (INamedTypeSymbol? owner = method.ContainingType; owner != null; owner = owner.ContainingType)
                arguments.InsertRange(0, owner.TypeArguments.Select(argument => ReadableTypeName.Of(TypeName(argument))));
            arguments.AddRange(method.TypeArguments.Select(argument => ReadableTypeName.Of(TypeName(argument))));
            string plain = "LuminWrapper_" + method.Name + (arguments.Count == 0 ? string.Empty : "_" + string.Join("And", arguments));
            string name = plain;
            for (int number = 2; !_names.Add(name); number++)
                name = plain + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return name;
        }

        /// <summary>Rejects relocations that do not bind in the generated scope, with every facade call spelled concretely.</summary>
        private void Validate()
        {
            if (!_wrappers.Values.Any(wrapper => wrapper.Valid))
                return;
            var text = new StringBuilder("namespace LuminPack { public static partial class LuminPackSerializer {\n");
            foreach (Wrapper wrapper in _wrappers.Values)
                if (wrapper.Valid)
                    text.Append(wrapper.Declaration()).Append('\n').Append(wrapper.Resolved(wrapper.Fallbacks)).Append('\n');
            text.Append("} }\n");
            var options = (CSharpParseOptions)_compilation.SyntaxTrees.First().Options;
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text.ToString(), options, cancellationToken: _token);
            Compilation validation = _compilation.AddSyntaxTrees(tree);
            var byName = new Dictionary<string, Wrapper>(StringComparer.Ordinal);
            foreach (Wrapper wrapper in _wrappers.Values)
                byName[wrapper.Name] = wrapper;
            foreach (Diagnostic error in validation.GetSemanticModel(tree).GetDiagnostics(cancellationToken: _token))
            {
                if (error.Severity != DiagnosticSeverity.Error)
                    continue;
                var declaration = tree.GetRoot(_token).FindNode(error.Location.SourceSpan).AncestorsAndSelf().OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault();
                if (declaration != null && byName.TryGetValue(declaration.Identifier.ValueText, out Wrapper? wrapper))
                    wrapper.Valid = false;
            }
        }

        /// <summary>Propagates boundaries through wrappers that call other wrappers.</summary>
        private void RemoveInvalid()
        {
            bool changed;
            do
            {
                changed = false;
                foreach (Wrapper wrapper in _wrappers.Values)
                    if (wrapper.Valid && wrapper.Dependencies.Any(dependency => !dependency.Valid))
                    {
                        wrapper.Valid = false;
                        changed = true;
                    }
            }
            while (changed);
        }

        /// <summary>Freezes the valid wrappers into value-equal models.</summary>
        private WrapperSet Publish()
        {
            var locations = new Dictionary<Wrapper, SortedSet<string>>();
            foreach (var call in _calls)
                if (call.Wrapper.Valid)
                {
                    if (!locations.TryGetValue(call.Wrapper, out SortedSet<string>? set))
                        locations[call.Wrapper] = set = new SortedSet<string>(StringComparer.Ordinal);
                    set.Add(call.Attribute);
                }
            var models = new List<WrapperModel>();
            foreach (Wrapper wrapper in _wrappers.Values)
                if (wrapper.Valid)
                    models.Add(new WrapperModel(wrapper.Name, wrapper.Declaration(), wrapper.Text,
                        locations.TryGetValue(wrapper, out SortedSet<string>? set) ? set.ToArray() : Array.Empty<string>(),
                        wrapper.Calls.ToArray(), wrapper.Fallbacks.ToArray()));
            if (models.Count == 0)
                return WrapperSet.Empty;
            models.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return new WrapperSet(models.ToArray());
        }

        /// <summary>Whether a tree is generated by this generator.</summary>
        private static bool Generated(SyntaxTree tree) =>
            tree.FilePath.IndexOf("LuminPack.Generator.Next/", StringComparison.Ordinal) >= 0 ||
            tree.FilePath.IndexOf("LuminPack.Generator.Next" + (char)92, StringComparison.Ordinal) >= 0;

        /// <summary>Whether generated assembly-level code can reference a symbol.</summary>
        internal bool Accessible(ISymbol symbol) => _compilation.IsSymbolAccessibleWithin(symbol, _compilation.Assembly);

        /// <summary>Substitutes method and containing-type arguments together.</summary>
        internal ITypeSymbol Resolve(ITypeSymbol type, Dictionary<ITypeParameterSymbol, ITypeSymbol> map) =>
            GenericPayloadExpansion.Substitute(_compilation, type, map);

        /// <summary>Binds a closed facade call of a wrapper body.</summary>
        internal SerializerCallModel? Facade(IMethodSymbol method) => SerializerCallAnalysis.Closed(method, _compilation);

        /// <summary>Whether a method reaches the facade.</summary>
        internal bool Serializing(IMethodSymbol method) => _serializing.Contains(method.OriginalDefinition);
    }

    /// <summary>One closed copy shared by all matching source calls.</summary>
    private sealed class Wrapper
    {
        internal readonly string Key;
        internal readonly IMethodSymbol Method;
        internal readonly string Name;
        internal readonly List<Wrapper> Dependencies = new();
        internal readonly List<SerializerCallModel> Calls = new();
        internal readonly List<string> Fallbacks = new();
        internal string Text = string.Empty;
        internal bool Unsafe;
        internal bool Valid = true;

        internal Wrapper(string key, IMethodSymbol method, string name)
        {
            Key = key;
            Method = method;
            Name = name;
        }

        /// <summary>The declaration line of the wrapper.</summary>
        internal string Declaration() => "internal static " + (Unsafe ? "unsafe " : string.Empty) + TypeName(Method.ReturnType) + " " + Name + "(" +
            string.Join(", ", Method.Parameters.Select(Parameter)) + ")";

        /// <summary>The body with each placeholder replaced by the given spelling.</summary>
        internal string Resolved(IReadOnlyList<string> targets)
        {
            string text = Text;
            // Higher indices first, so __LuminCall_1 never rewrites the prefix of __LuminCall_10.
            for (int index = targets.Count - 1; index >= 0; index--)
                text = text.Replace(WrapperModel.Placeholder(index), targets[index]);
            return text;
        }
    }

    /// <summary>Closes a source body while keeping the original overload and argument binding.</summary>
    private sealed class Body : CSharpSyntaxRewriter
    {
        private readonly Session _owner;
        private readonly Wrapper _wrapper;
        private readonly SemanticModel _model;
        private readonly Dictionary<ITypeParameterSymbol, ITypeSymbol> _arguments;
        private readonly int _depth;
        internal bool Valid = true;

        internal Body(Session owner, Wrapper wrapper, SemanticModel model, Dictionary<ITypeParameterSymbol, ITypeSymbol> arguments, int depth)
        {
            _owner = owner;
            _wrapper = wrapper;
            _model = model;
            _arguments = arguments;
            _depth = depth;
        }

        /// <summary>Keeps the selected methods: facade calls become placeholders, specialized source calls their wrappers.</summary>
        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is IdentifierNameSyntax { Identifier.ValueText: "nameof" })
            {
                Optional<object?> value = _model.GetConstantValue(node);
                return value.HasValue && value.Value is string name
                    ? SyntaxFactory.ParseExpression(SymbolDisplay.FormatLiteral(name, true)) : Fail(node);
            }
            if (_model.GetOperation(node) is not IInvocationOperation operation || operation.TargetMethod.ReducedFrom != null)
                return Fail(node);
            IMethodSymbol? method = Close(operation.TargetMethod);
            if (method == null || !IsClosed(method) || method.MethodKind != MethodKind.Ordinary ||
                method.ContainingNamespace.ToDisplayString() is "System.Reflection" or "System.Diagnostics")
                return Fail(node);
            if (SerializerCallAnalysis.IsFacade(method) && method.Name != "Register")
            {
                if (method.TypeArguments.Any(argument => !_owner.Accessible(argument)))
                    return Fail(node);
                // The concrete entry takes the facade's parameters in order, so arguments are passed positionally.
                if (Positional(node.ArgumentList, operation.Arguments, method.Parameters) is not { } positional)
                    return Fail(node);
                int index = _wrapper.Calls.Count;
                string fallback = "global::LuminPack.LuminPackSerializer." + Escape(method.Name) + "<" +
                    string.Join(", ", method.TypeArguments.Select(TypeName)) + ">";
                if (_owner.Facade(method) is { } call)
                {
                    _wrapper.Calls.Add(call);
                    _wrapper.Fallbacks.Add(fallback);
                    return node.WithExpression(SyntaxFactory.IdentifierName(WrapperModel.Placeholder(index))).WithArgumentList(positional);
                }
                return node.WithExpression(SyntaxFactory.ParseExpression(fallback)).WithArgumentList(positional);
            }
            ExpressionSyntax target;
            if (_owner.Serializing(method) && (method.IsGenericMethod || method.ContainingType.IsGenericType))
            {
                Wrapper? dependency = _owner.Specialize(method, _depth + 1);
                if (dependency == null)
                    return Fail(node);
                _wrapper.Dependencies.Add(dependency);
                target = SyntaxFactory.ParseExpression("global::LuminPack.LuminPackSerializer." + dependency.Name);
            }
            else
            {
                if (!_owner.Accessible(method))
                    return Fail(node);
                string name = Escape(method.Name) + (method.IsGenericMethod ? "<" + string.Join(", ", method.TypeArguments.Select(TypeName)) + ">" : string.Empty);
                if (method.IsStatic)
                {
                    target = SyntaxFactory.ParseExpression(TypeName(method.ContainingType) + "." + name);
                }
                else if (node.Expression is MemberAccessExpressionSyntax member)
                {
                    if (OpenOperand(member.Expression) || method.ContainingType.IsValueType && !method.IsReadOnly)
                        return Fail(node);
                    target = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
                        Cast(method.ContainingType, (ExpressionSyntax)Visit(member.Expression)!), (SimpleNameSyntax)SyntaxFactory.ParseName(name));
                }
                else
                {
                    return Fail(node);
                }
            }
            return node.WithExpression(target).WithArgumentList(Arguments(node.ArgumentList, operation.Arguments, method.Parameters));
        }

        /// <summary>Keeps constructor overload selection and rejects type-parameter construction.</summary>
        public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            if (_model.GetOperation(node) is not IObjectCreationOperation operation || operation.Constructor == null ||
                _model.GetTypeInfo(node.Type).Type is ITypeParameterSymbol || !_owner.Accessible(operation.Constructor))
                return Fail(node);
            IMethodSymbol? constructor = Close(operation.Constructor);
            if (constructor == null)
                return Fail(node);
            return node.WithType(SyntaxFactory.ParseTypeName(TypeName(Resolve(constructor.ContainingType))))
                .WithArgumentList(Arguments(node.ArgumentList ?? SyntaxFactory.ArgumentList(), operation.Arguments, constructor.Parameters))
                .WithInitializer((InitializerExpressionSyntax?)Visit(node.Initializer));
        }

        /// <summary>Qualifies members without letting specialization change an implicit receiver.</summary>
        public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            ISymbol? symbol = _model.GetSymbolInfo(node).Symbol;
            if (symbol is ITypeSymbol type)
                return SyntaxFactory.ParseExpression(TypeName(Resolve(type))).WithTriviaFrom(node);
            if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol)
            {
                if (!_owner.Accessible(symbol))
                    return Fail(node);
                if (symbol.IsStatic)
                    return SyntaxFactory.ParseExpression(TypeName(Resolve(symbol.ContainingType)) + "." + Escape(symbol.Name)).WithTriviaFrom(node);
            }
            return node.WithExpression((ExpressionSyntax)Visit(node.Expression)!);
        }

        /// <summary>Replaces type parameters and removes dependence on the source file's imports.</summary>
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.IsVar)
                return node;
            ISymbol? symbol = _model.GetSymbolInfo(node).Symbol;
            if (symbol is ITypeSymbol type)
                return SyntaxFactory.ParseTypeName(TypeName(Resolve(type))).WithTriviaFrom(node);
            if (symbol is IFieldSymbol or IPropertySymbol or IEventSymbol)
            {
                // A member named on the left of an object initializer belongs to the created instance, not an implicit receiver.
                if (!symbol.IsStatic && node.Parent is AssignmentExpressionSyntax assignment && assignment.Left == node &&
                    assignment.Parent is InitializerExpressionSyntax { RawKind: (int)SyntaxKind.ObjectInitializerExpression })
                    return _owner.Accessible(symbol) ? node : Fail(node);
                if (!symbol.IsStatic || !_owner.Accessible(symbol))
                    return Fail(node);
                return SyntaxFactory.ParseExpression(TypeName(Resolve(symbol.ContainingType)) + "." + Escape(symbol.Name)).WithTriviaFrom(node);
            }
            return symbol is IMethodSymbol ? Fail(node) : node;
        }

        /// <summary>Closes complete named types rather than individual syntax fragments.</summary>
        public override SyntaxNode? VisitGenericName(GenericNameSyntax node)
            => _model.GetSymbolInfo(node).Symbol is ITypeSymbol type
                ? SyntaxFactory.ParseTypeName(TypeName(Resolve(type))).WithTriviaFrom(node) : Fail(node);

        /// <summary>Closes an entire qualified type name in one substitution.</summary>
        public override SyntaxNode? VisitQualifiedName(QualifiedNameSyntax node)
            => _model.GetSymbolInfo(node).Symbol is ITypeSymbol type
                ? SyntaxFactory.ParseTypeName(TypeName(Resolve(type))).WithTriviaFrom(node) : base.VisitQualifiedName(node);

        /// <summary>Rejects operators whose binding could change when an open operand becomes concrete.</summary>
        public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
            => OpenOperand(node.Left) || OpenOperand(node.Right) ? Fail(node) : base.VisitBinaryExpression(node);

        /// <summary>Keeps open unary operators on their generic path.</summary>
        public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
            => OpenOperand(node.Operand) ? Fail(node) : base.VisitPrefixUnaryExpression(node);

        /// <summary>Keeps open increment operators on their generic path.</summary>
        public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
            => OpenOperand(node.Operand) ? Fail(node) : base.VisitPostfixUnaryExpression(node);

        /// <summary>Rejects compound assignment that could bind a different closed operator.</summary>
        public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
            => !node.IsKind(SyntaxKind.SimpleAssignmentExpression) && (OpenOperand(node.Left) || OpenOperand(node.Right))
                ? Fail(node) : base.VisitAssignmentExpression(node);

        /// <summary>Keeps generic enumeration whose closed type could select another enumerator.</summary>
        public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
            => OpenOperand(node.Expression) ? Fail(node) : base.VisitForEachStatement(node);

        private bool OpenOperand(ExpressionSyntax node)
        {
            ITypeSymbol? type = _model.GetTypeInfo(node).Type;
            return type != null && (type.TypeKind == TypeKind.Dynamic || GenericPayloadExpansion.ContainsTypeParameter(type));
        }

        /// <summary>Marks an unsupported relocation as a boundary without altering the original source.</summary>
        private SyntaxNode Fail(SyntaxNode node)
        {
            Valid = false;
            return node;
        }

        private ITypeSymbol Resolve(ITypeSymbol type) => _owner.Resolve(type, _arguments);

        /// <summary>Reconstructs a selected method in its substituted declaring type.</summary>
        private IMethodSymbol? Close(IMethodSymbol method)
        {
            var containing = Resolve(method.ContainingType) as INamedTypeSymbol;
            IMethodSymbol? definition = containing?.GetMembers(method.Name).OfType<IMethodSymbol>()
                .FirstOrDefault(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, method.OriginalDefinition));
            return definition == null ? null : method.IsGenericMethod
                ? definition.Construct(method.TypeArguments.Select(Resolve).ToArray()) : definition;
        }

        /// <summary>Keeps source evaluation order and the original parameter conversions.</summary>
        private ArgumentListSyntax Arguments(ArgumentListSyntax syntax, ImmutableArray<IArgumentOperation> operations,
            ImmutableArray<IParameterSymbol> parameters)
        {
            var result = new List<ArgumentSyntax>();
            foreach (ArgumentSyntax argument in syntax.Arguments)
            {
                IArgumentOperation? operation = operations.FirstOrDefault(item => item.Syntax == argument);
                if (operation?.Parameter == null || operation.ArgumentKind == ArgumentKind.ParamArray)
                {
                    Valid = false;
                    return syntax;
                }
                IParameterSymbol parameter = parameters[operation.Parameter.Ordinal];
                var expression = (ExpressionSyntax)Visit(argument.Expression)!;
                if (parameter.RefKind == RefKind.None)
                    expression = Cast(parameter.Type, expression);
                result.Add(argument.WithExpression(expression));
            }
            foreach (IArgumentOperation operation in operations.Where(item => item.IsImplicit))
            {
                if (Default(operation, parameters) is not { } argument)
                {
                    Valid = false;
                    return syntax;
                }
                result.Add(argument.WithNameColon(SyntaxFactory.NameColon(Escape(operation.Parameter!.Name))));
            }
            return syntax.WithArguments(SyntaxFactory.SeparatedList(result));
        }

        /// <summary>Spells the arguments of a facade call in parameter order, without names, as the entry shim declares them.</summary>
        /// <returns>The argument list, or null when the source order differs from the parameter order.</returns>
        private ArgumentListSyntax? Positional(ArgumentListSyntax syntax, ImmutableArray<IArgumentOperation> operations,
            ImmutableArray<IParameterSymbol> parameters)
        {
            var result = new ArgumentSyntax?[parameters.Length];
            int previous = -1;
            foreach (ArgumentSyntax argument in syntax.Arguments)
            {
                IArgumentOperation? operation = operations.FirstOrDefault(item => item.Syntax == argument);
                if (operation?.Parameter == null || operation.ArgumentKind == ArgumentKind.ParamArray || operation.Parameter.Ordinal < previous)
                    return null;
                previous = operation.Parameter.Ordinal;
                IParameterSymbol parameter = parameters[previous];
                var expression = (ExpressionSyntax)Visit(argument.Expression)!;
                if (parameter.RefKind == RefKind.None)
                    expression = Cast(parameter.Type, expression);
                result[previous] = argument.WithNameColon(null).WithExpression(expression);
            }
            foreach (IArgumentOperation operation in operations.Where(item => item.IsImplicit))
            {
                if (Default(operation, parameters) is not { } argument)
                    return null;
                result[operation.Parameter!.Ordinal] = argument;
            }
            if (result.Any(argument => argument == null))
                return null;
            return syntax.WithArguments(SyntaxFactory.SeparatedList(result!));
        }

        /// <summary>Freezes an omitted optional argument to its constant.</summary>
        private static ArgumentSyntax? Default(IArgumentOperation operation, ImmutableArray<IParameterSymbol> parameters)
        {
            if (operation.ArgumentKind != ArgumentKind.DefaultValue || operation.Parameter == null || !operation.Value.ConstantValue.HasValue)
                return null;
            object? constant = operation.Value.ConstantValue.Value;
            ITypeSymbol type = parameters[operation.Parameter.Ordinal].Type;
            string literal = constant == null ? "default(" + TypeName(type) + ")"
                : SymbolDisplay.FormatPrimitive(constant, quoteStrings: true, useHexadecimalNumbers: false);
            return SyntaxFactory.Argument(Cast(type, SyntaxFactory.ParseExpression(literal)));
        }

        /// <summary>Freezes the originally selected implicit conversion before overload resolution.</summary>
        private static ExpressionSyntax Cast(ITypeSymbol type, ExpressionSyntax expression)
            => SyntaxFactory.ParenthesizedExpression(SyntaxFactory.CastExpression(SyntaxFactory.ParseTypeName(TypeName(type)),
                SyntaxFactory.ParenthesizedExpression(expression)));
    }

    /// <summary>Whether no open type parameter remains in a method construction.</summary>
    private static bool IsClosed(IMethodSymbol method) => !GenericPayloadExpansion.ContainsTypeParameter(method.ContainingType) &&
        !method.TypeArguments.Any(GenericPayloadExpansion.ContainsTypeParameter);

    /// <summary>Maps method and containing-type parameters as one correlated construction.</summary>
    private static Dictionary<ITypeParameterSymbol, ITypeSymbol> Arguments(IMethodSymbol method)
    {
        var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        for (int index = 0; index < method.TypeArguments.Length; index++)
            map[method.OriginalDefinition.TypeParameters[index]] = method.TypeArguments[index];
        for (INamedTypeSymbol? owner = method.ContainingType; owner != null; owner = owner.ContainingType)
            for (int index = 0; index < owner.TypeArguments.Length; index++)
                map[owner.OriginalDefinition.TypeParameters[index]] = owner.TypeArguments[index];
        return map;
    }

    /// <summary>The value-only identity of a method construction.</summary>
    private static string Signature(IMethodSymbol method)
        => method.Name + "|" + TypeName(method.ReturnType) + "|" + string.Join("|", method.TypeArguments.Select(TypeName)) + "|" +
            string.Join("|", method.Parameters.Select(parameter => parameter.RefKind + ":" + TypeName(parameter.Type)));

    /// <summary>Spells a parameter with its passing convention.</summary>
    private static string Parameter(IParameterSymbol parameter)
    {
        bool scoped = parameter.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is ParameterSyntax syntax &&
            syntax.Modifiers.Any(token => token.ValueText == "scoped");
        string passing = parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            _ => string.Empty,
        };
        return (scoped ? "scoped " : string.Empty) + passing + TypeName(parameter.Type) + " " + Escape(parameter.Name);
    }

    private static string TypeName(ITypeSymbol type) => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;
}
