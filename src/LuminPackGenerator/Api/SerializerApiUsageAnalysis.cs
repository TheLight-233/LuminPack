using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

#nullable enable

/// <summary>Resolves project calls against the complete facade before generated sources exist.</summary>
internal sealed class SerializerApiUsageAnalysis
{
    private const string SerializerName = "LuminPack.LuminPackSerializer";
    private static readonly ConditionalWeakTable<Compilation, SerializerApiUsageAnalysis> Cache = new();
    private readonly Dictionary<string, SerializerApiKind> _apis = new(StringComparer.Ordinal);
    private bool _conservative;

    /// <summary>Gets formatter roots rebound to the original input compilation.</summary>
    internal IReadOnlyList<ITypeSymbol> Roots { get; }

    /// <summary>Gets whether unresolved serializer expressions require conservative formatter retention.</summary>
    internal bool RequiresConservativeRoots => _conservative;

    /// <summary>Gets the stable overload projection used by aggregate cache guards.</summary>
    internal string Key { get; }

    /// <summary>Gets one compilation's shared call-site analysis.</summary>
    /// <param name="compilation">The current input compilation.</param>
    /// <returns>Selections and roots belonging only to the current compilation.</returns>
    internal static SerializerApiUsageAnalysis GetOrCreate(Compilation compilation)
        => Cache.GetValue(compilation, current => new SerializerApiUsageAnalysis(current));

    /// <summary>Looks up the concrete overloads selected for a formatter type.</summary>
    /// <param name="type">A current formatter type.</param>
    /// <returns>The selected overloads; shared generic APIs are independent of this mask.</returns>
    internal SerializerApiKind GetApis(ITypeSymbol type)
        => _apis.TryGetValue(FormatterTypeName.Get(type), out var apis) ? apis : SerializerApiKind.None;

    /// <summary>Binds source expressions with a signature-only facade and closes generic forwarding roots.</summary>
    /// <param name="compilation">The compilation whose original symbols must be returned.</param>
    private SerializerApiUsageAnalysis(Compilation compilation)
    {
        var options = compilation.SyntaxTrees.FirstOrDefault()?.Options as CSharpParseOptions ?? CSharpParseOptions.Default;
        var metadata = new MetaInfo(options, options.LanguageVersion, options.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER"),
            options.PreprocessorSymbolNames.Contains("NET9_0_OR_GREATER"),
            compilation.Options is CSharpCompilationOptions { AllowUnsafe: true }, LuminPackGenerationTierResolver.RegisterModeFromAssembly(compilation));
        var existing = compilation.GetTypeByMetadataName(SerializerName);
        SyntaxTree? signatures = null;
        var kinds = new Dictionary<int, SerializerApiKind>();
        var bindingCompilation = compilation;
        if (existing == null || !existing.Locations.Any(location => location.IsInSource))
        {
            signatures = LuminPackSerializerGenerator.GetApiSignatures(compilation, metadata, out kinds);
            bindingCompilation = compilation.AddSyntaxTrees(signatures);
        }

        var payloads = new List<ITypeSymbol>();
        var invocations = new List<IMethodSymbol?>();
        var observations = InvocationFileObservationCache.For(compilation);
        var methodIdentities = new StableMethodIdentityFactory(compilation);
        var methods = new CurrentMethodResolver(bindingCompilation);
        var files = new SerializerApiFileCache(compilation, bindingCompilation, signatures!);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var file = files.Read(tree, () => BindFile(tree));
            _conservative |= file.Conservative;
            invocations.AddRange(file.Methods);
            foreach (var method in file.Methods) Observe(method, signatures, kinds, payloads);
        }

        SerializerApiFileMethods BindFile(SyntaxTree tree)
        {
            var bound = new List<IMethodSymbol>();
            bool conservative = false;
            var calls = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
            var previous = observations.Read(tree);
            SemanticModel? model = null;
            for (int index = 0; index < calls.Length; index++)
            {
                var call = calls[index];
                var original = previous[index];
                IMethodSymbol? selected = null;
                if (original != null && original.ContainingType?.ToDisplayString() != SerializerName)
                {
                    var identity = methodIdentities.Create(original);
                    if (identity != null) selected = methods.Resolve(identity);
                }
                if (selected == null)
                {
                    model ??= bindingCompilation.GetSemanticModel(tree);
                    System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.ApiRootInvocationBindings);
                    var info = model.GetSymbolInfo(call);
                    selected = info.Symbol as IMethodSymbol;
                    if (selected == null && IsSerializerExpression(call.Expression, model))
                    {
                        // Another generator can supply missing contextual types after this pass.
                        // Preserve formatter roots, while the complete generic overload surface
                        // handles calls that cannot yet select a concrete contribution.
                        conservative = true;
                    }
                }
                if (selected != null) bound.Add(selected);
            }

            // Method groups are callable roots too. Let contextual delegate conversion select the overload.
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                ExpressionSyntax expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
                if (expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression) continue;
                if (expression.Parent is MemberAccessExpressionSyntax parent && parent.Expression == expression) continue;
                if (!IsApiName(name.Identifier.ValueText) && expression.Parent is not (EqualsValueClauseSyntax or ArgumentSyntax or AssignmentExpressionSyntax or
                    ArrowExpressionClauseSyntax or ReturnStatementSyntax or ParenthesizedExpressionSyntax or PrefixUnaryExpressionSyntax or
                    ConditionalExpressionSyntax or SwitchExpressionArmSyntax or InitializerExpressionSyntax or CastExpressionSyntax)) continue;
                model ??= bindingCompilation.GetSemanticModel(tree);
                var info = model.GetSymbolInfo(expression);
                if (info.Symbol is IMethodSymbol method)
                {
                    bound.Add(method);
                }
                else if (IsSerializerExpression(expression, model) && expression.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault()?.Expression.ToString() != "nameof")
                    conservative = true;
            }
            return new SerializerApiFileMethods(bound.ToArray(), conservative);
        }

        var identities = new StableTypeIdentityFactory(bindingCompilation);
        var resolver = new CurrentTypeResolver(compilation);
        var roots = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var payload in ReachabilityAnalysisCache.ExpandApiPayloads(bindingCompilation, payloads, invocations))
        {
            if (LuminPackExtensionGenerator.ContainsTypeParameter(payload)) continue;
            var identity = identities.Create(payload);
            var root = identity == null ? null : resolver.Resolve(identity);
            if (root != null) roots.Add(root);
            else _conservative = true;
        }
        Roots = roots.ToArray();
        var key = new StringBuilder().Append(_conservative).Append('|');
        foreach (var pair in _apis.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            key.Append(pair.Key.Length).Append(':').Append(pair.Key).Append('=').Append((int)pair.Value).Append('|');
        Key = key.ToString();
    }

    /// <summary>Records a selected facade overload and its formatter payload.</summary>
    /// <param name="method">The method selected by C# overload resolution.</param>
    /// <param name="signatures">The temporary signature tree, when needed.</param>
    /// <param name="kinds">Concrete signature offsets.</param>
    /// <param name="payloads">The current compilation's payload roots.</param>
    private void Observe(IMethodSymbol method, SyntaxTree? signatures, Dictionary<int, SerializerApiKind> kinds, List<ITypeSymbol> payloads)
    {
        if (method.ContainingType?.ToDisplayString() != SerializerName) return;
        var payload = method.Parameters.FirstOrDefault(parameter => parameter.Name == "value")?.Type
            ?? (method.IsGenericMethod && method.TypeArguments.Length != 0 ? method.TypeArguments[0] : null);
        if (payload == null) return;
        payloads.Add(payload);
        if (method.IsGenericMethod) return;
        var declaration = method.DeclaringSyntaxReferences.FirstOrDefault(reference => reference.SyntaxTree == signatures);
        var apis = declaration != null && kinds.TryGetValue(declaration.Span.Start, out var kind) ? kind : Family(method.Name);
        string name = FormatterTypeName.Get(payload);
        _apis.TryGetValue(name, out var previous);
        _apis[name] = previous | apis;
    }

    /// <summary>Recognizes serializer expressions including aliases and imported static methods.</summary>
    /// <param name="expression">An invocation target or method group.</param>
    /// <param name="model">The signature-aware semantic model.</param>
    /// <returns>Whether the expression refers to the serializer facade.</returns>
    private static bool IsSerializerExpression(ExpressionSyntax expression, SemanticModel model)
    {
        var info = model.GetSymbolInfo(expression);
        if (info.Symbol is IMethodSymbol method && method.ContainingType?.ToDisplayString() == SerializerName) return true;
        if (info.CandidateSymbols.OfType<IMethodSymbol>().Any(candidate => candidate.ContainingType?.ToDisplayString() == SerializerName)) return true;
        if (expression is MemberAccessExpressionSyntax access)
        {
            var receiver = model.GetSymbolInfo(access.Expression).Symbol;
            return receiver?.ToDisplayString() == SerializerName || receiver is IAliasSymbol alias && alias.Target.ToDisplayString() == SerializerName;
        }
        return false;
    }

    /// <summary>Identifies serializer method names without interpreting parameter syntax.</summary>
    /// <param name="name">The source identifier.</param>
    /// <returns>Whether it names a serializer API family.</returns>
    private static bool IsApiName(string name) => Family(name) != SerializerApiKind.None || name == "Register";

    /// <summary>Conservatively maps existing source declarations to their complete overload family.</summary>
    /// <param name="name">The bound facade method name.</param>
    /// <returns>All concrete variants for that name.</returns>
    private static SerializerApiKind Family(string name) => name switch
    {
        "Serialize" => SerializerApiKind.SerializeArray | SerializerApiKind.SerializeBuffer,
        "SerializeJson" => SerializerApiKind.SerializeJsonString | SerializerApiKind.SerializeJsonBuffer,
        "SerializeAsync" => SerializerApiKind.SerializeAsync,
        "Deserialize" => SerializerApiKind.DeserializeSpanRef | SerializerApiKind.DeserializeBufferRef | SerializerApiKind.DeserializeSequenceRef,
        "DeserializeJson" => SerializerApiKind.DeserializeJsonStringRef | SerializerApiKind.DeserializeJsonSpanByteRef | SerializerApiKind.DeserializeJsonSpanCharRef | SerializerApiKind.DeserializeJsonBufferRef,
        "DeserializeAsync" => SerializerApiKind.DeserializeAsyncValue,
        "Sizeof" => SerializerApiKind.Sizeof,
        _ => SerializerApiKind.None,
    };
}
