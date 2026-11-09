using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Routes modern state interceptors to their roots and omits the internal forwarders that have no remaining consumers.</summary>
internal sealed class StateForwardingPlan : IEquatable<StateForwardingPlan>
{
    private const string Interceptors = GeneratedFileNames.Prefix + "Interceptors.g.cs";
    private static readonly StateForwardingPlan Empty = new(null, new HashSet<(string, int)>(), null);
    private static readonly ConditionalWeakTable<string, ConditionalWeakTable<CSharpParseOptions, FileReferences>> References = new();
    private readonly CSharpParseOptions _options;
    private readonly HashSet<(string Name, int Arity)> _omitted;
    private readonly string _interceptors;

    /// <summary>Captures publication decisions without retaining the compilation or its semantic models.</summary>
    /// <param name="options">The generated source's parse settings.</param>
    /// <param name="omitted">The unique forwarder signatures that can be omitted.</param>
    /// <param name="interceptors">The rewritten compiler interceptor source, or null when unchanged.</param>
    private StateForwardingPlan(CSharpParseOptions options, HashSet<(string, int)> omitted, string interceptors)
    {
        _options = options;
        _omitted = omitted;
        _interceptors = interceptors;
    }

    /// <summary>Accounts for consumers across all serializer, formatter and Union contributions before they are partitioned.</summary>
    /// <param name="compilation">The consumer's original source and assembly settings.</param>
    /// <param name="options">The active compiler settings.</param>
    /// <param name="types">The completed per-type roots and Union contributions.</param>
    /// <param name="files">The completed assembly-wide contributions from all publishers.</param>
    /// <param name="cancellationToken">Stops the analysis when a newer compilation replaces this one.</param>
    /// <returns>The common publication plan for this compilation.</returns>
    internal static StateForwardingPlan Build(Compilation compilation, CSharpParseOptions options, ImmutableArray<FrozenTypeEmission> types,
        ImmutableArray<FrozenGeneratedSource> files, CancellationToken cancellationToken)
    {
        if (!InterceptionSupport.UsesCompilerInterceptors(options) || !GeneratedOperationUsage.IsClosed(compilation) ||
            compilation.Options.OutputKind is not (OutputKind.ConsoleApplication or OutputKind.WindowsApplication or OutputKind.WindowsRuntimeApplication))
            return Empty;

        var declared = new Dictionary<(string, int), InvocationExpressionSyntax>();
        var referenced = new HashSet<(string, int)>();
        string interceptionSource = null;
        foreach (var file in files)
        {
            if (file.Name == Interceptors)
                interceptionSource = file.Text;
            else
                Inspect(file.Text);
        }
        foreach (var type in types)
        {
            Inspect(type.Extension);
            Inspect(type.Union);
        }
        if (!declared.Values.Any(call => call != null))
            return Empty;

        // A source identifier or literal can name an internal entry outside LuminPack's generated call graph.
        var sourceNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            foreach (var token in tree.GetRoot(cancellationToken).DescendantTokens(descendIntoTrivia: true))
                if (IsStateName(token.ValueText))
                    sourceNames.Add(token.ValueText);
        }

        string interceptors = null;
        if (interceptionSource != null)
        {
            var original = Parse(interceptionSource, options);
            var routes = new Dictionary<InvocationExpressionSyntax, InvocationExpressionSyntax>();
            foreach (var method in SerializerMethods(original))
            {
                if (!method.Identifier.ValueText.StartsWith("Intercept", StringComparison.Ordinal) ||
                    !SyntaxText.Is(method.ReturnType, "void") || method.ParameterList.Parameters.Count != 2 ||
                    method.Body?.Statements is not { Count: 1 } statements ||
                    statements[0] is not ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call } ||
                    call.Expression is not MemberAccessExpressionSyntax access || !SyntaxText.Is(access.Expression, "global::LuminPack.LuminPackSerializer") ||
                    sourceNames.Contains(access.Name.Identifier.ValueText) || !declared.TryGetValue(Key(access.Name), out var target) || target == null)
                    continue;
                if (DirectCall(call, access.Name, target) is { } direct)
                    routes.Add(call, direct);
            }
            var rewritten = original.ReplaceNodes(routes.Keys, (node, _) => routes[node]);
            CollectReferences(rewritten, referenced);
            if (routes.Count != 0)
                interceptors = rewritten.ToFullString();
        }

        var omitted = new HashSet<(string, int)>(declared.Where(pair => pair.Value != null && !referenced.Contains(pair.Key) &&
            !sourceNames.Contains(pair.Key.Item1)).Select(pair => pair.Key));
        return omitted.Count == 0 && interceptors == null ? Empty : new StateForwardingPlan(options, omitted, interceptors);

        void Inspect(string source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!MayContainStateNames(source))
                return;
            var facts = References.GetValue(source, static _ => new()).GetValue(options, parse => InspectGeneratedSource(source, parse));
            foreach (var declaration in facts.Declarations)
            {
                // A same-name, same-arity overload makes a syntax-only decision ambiguous.
                declared[declaration.Key] = declared.ContainsKey(declaration.Key) ? null : declaration.Call;
            }
            referenced.UnionWith(facts.Uses);
        }
    }

    /// <summary>Applies the same decisions to the final format-specific source.</summary>
    /// <param name="file">The selected output file before presentation formatting.</param>
    /// <returns>The original file or its safely shortened source.</returns>
    internal FrozenGeneratedSource Apply(FrozenGeneratedSource file)
    {
        if (file.Name == Interceptors)
            return _interceptors == null ? file : new FrozenGeneratedSource(file.Name, _interceptors, file.Owner, file.Payload);
        if (_omitted.Count == 0 || !MayContainStateNames(file.Text))
            return file;
        var root = Parse(file.Text, _options);
        var removed = SerializerMethods(root).Where(method => _omitted.Contains(Key(method)) && ForwardingCall(method) != null).ToArray();
        return removed.Length == 0 ? file : new FrozenGeneratedSource(file.Name,
            root.RemoveNodes(removed, SyntaxRemoveOptions.KeepUnbalancedDirectives).ToFullString(), file.Owner, file.Payload);
    }

    /// <summary>Finds a method that forwards only the original state and value to its own nested root.</summary>
    /// <param name="method">The completed serializer member.</param>
    /// <returns>The single forwarding expression, or null if the body or signature needs to stay.</returns>
    private static InvocationExpressionSyntax ForwardingCall(MethodDeclarationSyntax method)
    {
        var parameters = method.ParameterList.Parameters;
        if (method.ContainsDirectives || method.ContainsDiagnostics || !method.Modifiers.Any(SyntaxKind.InternalKeyword) ||
            !method.Modifiers.Any(SyntaxKind.StaticKeyword) || !SyntaxText.Is(method.ReturnType, "void") || parameters.Count != 2 ||
            !parameters[0].Modifiers.Any(SyntaxKind.RefKeyword) ||
            (method.TypeParameterList is { } profiles && (profiles.Parameters.Count != 1 ||
                profiles.Parameters[0].Identifier.ValueText != "TLuminProfile" ||
                parameters.Any(parameter => parameter.Type.DescendantTokens().Any(token => token.ValueText == "TLuminProfile")))))
            return null;

        string name = method.Identifier.ValueText;
        string container = RootContainerProjection.ContainerPrefix + name.Substring(name.IndexOf('_') + 1);
        var expression = method.ExpressionBody?.Expression ?? (method.Body?.Statements is { Count: 1 } statements &&
            statements[0] is ExpressionStatementSyntax statement ? statement.Expression : null);
        if (expression is not InvocationExpressionSyntax call || call.Expression is not MemberAccessExpressionSyntax access ||
            !access.Name.Identifier.ValueText.StartsWith("Root", StringComparison.Ordinal) ||
            (access.Expression.ToString() != container && access.Expression.ToString() != "global::LuminPack.LuminPackSerializer." + container) ||
            call.ArgumentList.Arguments is not { Count: 2 } arguments || arguments[0].NameColon != null || arguments[1].NameColon != null ||
            !arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword) ||
            arguments[0].Expression is not IdentifierNameSyntax state || state.Identifier.ValueText != parameters[0].Identifier.ValueText ||
            arguments[1].Expression is not IdentifierNameSyntax value || value.Identifier.ValueText != parameters[1].Identifier.ValueText)
            return null;
        return call;
    }

    /// <summary>Substitutes the root from a proven state forwarder while retaining the interceptor's exact signature.</summary>
    /// <param name="call">The original interceptor invocation.</param>
    /// <param name="name">The original state entry name and profile argument, if present.</param>
    /// <param name="target">The proven forwarding expression.</param>
    /// <returns>A direct call, or null when the profile cannot be moved safely.</returns>
    private static InvocationExpressionSyntax DirectCall(InvocationExpressionSyntax call, SimpleNameSyntax name, InvocationExpressionSyntax target)
    {
        if (call.ArgumentList.Arguments is not { Count: 2 } arguments || !arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
            return null;
        var root = (MemberAccessExpressionSyntax)target.Expression;
        if (name is GenericNameSyntax profile)
        {
            if (root.Name is not GenericNameSyntax { TypeArgumentList.Arguments.Count: 1 } rootProfile ||
                rootProfile.TypeArgumentList.Arguments[0] is not IdentifierNameSyntax { Identifier.ValueText: "TLuminProfile" })
                return null;
            root = root.WithName(rootProfile.WithTypeArgumentList(profile.TypeArgumentList));
        }
        else if (root.Name is GenericNameSyntax defaultProfile && defaultProfile.TypeArgumentList.Arguments.Any(argument =>
            !argument.ToString().StartsWith("global::", StringComparison.Ordinal)))
            return null;

        // The declaration proves when an in parameter becomes a by-value root argument.
        var value = arguments[1].WithRefKindKeyword(target.ArgumentList.Arguments[1].RefKindKeyword);
        return call.WithExpression(root.WithTriviaFrom(call.Expression)).WithArgumentList(call.ArgumentList.WithArguments(arguments.Replace(arguments[1], value)));
    }

    /// <summary>Records the state names used by generated calls, method groups and documentation.</summary>
    /// <param name="root">The active syntax of one completed contribution.</param>
    /// <param name="references">The shared set of state signature references.</param>
    private static void CollectReferences(SyntaxNode root, HashSet<(string, int)> references)
    {
        foreach (var name in root.DescendantNodes(descendIntoTrivia: true).OfType<SimpleNameSyntax>())
            if (IsStateName(name.Identifier.ValueText))
            {
                references.Add(Key(name));
                // An unqualified arity in a method group or nameof can refer to the profiled declaration as well.
                var expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : (ExpressionSyntax)name;
                if (name.Arity == 0 && expression.Parent is not InvocationExpressionSyntax)
                    references.Add((name.Identifier.ValueText, 1));
            }
    }

    /// <summary>Gets methods on the generated facade, excluding nested roots and other partial containers.</summary>
    /// <param name="root">The generated compilation unit.</param>
    /// <returns>Direct members of the serializer's top-level partial declarations.</returns>
    private static IEnumerable<MethodDeclarationSyntax> SerializerMethods(CompilationUnitSyntax root)
        => root.Members.OfType<NamespaceDeclarationSyntax>().Where(ns => SyntaxText.Is(ns.Name, "LuminPack"))
            .SelectMany(ns => ns.Members.OfType<ClassDeclarationSyntax>()).Where(type => type.Identifier.ValueText == "LuminPackSerializer")
            .SelectMany(type => type.Members.OfType<MethodDeclarationSyntax>());

    /// <summary>Skips parsing contributions that cannot contain the generated state names.</summary>
    /// <param name="source">A generated contribution.</param>
    /// <returns>Whether any state name prefix occurs in the text.</returns>
    private static bool MayContainStateNames(string source) => !string.IsNullOrEmpty(source) &&
        (source.IndexOf("Serialize_", StringComparison.Ordinal) >= 0 || source.IndexOf("Deserialize_", StringComparison.Ordinal) >= 0 ||
         source.IndexOf("SerializeJson_", StringComparison.Ordinal) >= 0 || source.IndexOf("DeserializeJson_", StringComparison.Ordinal) >= 0);

    /// <summary>Recognizes the four generated internal state families.</summary>
    /// <param name="name">A source identifier or literal value.</param>
    /// <returns>Whether it could name an internal state forwarder.</returns>
    private static bool IsStateName(string name) => name.StartsWith("Serialize_", StringComparison.Ordinal) ||
        name.StartsWith("Deserialize_", StringComparison.Ordinal) || name.StartsWith("SerializeJson_", StringComparison.Ordinal) ||
        name.StartsWith("DeserializeJson_", StringComparison.Ordinal);

    /// <summary>Gets the case-sensitive state name and generic arity of a declaration.</summary>
    /// <param name="method">The state declaration.</param>
    /// <returns>The signature key, conservatively shared by any same-arity overloads.</returns>
    private static (string, int) Key(MethodDeclarationSyntax method) => (method.Identifier.ValueText, method.TypeParameterList?.Parameters.Count ?? 0);

    /// <summary>Gets the case-sensitive state name and generic arity of a reference.</summary>
    /// <param name="name">The name at the call site or method group.</param>
    /// <returns>The referenced signature key.</returns>
    private static (string, int) Key(SimpleNameSyntax name) => (name.Identifier.ValueText, name.Arity);

    /// <summary>Reads the active syntax with documentation references.</summary>
    /// <param name="source">The exact generated text.</param>
    /// <param name="options">The active preprocessor settings.</param>
    /// <returns>The active compilation unit.</returns>
    private static CompilationUnitSyntax Parse(string source, CSharpParseOptions options)
        => SyntaxFactory.ParseCompilationUnit(source, options: options.WithDocumentationMode(DocumentationMode.Parse));

    /// <summary>Captures reusable forwarding facts without retaining the source file's syntax tree.</summary>
    /// <param name="source">One completed generated contribution.</param>
    /// <param name="options">The active compiler settings.</param>
    /// <returns>The declarations and references used by all publishers.</returns>
    private static FileReferences InspectGeneratedSource(string source, CSharpParseOptions options)
    {
        var root = Parse(source, options);
        var uses = new HashSet<(string, int)>();
        CollectReferences(root, uses);
        var declarations = SerializerMethods(root).Where(method => IsStateName(method.Identifier.ValueText)).Select(method =>
        {
            var call = ForwardingCall(method);
            return (Key(method), call == null ? null : (InvocationExpressionSyntax)SyntaxFactory.ParseExpression(call.ToString(), options: options));
        }).ToArray();
        return new FileReferences(declarations, uses);
    }

    /// <summary>Holds a generated contribution's state names and detached forwarding expressions.</summary>
    private sealed class FileReferences
    {
        internal readonly ((string, int) Key, InvocationExpressionSyntax Call)[] Declarations;
        internal readonly HashSet<(string, int)> Uses;

        /// <summary>Stores the declarations and reference keys from one contribution.</summary>
        /// <param name="declarations">All matching declarations, including ambiguous and non-forwarding overloads.</param>
        /// <param name="uses">The state names referenced by the contribution.</param>
        internal FileReferences(((string, int) Key, InvocationExpressionSyntax Call)[] declarations, HashSet<(string, int)> uses)
        {
            Declarations = declarations;
            Uses = uses;
        }
    }

    /// <summary>Compares the decisions that affect published source.</summary>
    /// <param name="other">The previous publication plan.</param>
    /// <returns>Whether the published output can stay cached.</returns>
    public bool Equals(StateForwardingPlan other) => ReferenceEquals(this, other) || other != null &&
        Equals(_options, other._options) && _interceptors == other._interceptors && _omitted.SetEquals(other._omitted);

    /// <summary>Compares an arbitrary object with the publication plan.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether both values represent the same plan.</returns>
    public override bool Equals(object obj) => Equals(obj as StateForwardingPlan);

    /// <summary>Hashes the plan consistently with value equality.</summary>
    /// <returns>The count of omitted signatures.</returns>
    public override int GetHashCode() => _omitted.Count;
}
