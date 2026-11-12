using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering;
using LuminPack.SourceGenerator.Next.Rendering.Binary;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Rendering.Facade;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
using LuminPack.SourceGenerator.Next.Rendering.Json;
using LuminPack.SourceGenerator.Next.Rendering.Union;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Pipeline;

/// <summary>The Next generator: symbols enter, normalized models come out, and each payload kind owns one renderer.</summary>
/// <remarks>The pipeline has three boundaries. The analysis boundary (<see cref="TypeModelFactory"/>) is the only place that
/// sees Roslyn. The model boundary holds immutable, value-equal facts. The rendering boundary produces the final source through
/// one writer and one layout pass. No legacy analysis type or emitter text participates.</remarks>
internal static partial class NextPipeline
{
    private const string PackableAttribute = "LuminPack.Attribute.LuminPackableAttribute";

    /// <summary>Builds per-declaration normalized facts and one publication graph for all output folders.</summary>
    /// <param name="context">The incremental generator context.</param>
    internal static void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(PackableAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                GeneratorOptions options = GeneratorOptionsFactory.From(attributeContext.SemanticModel.Compilation);
                var symbol = (INamedTypeSymbol)attributeContext.TargetSymbol;
                Compilation compilation = attributeContext.SemanticModel.Compilation;
                // Union case and formatter names captured by the model spell ambiguous simple names with their namespace.
                using var names = ReadableTypeName.Use(options.AmbiguousNames);
                try
                {
                    DiagnosticInfo[] diagnostics = TypeModelFactory.Diagnose(symbol, compilation, options);
                    bool errors = DiagnosticDescriptors.HasErrors(diagnostics) ||
                        DiagnosticDescriptors.HasErrors(TypeModelFactory.DiagnoseAssembly(compilation, options));
                    cancellationToken.ThrowIfCancellationRequested();
                    ModelResult result = errors ? ModelResult.Unsupported("invalid serialization declaration") :
                        TypeModelFactory.TryCreate(symbol, compilation, options);
                    // A valid Custom declaration registers its own static operations.
                    string? custom = DiagnosticDescriptors.HasErrors(diagnostics) ? null :
                        TypeModelFactory.CustomRegistration(symbol, options, out bool customJson) is { } customType
                            ? CustomRegistrationSource.Block(customType, customJson) : null;
                    return new ModeledPayload(result, options, diagnostics, custom,
                        result.IsComplete ? OperationUsageAnalysis.Declaration(symbol, compilation) : OperationReach.Empty,
                        result.IsComplete && result.Model != null ? PayloadDocumentationFactory.Create(symbol, result.Model) : null);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    DiagnosticInfo diagnostic = DiagnosticInfo.Capture(DiagnosticDescriptors.GeneratorFailure.Id,
                        symbol.Locations.Length == 0 ? null : symbol.Locations[0], new[] { error.GetType().FullName + ": " + error.Message });
                    return new ModeledPayload(ModelResult.Unsupported("declaration analysis failed"), options, new[] { diagnostic });
                }
            })
            .WithTrackingName("LuminPack.Next.PayloadModels");
        var facts = context.CompilationProvider.Select(static (compilation, _) => new ProjectFacts(compilation));
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => SerializerCallAnalysis.IsCandidate(node) && ConsumerTree(node.SyntaxTree),
            static (syntax, token) => SerializerCallAnalysis.Analyze(syntax, token))
            .Where(static call => call.Call != null || call.Whole != GeneratedOperations.None || call.Open)
            .WithTrackingName("LuminPack.Next.SerializerCalls").Collect()
            .WithComparer(FactArrayComparer<CallFact>.Instance)
            .WithTrackingName("LuminPack.Next.CollectedCalls");
        var forwarded = calls.Combine(context.CompilationProvider).Select(static (input, token) =>
        {
            foreach (CallFact call in input.Left)
                if (call.Call == null || call.Open)
                    return GenericPayloadExpansion.Roots(input.Right, token);
            return RootMembers.Empty;
        }).WithTrackingName("LuminPack.Next.ForwardedPayloads");
        // Closed calls of source generic methods that reach the facade are redirected to closed wrapper copies.
        var wrappers = calls.Combine(context.CompilationProvider).Select(static (input, token) =>
        {
            foreach (CallFact call in input.Left)
                if (call.Open)
                    return GenericWrapperAnalysis.Analyze(input.Right, token);
            return WrapperSet.Empty;
        }).WithTrackingName("LuminPack.Next.GenericWrappers");
        // Generated extensions, state calls, registrations and facade method groups keep their operations for every payload.
        var usage = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => OperationUsageAnalysis.IsUsageCandidate(node) && ConsumerTree(node.SyntaxTree),
            static (syntax, token) => OperationUsageAnalysis.Usage(syntax, token))
            .WithTrackingName("LuminPack.Next.OperationUsage").Collect().Select(static (input, _) =>
            {
                var operations = GeneratedOperations.None;
                foreach (GeneratedOperations item in input)
                    operations |= item;
                return operations;
            });
        // Referenced types the assembly marks with [assembly: LuminPackable(typeof(T))] are payloads of this compilation.
        var external = context.CompilationProvider.Select(static (compilation, token) => ExternalPayloads(compilation, token))
            .WithComparer(FactArrayComparer<ModeledPayload>.Instance).WithTrackingName("LuminPack.Next.ExternalPayloads");
        var declared = models.Collect().WithComparer(FactArrayComparer<ModeledPayload>.Instance).Combine(external)
            .Select(static (input, _) => input.Right.IsEmpty ? input.Left : input.Left.AddRange(input.Right))
            .WithComparer(FactArrayComparer<ModeledPayload>.Instance);
        var requests = declared
            .Combine(calls).Combine(forwarded.Combine(wrappers)).Combine(facts.Combine(usage)).Select(static (input, _) =>
        {
            ImmutableArray<ModeledPayload> payloads = input.Left.Left.Left;
            var bound = ImmutableArray.CreateBuilder<SerializerCallModel>();
            GeneratedOperations whole = input.Right.Right;
            foreach (CallFact call in input.Left.Left.Right)
            {
                whole |= call.Whole;
                if (call.Call != null)
                    bound.Add(call.Call);
            }
            ProjectFacts project = input.Right.Left;
            var diagnostics = new List<DiagnosticInfo>(project.Diagnostics);
            foreach (ModeledPayload payload in payloads)
                diagnostics.AddRange(payload.Diagnostics);
            // A wrapper body's facade calls bind entries like source calls, located only in the wrapper.
            foreach (WrapperModel wrapper in input.Left.Right.Right.Wrappers)
                bound.AddRange(wrapper.Calls);
            return new PublicationRequest(payloads, bound.ToImmutable(), input.Left.Right.Left, input.Left.Right.Right,
                project.Options, project.Policy, project.Symbols, diagnostics.ToArray(), project.Fixed | whole);
        });
        var diagnostics = requests.Select(static (request, _) => request.Diagnostics).WithComparer(DiagnosticInfoComparer.Instance);
        context.RegisterSourceOutput(diagnostics.Combine(context.CompilationProvider), static (production, input) =>
        {
            foreach (DiagnosticInfo diagnostic in input.Left)
                production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic, input.Right));
        });
        InitializePublication(context, requests);
    }

    /// <summary>Models the referenced types the consuming assembly marks as external roots.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The external payloads, or an empty array when the assembly marks none.</returns>
    /// <remarks>A marked type is analyzed through the complete-metadata view, so private and automatic-property storage of the
    /// referenced assembly is visible to its layout mirror. An imported Union root writes through the virtual dispatch its own
    /// assembly generated. Open generic markers, which the original specialized from observed constructions, are not modeled yet.</remarks>
    private static ImmutableArray<ModeledPayload> ExternalPayloads(Compilation compilation, CancellationToken cancellationToken)
    {
        List<INamedTypeSymbol> marked = TypeModelFactory.MarkedTypes(compilation);
        if (marked.Count == 0)
            return ImmutableArray<ModeledPayload>.Empty;
        GeneratorOptions options = GeneratorOptionsFactory.From(compilation);
        Compilation view = TypeModelFactory.Complete(compilation);
        using var names = ReadableTypeName.Use(options.AmbiguousNames);
        var payloads = ImmutableArray.CreateBuilder<ModeledPayload>(marked.Count);
        foreach (INamedTypeSymbol type in marked)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (type.IsGenericType)
                continue;
            INamedTypeSymbol complete = TypeModelFactory.InView(type, view);
            DiagnosticInfo[] missing = TypeModelFactory.MissingStorage(complete);
            ModelResult result = missing.Length != 0 ? ModelResult.Unsupported("imported storage is incomplete") :
                TypeModelFactory.TryCreate(type, compilation, options);
            payloads.Add(new ModeledPayload(result, options, missing, null,
                result.IsComplete ? OperationUsageAnalysis.Declaration(complete, view) : OperationReach.Empty,
                result.IsComplete && result.Model != null ? PayloadDocumentationFactory.Create(complete, result.Model) : null));
        }
        return payloads.ToImmutable();
    }

    /// <summary>Excludes the fixed generated facade from consumer-call discovery.</summary>
    /// <param name="tree">The invocation's source tree.</param>
    /// <returns>Whether the tree belongs to the consumer.</returns>
    private static bool ConsumerTree(SyntaxTree tree) =>
        tree.FilePath.IndexOf("LuminPack.Generator.Next/", StringComparison.Ordinal) < 0 &&
        tree.FilePath.IndexOf("LuminPack.Generator.Next" + (char)92, StringComparison.Ordinal) < 0;

    /// <summary>Compares project-wide normalized facts independently of compilation identities.</summary>
    private sealed class ProjectFacts : IEquatable<ProjectFacts>
    {
        internal readonly GeneratorOptions Options;
        internal readonly WirePolicy Policy;
        internal readonly string[] Symbols;
        internal readonly DiagnosticInfo[] Diagnostics;

        /// <summary>The operations every payload keeps regardless of calls.</summary>
        internal readonly GeneratedOperations Fixed;

        /// <summary>Captures compiler-free settings, conditional symbols and assembly diagnostics.</summary>
        /// <param name="compilation">The current compiler input.</param>
        internal ProjectFacts(Compilation compilation)
        {
            Options = GeneratorOptionsFactory.From(compilation);
            Policy = TypeModelFactory.AssemblyPolicy(compilation);
            Diagnostics = TypeModelFactory.DiagnoseAssembly(compilation, Options);
            Fixed = OperationUsageAnalysis.Fixed(compilation);
            var symbols = new List<string>();
            foreach (SyntaxTree tree in compilation.SyntaxTrees)
            {
                if (tree.Options is CSharpParseOptions parse)
                    symbols.AddRange(parse.PreprocessorSymbolNames);
                break;
            }
            symbols.Sort(StringComparer.Ordinal);
            Symbols = symbols.ToArray();
        }

        /// <inheritdoc/>
        public bool Equals(ProjectFacts? other) => other != null && Fixed == other.Fixed && Options.Equals(other.Options) && Policy.Equals(other.Policy) &&
            Same(Symbols, other.Symbols) && Same(Diagnostics, other.Diagnostics);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as ProjectFacts);

        /// <inheritdoc/>
        public override int GetHashCode() => Options.GetHashCode() ^ Policy.GetHashCode();
    }

    /// <summary>Compares normalized arrays without an enumerator or predicate allocation.</summary>
    /// <typeparam name="T">The fact type.</typeparam>
    /// <param name="left">The previous facts.</param>
    /// <param name="right">The current facts.</param>
    /// <returns>Whether every fact is equal in order.</returns>
    private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T>
    {
        if (left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (!left[index].Equals(right[index]))
                return false;
        return true;
    }

    /// <summary>Compares collected value facts without boxing immutable-array enumerators.</summary>
    /// <typeparam name="T">The fact type.</typeparam>
    /// <param name="left">The preceding facts.</param>
    /// <param name="right">The current facts.</param>
    /// <returns>Whether every fact is equal in order.</returns>
    private static bool Same<T>(ImmutableArray<T> left, ImmutableArray<T> right) where T : IEquatable<T>
    {
        if (left.Length != right.Length)
            return false;
        for (int index = 0; index < left.Length; index++)
            if (!left[index].Equals(right[index]))
                return false;
        return true;
    }

    private readonly struct ModeledPayload : IEquatable<ModeledPayload>
    {
        /// <summary>Captures one declaration result, its settings and diagnostics.</summary>
        /// <param name="result">The declaration analysis result.</param>
        /// <param name="options">The normalized project settings.</param>
        /// <param name="diagnostics">The normalized declaration and assembly diagnostics.</param>
        /// <param name="customRegistration">The optional typed custom registration block.</param>
        /// <param name="reach">The payload identities the declaration reaches.</param>
        /// <param name="documentation">Compiler-free layout and member-link facts for caller-facing XML.</param>
        internal ModeledPayload(ModelResult result, GeneratorOptions options, DiagnosticInfo[] diagnostics, string? customRegistration = null,
            OperationReach? reach = null, PayloadDocumentation? documentation = null)
        {
            Documentation = documentation;
            Reach = reach ?? OperationReach.Empty;
            CustomRegistration = customRegistration;
            Diagnostics = diagnostics;
            Result = result;
            Options = options;
        }

        internal ModelResult Result
        {
            get;
        }

        internal PayloadDocumentation? Documentation { get; }

        internal GeneratorOptions Options
        {
            get;
        }

        internal DiagnosticInfo[] Diagnostics
        {
            get;
        }

        /// <summary>The payload identities the declaration reaches, which keep its operations.</summary>
        internal OperationReach Reach
        {
            get;
        }

        /// <summary>The registration block of a valid Custom declaration, or null.</summary>
        internal string? CustomRegistration
        {
            get;
        }

        /// <inheritdoc/>
        public bool Equals(ModeledPayload other) => Result.Equals(other.Result) && Options.Equals(other.Options) &&
            CustomRegistration == other.CustomRegistration && Reach.Equals(other.Reach) && Equals(Documentation, other.Documentation) &&
            SameDiagnostics(Diagnostics, other.Diagnostics);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is ModeledPayload other && Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int hash = Result.GetHashCode() ^ Options.GetHashCode();
            foreach (DiagnosticInfo diagnostic in Diagnostics)
                hash ^= diagnostic.GetHashCode();
            return hash;
        }

        /// <summary>Compares normalized diagnostic arrays in order.</summary>
        /// <param name="left">The preceding diagnostics.</param>
        /// <param name="right">The current diagnostics.</param>
        /// <returns>Whether every diagnostic is equal in order.</returns>
        private static bool SameDiagnostics(DiagnosticInfo[] left, DiagnosticInfo[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
                if (!left[index].Equals(right[index]))
                    return false;
            return true;
        }
    }
}
