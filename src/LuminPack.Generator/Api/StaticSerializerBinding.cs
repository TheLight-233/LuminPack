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

/// <summary>Binds the fixed generic facade and records concrete interception targets within one compilation.</summary>
internal sealed partial class StaticSerializerBinding
{
    private static readonly ConditionalWeakTable<Compilation, Lazy<StaticSerializerBinding>> Cache = new();
    private readonly Dictionary<string, SerializerApiKind> _apis = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SerializerApiKind> _directApis = new(StringComparer.Ordinal);
    private readonly List<BoundCall> _calls = new();
    private readonly Dictionary<string, IMethodSymbol> _operations = new(StringComparer.Ordinal);
    private readonly List<ITypeSymbol> _operationPayloads = new();
    private readonly Compilation _input;
    private Compilation _binding;
    private readonly SyntaxTree _facadeSignatures;

    /// <summary>Gets closed payloads rebound to the original compilation.</summary>
    internal IReadOnlyList<ITypeSymbol> Roots { get; }

    /// <summary>Gets the exact call-location and target projection used by incremental guards.</summary>
    internal string Key { get; }

    /// <summary>Gets whether the input contains any serializer usage.</summary>
    internal bool HasUsage { get; }

    /// <summary>Gets the binding for one original compilation.</summary>
    /// <param name="compilation">The consumer before generated output is added.</param>
    /// <returns>A compilation-local binding graph.</returns>
    internal static StaticSerializerBinding For(Compilation compilation)
        => Cache.GetValue(compilation, current => new Lazy<StaticSerializerBinding>(() => new StaticSerializerBinding(current),
            System.Threading.LazyThreadSafetyMode.ExecutionAndPublication)).Value;

    /// <summary>Gets the convenience operations needed for one payload.</summary>
    /// <param name="type">The concrete payload.</param>
    /// <returns>The selected concrete operation families.</returns>
    internal SerializerApiKind GetApis(ITypeSymbol type)
        => _apis.TryGetValue(FormatterTypeName.Get(type), out var apis) ? apis : SerializerApiKind.None;

    /// <summary>Identifies the concrete overload families called directly in user source.</summary>
    /// <param name="payload">The canonical closed payload name.</param>
    /// <returns>The families that receive payload-specific documentation.</returns>
    internal SerializerApiKind GetDirectApis(string payload)
        => _directApis.TryGetValue(payload, out var apis) ? apis : SerializerApiKind.None;

    /// <summary>Names an isolated concrete convenience container.</summary>
    /// <param name="payload">The canonical concrete payload spelling.</param>
    /// <returns>A stable identifier independent of call locations.</returns>
    internal static string EntryName(string payload)
        => "LuminEntry_" + RootContainerProjection.Hash(payload.Replace("global::", string.Empty).Replace(" ", string.Empty));

    /// <summary>Analyzes calls against a constant-size signature tree.</summary>
    /// <param name="compilation">The original consumer compilation.</param>
    private StaticSerializerBinding(Compilation compilation)
    {
        _input = compilation;
        var options = (CSharpParseOptions)compilation.SyntaxTrees.First().Options;
        var metadata = new MetaInfo(options, options.LanguageVersion, options.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER"),
            options.PreprocessorSymbolNames.Contains("NET9_0_OR_GREATER"),
            compilation.Options is CSharpCompilationOptions { AllowUnsafe: true }, LuminPackGenerationTierResolver.RegisterModeFromAssembly(compilation));
        var signatures = LuminPackSerializerGenerator.GetInterceptionSignatures(metadata);
        _facadeSignatures = signatures;
        _binding = compilation.AddSyntaxTrees(signatures);
        var payloads = new List<ITypeSymbol>();
        var invocations = new List<IMethodSymbol>();
        var sourceCalls = new List<SourceCall>();
        var relevantNames = RelevantInvocationNames(compilation);
        foreach (var tree in compilation.SyntaxTrees)
        {
            SemanticModel model = null;
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!relevantNames.Contains(InvocationName(call)))
                    continue;
                model ??= _binding.GetSemanticModel(tree);
                if (call.Expression is MemberAccessExpressionSyntax stateCall && stateCall.Name.Identifier.ValueText is "Write" or "Read" &&
                    call.ArgumentList.Arguments.Count == 1 && model.GetTypeInfo(stateCall.Expression).Type?.ToDisplayString() is
                        "LuminPack.Core.LuminPackWriter" or "LuminPack.Core.LuminPackReader" or "LuminPack.Core.LuminPackJsonWriter" or "LuminPack.Core.LuminPackJsonReader")
                {
                    var statePayload = model.GetTypeInfo(call.ArgumentList.Arguments[0].Expression).Type;
                    if (statePayload != null)
                        payloads.Add(statePayload);
                }
                if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)
                    continue;
                invocations.Add(method);
                sourceCalls.Add(new SourceCall(call, method, model));
                if (!IsSerializer(method) || method.Name == "Register" || method.TypeArguments.Length == 0)
                    continue;
                HasUsage = true;
                ITypeSymbol payload = Payload(method);
                payloads.Add(payload);
                if (LuminPackExtensionGenerator.ContainsTypeParameter(payload) || payload.TypeKind == TypeKind.Error)
                    continue;
                if (!_binding.IsSymbolAccessibleWithin(payload, _binding.Assembly))
                    continue;
                if (method.TypeArguments.Length > 1)
                    method = method.OriginalDefinition.Construct(payload, method.OriginalDefinition.TypeParameters.Last());
                string attribute = InterceptionSupport.Attribute(model, call);
                if (attribute == null)
                    continue;
                var bound = new BoundCall(call, method, payload, attribute);
                _calls.Add(bound);
                string name = FormatterTypeName.Get(payload);
                _directApis.TryGetValue(name, out var directApis);
                _directApis[name] = directApis | Family(method);
                RequireOperation(method);
            }
        }
        AnalyzeWrappers(sourceCalls);
        var identities = new StableTypeIdentityFactory(_binding);
        var resolver = new CurrentTypeResolver(compilation);
        var roots = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var payload in ReachabilityAnalysisCache.ExpandApiPayloads(_binding, payloads.Concat(_operationPayloads), invocations))
        {
            var identity = identities.Create(payload);
            var root = identity == null ? null : resolver.Resolve(identity);
            if (root != null && !LuminPackExtensionGenerator.ContainsTypeParameter(root))
                roots.Add(root);
        }
        Roots = roots.ToArray();
        var key = new StringBuilder("interceptors|");
        foreach (var call in _calls)
            key.Append(call.Attribute).Append('|').Append(call.Method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('|');
        foreach (var call in _wrapperCalls)
            key.Append(call.Attribute).Append('|').Append(call.Wrapper.Key).Append('|');
        foreach (var wrapper in _wrappers.Values.Where(wrapper => wrapper.Valid).OrderBy(wrapper => wrapper.Key, StringComparer.Ordinal))
            key.Append(wrapper.Key).Append('|').Append(wrapper.Body.ToFullString()).Append('|');
        Key = key.ToString();
    }

    /// <summary>Filters unrelated invocations before requesting compiler overload binding.</summary>
    /// <param name="compilation">The original source-only consumer.</param>
    /// <returns>A conservative name closure of serializer calls and their source wrappers.</returns>
    private static HashSet<string> RelevantInvocationNames(Compilation compilation)
    {
        var names = new HashSet<string>(StringComparer.Ordinal)
        { "Serialize", "Deserialize", "SerializeJson", "DeserializeJson", "SerializeAsync", "DeserializeAsync", "Register", "Write", "Read" };
        var callers = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
            foreach (var call in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                var owner = call.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
                string target = InvocationName(call);
                if (owner == null || target == null)
                    continue;
                if (!callers.TryGetValue(target, out var incoming))
                    callers[target] = incoming = new HashSet<string>(StringComparer.Ordinal);
                incoming.Add(owner.Identifier.ValueText);
            }
        var pending = new Queue<string>(names);
        while (pending.Count != 0)
            if (callers.TryGetValue(pending.Dequeue(), out var incoming))
                foreach (string owner in incoming)
                    if (names.Add(owner))
                        pending.Enqueue(owner);
        return names;
    }

    /// <summary>Reads the syntactic operation name without binding the invocation.</summary>
    /// <param name="call">The original invocation.</param>
    /// <returns>The ordinary method name, or null for a dynamic expression.</returns>
    private static string InvocationName(InvocationExpressionSyntax call) => call.Expression switch
    {
        MemberAccessExpressionSyntax access => access.Name.Identifier.ValueText,
        MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
        SimpleNameSyntax simple => simple.Identifier.ValueText,
        _ => null,
    };

    /// <summary>Retains one shared concrete operation used directly or by a specialized source wrapper.</summary>
    /// <param name="method">The closed facade operation.</param>
    /// <returns>The generated operation helper name.</returns>
    private string RequireOperation(IMethodSymbol method)
    {
        string key = Signature(method);
        _operations[key] = method;
        var payload = Payload(method);
        _operationPayloads.Add(payload);
        string name = FormatterTypeName.Get(payload);
        _apis.TryGetValue(name, out var selected);
        _apis[name] = selected | Family(method);
        return "Intercept_" + RootContainerProjection.Hash(key);
    }

    /// <summary>Recognizes only the generated facade's ordinary methods.</summary>
    /// <param name="method">The compiler-selected method.</param>
    /// <returns>Whether the call belongs to the serializer facade.</returns>
    private bool IsSerializer(IMethodSymbol method)
        => method.ContainingType.ToDisplayString() == "LuminPack.LuminPackSerializer" &&
            (method.DeclaringSyntaxReferences.Length == 0 || method.DeclaringSyntaxReferences.Any(reference =>
                reference.SyntaxTree == _facadeSignatures));

    /// <summary>Gets the payload type from a generic serializer operation.</summary>
    /// <param name="method">The bound facade operation.</param>
    /// <returns>The selected payload type.</returns>
    private static ITypeSymbol Payload(IMethodSymbol method) => method.TypeArguments[0];

    /// <summary>Maps an operation to its concrete convenience family.</summary>
    /// <param name="method">The selected facade operation and its exact parameter shape.</param>
    /// <returns>The corresponding concrete entries.</returns>
    private static SerializerApiKind Family(IMethodSymbol method)
    {
        var first = method.Parameters.FirstOrDefault()?.Type;
        if (first?.ContainingNamespace?.ToDisplayString() == "LuminPack.Core")
            return SerializerApiKind.None;
        bool buffer = method.Parameters.Any(parameter => parameter.Type.Name == "LuminBufferWriter");
        bool output = method.Parameters.Any(parameter => parameter.Type.Name == "IBufferWriter");
        return method.Name switch
        {
            "Serialize" => buffer ? SerializerApiKind.SerializeBuffer : output ? SerializerApiKind.SerializeBufferWriter : SerializerApiKind.SerializeArray,
            "SerializeJson" => buffer ? SerializerApiKind.SerializeJsonBuffer : output ? SerializerApiKind.SerializeJsonBufferWriter : SerializerApiKind.SerializeJsonString,
            "Deserialize" => buffer ? SerializerApiKind.DeserializeBufferRef : first?.Name == "ReadOnlySequence"
                ? SerializerApiKind.DeserializeSequenceRef : SerializerApiKind.DeserializeSpanRef,
            "DeserializeJson" => buffer ? SerializerApiKind.DeserializeJsonBufferRef : first?.SpecialType == SpecialType.System_String
                ? SerializerApiKind.DeserializeJsonStringRef : first is INamedTypeSymbol span && span.TypeArguments.FirstOrDefault()?.SpecialType == SpecialType.System_Char
                    ? SerializerApiKind.DeserializeJsonSpanCharRef : SerializerApiKind.DeserializeJsonSpanByteRef,
            "SerializeAsync" => SerializerApiKind.SerializeAsync,
            "DeserializeAsync" => SerializerApiKind.DeserializeAsyncValue,
            _ => SerializerApiKind.None,
        };
    }

    /// <summary>Retains semantic binding only for the lifetime of its original compilation.</summary>
    private sealed class BoundCall
    {
        internal readonly InvocationExpressionSyntax Syntax;
        internal readonly IMethodSymbol Method;
        internal readonly ITypeSymbol Payload;
        internal readonly string Attribute;

        /// <summary>Captures one eligible call and its compiler-owned location.</summary>
        /// <param name="syntax">The source call.</param>
        /// <param name="method">The selected constructed method.</param>
        /// <param name="payload">Its closed payload.</param>
        /// <param name="attribute">The location attribute text.</param>
        internal BoundCall(InvocationExpressionSyntax syntax, IMethodSymbol method, ITypeSymbol payload, string attribute)
        {
            Syntax = syntax;
            Method = method;
            Payload = payload;
            Attribute = attribute;
        }
    }
}
