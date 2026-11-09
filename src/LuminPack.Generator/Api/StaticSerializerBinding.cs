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
    private readonly Dictionary<string, string> _interceptNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _usedInterceptNames = new(StringComparer.Ordinal);
    private readonly List<ITypeSymbol> _operationPayloads = new();
    private readonly Compilation _input;
    private Compilation _binding;
    private readonly SyntaxTree _facadeSignatures;

    /// <summary>Gets closed payloads rebound to the original compilation.</summary>
    internal IReadOnlyList<ITypeSymbol> Roots { get; }

    /// <summary>Gets the operation modules and payloads selected by the bound calls.</summary>
    internal GeneratedOperationUsage OperationUsage { get; }

    /// <summary>Gets whether source expressions could not bind to a known serializer signature.</summary>
    internal bool RequiresConservativeRoots { get; }

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
        => "LuminEntry_" + ReadableTypeName.Of(payload);

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
        var operationRoots = new List<(GeneratedOperations Operation, ITypeSymbol Payload)>();
        var wholeModules = GeneratedOperations.None;
        bool trackOperations = GeneratedOperationUsage.IsClosed(compilation);
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
                    call.ArgumentList.Arguments.Count == 1)
                {
                    var operation = GeneratedOperationUsage.OfState(model.GetTypeInfo(stateCall.Expression).Type?.ToDisplayString());
                    if (operation != GeneratedOperations.None)
                    {
                        if (trackOperations)
                            wholeModules |= operation;
                        var statePayload = model.GetTypeInfo(call.ArgumentList.Arguments[0].Expression).Type;
                        if (statePayload != null)
                            payloads.Add(statePayload);
                    }
                }
                if (model.GetSymbolInfo(call).Symbol is not IMethodSymbol method)
                {
                    if (trackOperations && SerializerApiUsageAnalysis.IsSerializerExpression(call.Expression, model))
                        RequiresConservativeRoots = true;
                    continue;
                }
                invocations.Add(method);
                sourceCalls.Add(new SourceCall(call, method, model));
                if (!IsSerializer(method))
                    continue;
                HasUsage = true;
                if (trackOperations)
                    ObserveOperation(method);
                if (method.Name == "Register" || method.TypeArguments.Length == 0)
                    continue;
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
            if (!trackOperations)
                continue;
            foreach (var name in tree.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                if (GeneratedOperationUsage.IsExtensionName(name.Identifier.ValueText))
                    wholeModules = GeneratedOperations.All;
                if (!SerializerApiUsageAnalysis.IsApiName(name.Identifier.ValueText))
                    continue;
                ExpressionSyntax expression = name.Parent is MemberAccessExpressionSyntax access && access.Name == name ? access : name;
                if (expression.Parent is InvocationExpressionSyntax invocation && invocation.Expression == expression)
                    continue;
                model ??= _binding.GetSemanticModel(tree);
                if (model.GetSymbolInfo(expression).Symbol is IMethodSymbol method && IsSerializer(method))
                {
                    HasUsage = true;
                    ObserveOperation(method);
                    if (SerializerApiUsageAnalysis.GetPayload(method) is { } payload)
                        payloads.Add(payload);
                }
                else if (SerializerApiUsageAnalysis.IsSerializerExpression(expression, model))
                {
                    if (expression.Ancestors().OfType<InvocationExpressionSyntax>().FirstOrDefault()?.Expression.ToString() == "nameof")
                    {
                        HasUsage = true;
                        wholeModules |= GeneratedOperationUsage.Of(name.Identifier.ValueText);
                    }
                    else
                        RequiresConservativeRoots = true;
                }
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
        OperationUsage = GeneratedOperationUsage.Build(compilation, _binding, operationRoots, invocations,
            RequiresConservativeRoots ? GeneratedOperations.All : wholeModules);
        var key = new StringBuilder("interceptors|");
        foreach (var call in _calls)
            key.Append(call.Attribute).Append('|').Append(call.Method.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('|');
        foreach (var call in _wrapperCalls)
            key.Append(call.Attribute).Append('|').Append(call.Wrapper.Key).Append('|');
        foreach (var wrapper in _wrappers.Values.Where(wrapper => wrapper.Valid).OrderBy(wrapper => wrapper.Key, StringComparer.Ordinal))
            key.Append(wrapper.Key).Append('|').Append(wrapper.Body.ToFullString()).Append('|');
        Key = key.ToString();

        void ObserveOperation(IMethodSymbol method)
        {
            var operation = GeneratedOperationUsage.Of(method.Name);
            var payload = SerializerApiUsageAnalysis.GetPayload(method);
            if (payload == null || operation == GeneratedOperations.All)
                wholeModules |= operation;
            else
                operationRoots.Add((operation, payload));
        }
    }

    /// <summary>Filters unrelated invocations before requesting compiler overload binding.</summary>
    /// <param name="compilation">The original source-only consumer.</param>
    /// <returns>A conservative name closure of serializer calls and their source wrappers.</returns>
    private static HashSet<string> RelevantInvocationNames(Compilation compilation)
    {
        var names = new HashSet<string>(StringComparer.Ordinal)
        { "Serialize", "Deserialize", "SerializeJson", "DeserializeJson", "SerializeAsync", "DeserializeAsync", "SerializeJsonAsync", "DeserializeJsonAsync", "Register", "Write", "Read" };
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
        return InterceptName(key, method);
    }

    /// <summary>Names a specialized source wrapper after its method and closed arguments, such as <c>WrapperSave_ArmorItem</c>.</summary>
    /// <param name="method">The closed source method.</param>
    /// <returns>A name no other generated helper of this compilation uses; wrappers are created in source order.</returns>
    private string WrapperName(IMethodSymbol method)
    {
        var arguments = method.ContainingType.TypeArguments.Concat(method.TypeArguments)
            .Select(argument => ReadableTypeName.Of(FormatterTypeName.Get(argument))).ToArray();
        string plain = "Wrapper" + method.Name + (arguments.Length == 0 ? string.Empty : "_" + string.Join("And", arguments));
        string name = plain;
        for (int number = 2; !_usedInterceptNames.Add(name); number++)
            name = plain + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return name;
    }

    /// <summary>Names an operation's interception helper once, numbering a name that an equal C# signature already took.</summary>
    /// <param name="key">The operation key.</param>
    /// <param name="method">The closed facade operation.</param>
    /// <returns>The helper name.</returns>
    /// <remarks>Operations are required in source order, so the numbering is deterministic. Keys that C# does not tell apart,
    /// such as tuples that differ only in element names, take the next number.</remarks>
    private string InterceptName(string key, IMethodSymbol method)
    {
        if (_interceptNames.TryGetValue(key, out string name))
            return name;
        string plain = InterceptName(method);
        name = plain;
        for (int number = 2; !_usedInterceptNames.Add(name); number++)
            name = plain + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _interceptNames.Add(key, name);
        return name;
    }

    /// <summary>Names the interception helper of a closed facade operation after the operation and its payload.</summary>
    /// <param name="method">The closed facade operation.</param>
    /// <returns>A name such as <c>InterceptSerialize_ArmorItem</c>, or <c>InterceptSerialize_ArmorItem_Utf8Length</c> with an
    /// explicit profile; operations that share a name differ in their parameters or arity.</returns>
    private static string InterceptName(IMethodSymbol method)
    {
        var name = new System.Text.StringBuilder("Intercept").Append(method.Name).Append('_').Append(ReadableTypeName.Of(FormatterTypeName.Get(Payload(method))));
        // The interception helper is generic over the same arguments, so each closed profile needs its own name.
        foreach (var argument in method.TypeArguments.Skip(1))
        {
            string profile = ReadableTypeName.Of(FormatterTypeName.Get(argument));
            if (profile.StartsWith("LuminPack", StringComparison.Ordinal) && profile.Length > "LuminPack".Length)
                profile = profile.Substring("LuminPack".Length);
            if (profile.EndsWith("Options", StringComparison.Ordinal) && profile.Length > "Options".Length)
                profile = profile.Substring(0, profile.Length - "Options".Length);
            name.Append('_').Append(profile);
        }
        return name.ToString();
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
