using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Pipeline;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Analysis;

/// <summary>Binds facade signatures and gathers compiler or IL interception locations.</summary>
internal static class SerializerCallAnalysis
{
    private static readonly MethodInfo? LocationMethod = FindLocationMethod();
    private static readonly PropertyInfo? LocationVersion = LocationMethod?.ReturnType.GetProperty("Version");
    private static readonly PropertyInfo? LocationData = LocationMethod?.ReturnType.GetProperty("Data");

    private static readonly ConditionalWeakTable<Compilation, BindingFacts> Bindings = new();
    private const int ParallelCandidateThreshold = 128;
    private const int MaximumBindingWorkers = 6;

    /// <summary>Checks the syntax shape cheaply before binding.</summary>
    /// <param name="node">The candidate node.</param>
    /// <returns>Whether the node is an invocation of a facade method name.</returns>
    internal static bool IsCandidate(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;
        SimpleNameSyntax? name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax access => access.Name,
            SimpleNameSyntax simple => simple,
            _ => null,
        };
        return name != null && name.Identifier.ValueText is "Serialize" or "Deserialize" or "SerializeJson" or "DeserializeJson" or
            "SerializeAsync" or "DeserializeAsync";
    }

    /// <summary>Binds independent source files concurrently and retains the original discovery order.</summary>
    /// <param name="compilation">The compilation containing the candidates.</param>
    /// <param name="candidates">The syntax-filtered facade candidates.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The normalized bindings and conservative operation uses.</returns>
    internal static ImmutableArray<CallFact> Analyze(Compilation compilation, ImmutableArray<InvocationExpressionSyntax> candidates,
        CancellationToken cancellationToken)
    {
        GeneratorOptions options = GeneratorOptionsFactory.From(compilation);
        bool intercept = options.Interceptors && (LocationMethod != null || options.IlRewrite);
        if (candidates.IsEmpty || !intercept && options.RegisterMode == RegisterMode.Disabled && OperationUsageAnalysis.Fixed(compilation) == GeneratedOperations.All)
            return ImmutableArray<CallFact>.Empty;
        var facts = Bindings.GetValue(compilation, static item => new BindingFacts(item));
        var trees = new Dictionary<SyntaxTree, List<int>>();
        for (int index = 0; index < candidates.Length; index++)
        {
            SyntaxTree tree = candidates[index].SyntaxTree;
            if (!trees.TryGetValue(tree, out var indexes))
                trees.Add(tree, indexes = new List<int>());
            indexes.Add(index);
        }
        var results = new CallFact[candidates.Length];
        if (!compilation.Options.ConcurrentBuild || trees.Count < 2 || candidates.Length < ParallelCandidateThreshold)
        {
            foreach (var tree in trees)
                BindTree(tree);
        }
        else
        {
            Parallel.ForEach(trees, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, MaximumBindingWorkers),
            }, BindTree);
        }
        var retained = ImmutableArray.CreateBuilder<CallFact>();
        foreach (CallFact result in results)
            if (result.Call != null || result.Whole != GeneratedOperations.None)
                retained.Add(result);
        return retained.ToImmutable();

        void BindTree(KeyValuePair<SyntaxTree, List<int>> tree)
        {
            SemanticModel model = BindingSemanticModel.For(compilation, tree.Key);
            foreach (int index in tree.Value)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results[index] = Bind(model, candidates[index], facts, intercept, options.IlRewrite, cancellationToken);
            }
        }
    }

    /// <summary>Binds the signature and location of one candidate invocation.</summary>
    /// <param name="semanticModel">The binding model of the source file.</param>
    /// <param name="invocation">The candidate facade call.</param>
    /// <param name="facts">The compilation's signature and payload facts.</param>
    /// <param name="intercept">Whether a static backend can bind the call.</param>
    /// <param name="il">Whether the IL postprocessor owns static binding.</param>
    /// <param name="cancellationToken">Cancels binding.</param>
    /// <returns>The call or the operations retained for open fallback.</returns>
    private static CallFact Bind(SemanticModel semanticModel, InvocationExpressionSyntax invocation, BindingFacts facts,
        bool intercept, bool il, CancellationToken cancellationToken)
    {
        // The facade is static. Bind the receiver before the invocation to avoid overload resolution on unrelated APIs.
        if (invocation.Expression is MemberAccessExpressionSyntax access &&
            semanticModel.GetSymbolInfo(access.Expression, cancellationToken).Symbol is { } receiver &&
            !IsFacadeType(receiver is IAliasSymbol alias ? alias.Target : receiver))
            return default;
        var method = semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol as IMethodSymbol;
        if (method == null || !IsFacade(method))
            return new CallFact(null, OperationUsageAnalysis.Unbound(semanticModel, invocation, method, cancellationToken));

        ITypeSymbol payload = method.TypeArguments[0];
        SerializerApi? api = facts.Apis.GetOrAdd(method.OriginalDefinition, Classify);
        if (api == null || facts.Payloads.GetOrAdd(payload, facts.Create).Value is not { } binding)
            return new CallFact(null, OperationUsageAnalysis.Unbound(semanticModel, invocation, method, cancellationToken));

        bool profiled = method.Arity == 2;
        string? location = !intercept ? null : il
            ? StaticCallKey.Attribute(method, profiled ? 1 : -1)
            : Location(semanticModel, invocation, cancellationToken);
        return new CallFact(new SerializerCallModel(api.Value, binding, location ?? string.Empty, profiled), GeneratedOperations.None);
    }

    /// <summary>Whether a method belongs to the generated serializer facade.</summary>
    /// <param name="method">A bound method.</param>
    /// <returns>Whether its declaring type is <c>LuminPack.LuminPackSerializer</c>.</returns>
    private static bool IsFacade(IMethodSymbol method) => method.Arity is 1 or 2 && IsFacadeType(method.ContainingType);

    /// <summary>Identifies the serializer type independently of its spelling at the use site.</summary>
    /// <param name="symbol">The bound receiver or declaring type.</param>
    /// <returns>Whether it is the serializer facade.</returns>
    private static bool IsFacadeType(ISymbol symbol) =>
        symbol is INamedTypeSymbol { Name: "LuminPackSerializer", ContainingType: null,
            ContainingNamespace: { Name: "LuminPack", ContainingNamespace.IsGlobalNamespace: true } };

    /// <summary>Shares normalized payload and overload facts across all locations in one compilation.</summary>
    private sealed class BindingFacts
    {
        private readonly Compilation _compilation;
        private readonly GeneratorOptions _options;
        internal readonly ConcurrentDictionary<IMethodSymbol, SerializerApi?> Apis = new(SymbolEqualityComparer.Default);
        internal readonly ConcurrentDictionary<ITypeSymbol, Lazy<PayloadBinding?>> Payloads = new(SymbolEqualityComparer.Default);

        /// <summary>Captures the compilation owning the two binding caches.</summary>
        /// <param name="compilation">The consuming compilation.</param>
        internal BindingFacts(Compilation compilation)
        {
            _compilation = compilation;
            _options = GeneratorOptionsFactory.From(compilation);
        }

        /// <summary>Defers payload modeling until the shared cache selects this entry.</summary>
        /// <param name="payload">The closed payload symbol.</param>
        /// <returns>The single publication of the normalized payload binding.</returns>
        internal Lazy<PayloadBinding?> Create(ITypeSymbol payload) => new(() => Model(payload));

        /// <summary>Normalizes one closed payload after its cache entry has been selected.</summary>
        /// <param name="payload">The closed payload symbol.</param>
        /// <returns>The normalized payload binding, or null for an open or inaccessible payload.</returns>
        private PayloadBinding? Model(ITypeSymbol payload)
        {
            if (!IsClosed(payload) || !Accessible(payload))
                return null;
            using var names = ReadableTypeName.Use(_options.AmbiguousNames);
            string spelling = payload.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(PayloadFormat);
            MemberModel? root = TypeModelFactory.TryCreateRoot(payload, _compilation, _options);
            string? definition = null, typeArguments = null;
            if (payload is INamedTypeSymbol { IsGenericType: true } generic && generic.OriginalDefinition.SpecialType == SpecialType.None)
            {
                definition = generic.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                typeArguments = string.Join(", ", TypeModelFactory.AllTypeArguments(generic).Select(argument => argument.ToDisplayString(PayloadFormat)));
            }
            return new PayloadBinding(spelling, root, definition, typeArguments, TypeModelFactory.EntryShapeOf(payload, _compilation, _options),
                OperationUsageAnalysis.Payload(payload, _compilation));
        }
    }

    private static readonly SymbolDisplayFormat PayloadFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.ExpandNullable);

    /// <summary>Whether generated code outside the declaring type can name the type: a private or protected nested type keeps
    /// its call on the facade.</summary>
    /// <param name="type">The payload type.</param>
    /// <returns>Whether the generated interceptor can name the payload.</returns>
    private static bool Accessible(ITypeSymbol type)
    {
        while (type is IArrayTypeSymbol array)
            type = array.ElementType;
        for (ITypeSymbol? current = type; current != null; current = current.ContainingType)
            if (current.DeclaredAccessibility is Microsoft.CodeAnalysis.Accessibility.Private or Microsoft.CodeAnalysis.Accessibility.Protected or
                Microsoft.CodeAnalysis.Accessibility.ProtectedAndInternal ||
                current is INamedTypeSymbol named && !named.TypeArguments.All(Accessible))
                return false;
        return true;
    }

    /// <summary>Checks whether the compiler has a concrete payload type.</summary>
    /// <param name="type">The payload type.</param>
    /// <returns>Whether the payload is closed.</returns>
    private static bool IsClosed(ITypeSymbol type) => type.TypeKind is not (TypeKind.Error or TypeKind.TypeParameter or TypeKind.Dynamic) &&
        !ContainsTypeParameter(type);

    /// <summary>Checks the payload and its enclosing constructions for open arguments.</summary>
    /// <param name="type">The payload or one of its component types.</param>
    /// <returns>Whether the type contains a type parameter.</returns>
    private static bool ContainsTypeParameter(ITypeSymbol type) => type switch
    {
        ITypeParameterSymbol => true,
        IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
        INamedTypeSymbol named => named.TypeArguments.Any(ContainsTypeParameter) ||
            named.ContainingType != null && ContainsTypeParameter(named.ContainingType),
        IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
        _ => false,
    };

    /// <summary>Classifies the original facade overload by its parameter shapes.</summary>
    /// <param name="definition">The facade method definition.</param>
    /// <returns>The intercepted API, or null for an unsupported overload.</returns>
    private static SerializerApi? Classify(IMethodSymbol definition)
    {
        var parameters = definition.Parameters;
        string First() => Shape(parameters[0].Type);
        bool returnsValue = definition.ReturnType is ITypeParameterSymbol;
        switch (definition.Name)
        {
            case "Serialize" when parameters.Length == 1 && parameters[0].Type is ITypeParameterSymbol && !definition.ReturnsVoid:
                return SerializerApi.SerializeArray;
            case "Serialize" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackWriter":
                return SerializerApi.SerializeWriter;
            case "Deserialize" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackReader":
                return SerializerApi.DeserializeReader;
            case "SerializeJson" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackJsonWriter":
                return SerializerApi.SerializeJsonWriter;
            case "DeserializeJson" when parameters.Length == 2 && parameters[0].RefKind == RefKind.Ref && First() == "LuminPackJsonReader":
                return SerializerApi.DeserializeJsonReader;
            case "Serialize" when parameters.Length == 2 && parameters[0].Type is ITypeParameterSymbol:
                return Shape(parameters[1].Type) switch
                {
                    "IBufferWriter" => SerializerApi.SerializeBufferWriter,
                    "LuminBufferWriter" => SerializerApi.SerializeBuffer,
                    _ => null,
                };
            case "Deserialize" when parameters.Length == 1 && returnsValue:
                return First() switch
                {
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeSpan,
                    "LuminBufferWriter" => SerializerApi.DeserializeBuffer,
                    _ => null,
                };
            case "Deserialize" when parameters.Length == 2 && parameters[1].RefKind == RefKind.Ref && parameters[1].Type is ITypeParameterSymbol:
                return First() switch
                {
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeSpanRef,
                    "LuminBufferWriter" => SerializerApi.DeserializeBufferRef,
                    "ReadOnlySequence" when parameters[0].Type is INamedTypeSymbol { TypeArguments.Length: 1 } sequence &&
                        sequence.TypeArguments[0].SpecialType == SpecialType.System_Byte => SerializerApi.DeserializeSequenceRef,
                    _ => null,
                };
            case "SerializeJson" when parameters.Length == 1 && parameters[0].Type is ITypeParameterSymbol && !definition.ReturnsVoid:
                return SerializerApi.SerializeJsonString;
            case "SerializeJson" when parameters.Length == 2 && parameters[0].Type is ITypeParameterSymbol:
                return Shape(parameters[1].Type) switch
                {
                    "IBufferWriter" => SerializerApi.SerializeJsonBufferWriter,
                    "LuminBufferWriter" => SerializerApi.SerializeJsonBuffer,
                    _ => null,
                };
            case "DeserializeJson" when parameters.Length == 1 && returnsValue:
                return First() switch
                {
                    "string" => SerializerApi.DeserializeJsonString,
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeJsonBytes,
                    "ReadOnlySpan<char>" => SerializerApi.DeserializeJsonChars,
                    "LuminBufferWriter" => SerializerApi.DeserializeJsonBuffer,
                    _ => null,
                };
            case "DeserializeJson" when parameters.Length == 2 && parameters[1].RefKind == RefKind.Ref && parameters[1].Type is ITypeParameterSymbol:
                return First() switch
                {
                    "string" => SerializerApi.DeserializeJsonStringRef,
                    "ReadOnlySpan<byte>" => SerializerApi.DeserializeJsonBytesRef,
                    "ReadOnlySpan<char>" => SerializerApi.DeserializeJsonCharsRef,
                    "LuminBufferWriter" => SerializerApi.DeserializeJsonBufferRef,
                    _ => null,
                };
            case "SerializeAsync" when parameters.Length == 3 && First() == "Stream" && parameters[1].Type is ITypeParameterSymbol:
                return SerializerApi.SerializeStream;
            case "DeserializeAsync" when parameters.Length == 2 && First() == "Stream":
                return SerializerApi.DeserializeStream;
            case "DeserializeAsync" when parameters.Length == 3 && First() == "Stream" && parameters[1].Type is ITypeParameterSymbol:
                return SerializerApi.DeserializeStreamValue;
            default:
                return null;
        }
    }

    /// <summary>Spells the part of a parameter type that distinguishes facade overloads.</summary>
    /// <param name="type">The parameter type.</param>
    /// <returns>The overload classification key.</returns>
    private static string Shape(ITypeSymbol type) => type switch
    {
        { SpecialType: SpecialType.System_String } => "string",
        INamedTypeSymbol { Name: "ReadOnlySpan", TypeArguments.Length: 1 } span => "ReadOnlySpan<" +
            (span.TypeArguments[0].SpecialType == SpecialType.System_Byte ? "byte" :
                span.TypeArguments[0].SpecialType == SpecialType.System_Char ? "char" : "?") + ">",
        INamedTypeSymbol { Name: "IBufferWriter" } => "IBufferWriter",
        INamedTypeSymbol { Name: "LuminBufferWriter" } => "LuminBufferWriter",
        _ => type.Name,
    };

    /// <summary>Obtains the compiler-owned interception attribute without raising the Roslyn 4.3 loading requirement.</summary>
    /// <param name="model">The semantic model of the source file.</param>
    /// <param name="call">The invocation to intercept.</param>
    /// <param name="cancellationToken">Cancels location resolution.</param>
    /// <returns>The complete compiler attribute, if available.</returns>
    private static string? Location(SemanticModel model, InvocationExpressionSyntax call, CancellationToken cancellationToken)
    {
        object? location = LocationMethod!.Invoke(null, new object[] { model, call, cancellationToken });
        if (location == null)
            return null;
        object version = LocationVersion!.GetValue(location);
        string data = (string)LocationData!.GetValue(location);
        return "[global::System.Runtime.CompilerServices.InterceptsLocation(" + version + ", " + SymbolDisplay.FormatLiteral(data, true) + ")]";
    }

    /// <summary>Finds the compiler host's versioned interception location API.</summary>
    /// <returns>The public location method, or null for an older host.</returns>
    private static MethodInfo? FindLocationMethod()
    {
        var assembly = typeof(CSharpCompilation).Assembly;
        foreach (string name in new[] { "Microsoft.CodeAnalysis.CSharp.CSharpExtensions", "Microsoft.CodeAnalysis.CSharpExtensions" })
        {
            var method = assembly.GetType(name)?.GetMethod("GetInterceptableLocation", BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(SemanticModel), typeof(InvocationExpressionSyntax), typeof(CancellationToken) }, null);
            if (method != null)
                return method;
        }
        return null;
    }
}
