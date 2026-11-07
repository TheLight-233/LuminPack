using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

internal sealed partial class StaticSerializerBinding
{
    private readonly Dictionary<string, Wrapper> _wrappers = new(StringComparer.Ordinal);
    private readonly List<WrapperCall> _wrapperCalls = new();
    private readonly HashSet<IMethodSymbol> _serializationMethods = new(SymbolEqualityComparer.Default);

    /// <summary>Propagates serialization requirements through an indexed source call graph.</summary>
    /// <param name="calls">The already bound source invocations.</param>
    private void AnalyzeWrappers(IReadOnlyList<SourceCall> calls)
    {
        var callers = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
        var pending = new Queue<IMethodSymbol>();
        foreach (var call in calls)
        {
            var owner = call.Model.GetEnclosingSymbol(call.Syntax.SpanStart) as IMethodSymbol;
            if (owner == null)
                continue;
            owner = owner.OriginalDefinition;
            var target = call.Method.OriginalDefinition;
            if (!callers.TryGetValue(target, out var incoming))
                callers[target] = incoming = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            incoming.Add(owner);
            if (IsSerializer(call.Method) && call.Method.Name != "Register" && _serializationMethods.Add(owner))
                pending.Enqueue(owner);
        }
        while (pending.Count != 0)
        {
            if (!callers.TryGetValue(pending.Dequeue(), out var incoming))
                continue;
            foreach (var owner in incoming)
                if (_serializationMethods.Add(owner))
                    pending.Enqueue(owner);
        }
        foreach (var call in calls)
        {
            if ((!call.Method.IsGenericMethod && !call.Method.ContainingType.IsGenericType) ||
                !_serializationMethods.Contains(call.Method.OriginalDefinition) || !IsClosed(call.Method))
                continue;
            string attribute = InterceptionSupport.Attribute(call.Model, call.Syntax);
            if (attribute == null)
                continue;
            var wrapper = Specialize(call.Method, 0);
            if (wrapper != null)
                _wrapperCalls.Add(new WrapperCall(wrapper, attribute));
        }
        ValidateWrapperBodies();
        RemoveInvalidWrappers();
    }

    /// <summary>Propagates opaque boundaries through the specialized dependency graph.</summary>
    private void RemoveInvalidWrappers()
    {
        bool changed;
        do
        {
            changed = false;
            foreach (var wrapper in _wrappers.Values)
                if (wrapper.Valid && wrapper.Dependencies.Any(dependency => !dependency.Valid))
                {
                    wrapper.Valid = false;
                    changed = true;
                }
        }
        while (changed);
        _wrapperCalls.RemoveAll(call => !call.Wrapper.Valid);
    }

    /// <summary>Creates one concrete body for a safely relocatable source wrapper construction.</summary>
    /// <param name="method">The fully constructed source method.</param>
    /// <param name="depth">The current specialization depth, bounding expanding generic recursion.</param>
    /// <returns>The shared concrete wrapper, or null for an opaque boundary.</returns>
    private Wrapper Specialize(IMethodSymbol method, int depth)
    {
        string key = TypeName(method.ContainingType) + "|" + Signature(method);
        if (_wrappers.TryGetValue(key, out var previous))
            return previous.Valid ? previous : null;
        if (depth >= 64 || !method.IsStatic || method.IsVirtual || method.IsAbstract || method.IsExtern ||
            method.ContainingType.StaticConstructors.Any(constructor => !constructor.IsImplicitlyDeclared) ||
            method.ContainingType.GetMembers().Any(member => member.IsStatic && member.DeclaringSyntaxReferences.Any(reference =>
                reference.GetSyntax() is VariableDeclaratorSyntax { Initializer: not null } or PropertyDeclarationSyntax { Initializer: not null })) ||
            method.ReturnsByRef || method.ReturnsByRefReadonly || !IsClosed(method) ||
            method.MethodKind != MethodKind.Ordinary ||
            method.OriginalDefinition.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax() is not MethodDeclarationSyntax syntax ||
            syntax.AttributeLists.Count != 0 || syntax.Body == null && syntax.ExpressionBody == null ||
            syntax.DescendantNodes().Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax or
                AwaitExpressionSyntax or YieldStatementSyntax or BaseExpressionSyntax or ThisExpressionSyntax or QueryExpressionSyntax))
            return null;
        if (!_binding.IsSymbolAccessibleWithin(method.ReturnType, _binding.Assembly) ||
            method.TypeArguments.Any(argument => !_binding.IsSymbolAccessibleWithin(argument, _binding.Assembly)) ||
            method.Parameters.Any(parameter => !_binding.IsSymbolAccessibleWithin(parameter.Type, _binding.Assembly)))
            return null;
        var wrapper = new Wrapper(key, method);
        _wrappers.Add(key, wrapper);
        var model = _binding.GetSemanticModel(syntax.SyntaxTree);
        var rewriter = new WrapperBody(this, wrapper, model, TypeArguments(method), depth);
        if (syntax.Body != null)
            wrapper.Body = rewriter.Visit(syntax.Body).NormalizeWhitespace().ToFullString();
        else
        {
            var expression = (ExpressionSyntax)rewriter.Visit(syntax.ExpressionBody.Expression);
            wrapper.Body = SyntaxFactory.Block(method.ReturnsVoid
                ? (StatementSyntax)SyntaxFactory.ExpressionStatement(expression)
                : SyntaxFactory.ReturnStatement(expression)).NormalizeWhitespace().ToFullString();
        }
        wrapper.Valid = rewriter.Valid;
        return wrapper.Valid ? wrapper : null;
    }

    /// <summary>Emits valid wrapper bodies with attributes only at their concrete source entry calls.</summary>
    /// <param name="text">The common interception source builder.</param>
    /// <param name="locationsEnabled">Whether to include source interception attributes.</param>
    private void EmitWrappers(StringBuilder text, bool locationsEnabled = true)
    {
        var locations = _wrapperCalls.GroupBy(call => call.Wrapper.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(call => call.Attribute).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        foreach (var wrapper in _wrappers.Values.Where(value => value.Valid))
        {
            if (locationsEnabled && InterceptionSupport.UsesIlRewriting((CSharpParseOptions)_input.SyntaxTrees.First().Options) &&
                locations.ContainsKey(wrapper.Key))
                AppendIlMapping(text, wrapper.Method, -1);
            if (locationsEnabled && locations.TryGetValue(wrapper.Key, out var attributes))
                foreach (string attribute in attributes)
                    text.AppendLine(attribute);
            bool unsafeBody = wrapper.Method.DeclaringSyntaxReferences.First().GetSyntax() is MethodDeclarationSyntax declaration &&
                declaration.Modifiers.Any(token => token.IsKind(SyntaxKind.UnsafeKeyword));
            text.Append(unsafeBody ? "internal static unsafe " : "internal static ").Append(TypeName(wrapper.Method.ReturnType)).Append(' ').Append(wrapper.Name).Append('(')
                .Append(string.Join(", ", wrapper.Method.Parameters.Select(Parameter))).AppendLine(")");
            text.AppendLine(wrapper.Body);
        }
    }

    /// <summary>Rejects relocations that cannot bind in the actual generated scope.</summary>
    private void ValidateWrapperBodies()
    {
        if (!_wrappers.Values.Any(wrapper => wrapper.Valid))
            return;
        var text = new StringBuilder("namespace LuminPack { public static partial class LuminPackSerializer {\n");
        foreach (var operation in _operations)
        {
            var method = operation.Value;
            text.Append("internal static ").Append(TypeName(method.ReturnType)).Append(" Intercept_")
                .Append(RootContainerProjection.Hash(operation.Key)).Append(GenericDeclaration(method)).Append('(')
                .Append(string.Join(", ", method.Parameters.Select(Parameter))).Append(')').Append(GenericConstraints(method))
                .AppendLine(" { throw null!; }");
        }
        EmitWrappers(text, locationsEnabled: false);
        text.AppendLine("} }");
        var tree = CSharpSyntaxTree.ParseText(text.ToString(), (CSharpParseOptions)_input.SyntaxTrees.First().Options);
        var validation = _binding.AddSyntaxTrees(tree);
        var byName = _wrappers.Values.ToDictionary(wrapper => wrapper.Name, StringComparer.Ordinal);
        foreach (var error in validation.GetSemanticModel(tree).GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            var method = tree.GetRoot().FindNode(error.Location.SourceSpan).AncestorsAndSelf().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (method != null && byName.TryGetValue(method.Identifier.ValueText, out var wrapper))
                wrapper.Valid = false;
        }
    }

    /// <summary>Checks all method and enclosing type arguments before producing a concrete entry.</summary>
    /// <param name="method">A potentially constructed source method.</param>
    /// <returns>Whether no open type parameter remains.</returns>
    private static bool IsClosed(IMethodSymbol method)
        => !LuminPackExtensionGenerator.ContainsTypeParameter(method.ContainingType) &&
            !method.TypeArguments.Any(LuminPackExtensionGenerator.ContainsTypeParameter);

    /// <summary>Maps method and enclosing-type parameters as one correlated construction.</summary>
    /// <param name="method">The closed method.</param>
    /// <returns>The complete type substitution environment.</returns>
    private static Dictionary<ITypeParameterSymbol, ITypeSymbol> TypeArguments(IMethodSymbol method)
    {
        var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        for (int index = 0; index < method.TypeArguments.Length; index++)
            map[method.OriginalDefinition.TypeParameters[index]] = method.TypeArguments[index];
        for (var owner = method.ContainingType; owner != null; owner = owner.ContainingType)
            for (int index = 0; index < owner.TypeArguments.Length; index++)
                map[owner.OriginalDefinition.TypeParameters[index]] = owner.TypeArguments[index];
        return map;
    }

    /// <summary>One original invocation whose semantics were resolved against the fixed facade.</summary>
    private sealed class SourceCall
    {
        internal readonly InvocationExpressionSyntax Syntax;
        internal readonly IMethodSymbol Method;
        internal readonly SemanticModel Model;

        /// <summary>Captures a source call for compilation-local graph analysis.</summary>
        /// <param name="syntax">The invocation syntax.</param>
        /// <param name="method">The selected method.</param>
        /// <param name="model">Its original binding context.</param>
        internal SourceCall(InvocationExpressionSyntax syntax, IMethodSymbol method, SemanticModel model)
        {
            Syntax = syntax;
            Method = method;
            Model = model;
        }
    }

    /// <summary>One concrete wrapper body shared by all matching source calls.</summary>
    private sealed class Wrapper
    {
        internal readonly string Key;
        internal readonly IMethodSymbol Method;
        internal readonly List<Wrapper> Dependencies = new();
        internal readonly HashSet<string> Operations = new(StringComparer.Ordinal);
        internal string Body;
        internal bool Valid = true;

        /// <summary>Gets the unique generated wrapper method name.</summary>
        internal string Name => "Wrapper_" + RootContainerProjection.Hash(Key);

        /// <summary>Starts a specialization before visiting recursive calls.</summary>
        /// <param name="key">The full method construction identity.</param>
        /// <param name="method">The closed source method.</param>
        internal Wrapper(string key, IMethodSymbol method)
        {
            Key = key;
            Method = method;
        }
    }

    /// <summary>A source location that selects a concrete wrapper.</summary>
    private sealed class WrapperCall
    {
        internal readonly Wrapper Wrapper;
        internal readonly string Attribute;

        /// <summary>Associates a source location with its concrete body.</summary>
        /// <param name="wrapper">The shared specialized body.</param>
        /// <param name="attribute">The compiler's location attribute.</param>
        internal WrapperCall(Wrapper wrapper, string attribute)
        {
            Wrapper = wrapper;
            Attribute = attribute;
        }
    }
}
