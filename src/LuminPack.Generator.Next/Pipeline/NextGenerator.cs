using System;
using System.Collections.Generic;
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
[Generator(LanguageNames.CSharp)]
public sealed class NextGenerator : IIncrementalGenerator
{
    private const string PackableAttribute = "LuminPack.Attribute.LuminPackableAttribute";

    /// <inheritdoc/>
    /// <remarks>The provider is keyed per attributed type: editing one declaration re-runs that payload's transform and its
    /// source output, while unrelated payloads keep their cached model. Project facts are cached per compilation and compared
    /// by value, so an edit that leaves them unchanged does not invalidate every model.</remarks>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(PackableAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                GeneratorOptions options = GeneratorOptionsFactory.From(attributeContext.SemanticModel.Compilation);
                var symbol = (INamedTypeSymbol)attributeContext.TargetSymbol;
                Compilation compilation = attributeContext.SemanticModel.Compilation;
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
                    return new ModeledPayload(result, options, diagnostics, custom);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    DiagnosticInfo diagnostic = DiagnosticInfo.Capture(DiagnosticDescriptors.GeneratorFailure.Id,
                        symbol.Locations.Length == 0 ? null : symbol.Locations[0], new[] { error.GetType().FullName + ": " + error.Message });
                    return new ModeledPayload(ModelResult.Unsupported("declaration analysis failed"), options, new[] { diagnostic });
                }
            })
            .WithTrackingName("LuminPack.Next.PayloadModels");
        var assemblyDiagnostics = context.CompilationProvider.Select(static (compilation, _) =>
            TypeModelFactory.DiagnoseAssembly(compilation, GeneratorOptionsFactory.From(compilation)))
            .WithComparer(DiagnosticInfoComparer.Instance);
        context.RegisterSourceOutput(assemblyDiagnostics.Combine(context.CompilationProvider), static (production, input) =>
        {
            foreach (DiagnosticInfo diagnostic in input.Left)
                production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic, input.Right));
        });
        var declarationDiagnostics = models.Select(static (payload, _) => payload.Diagnostics)
            .WithComparer(DiagnosticInfoComparer.Instance);
        context.RegisterSourceOutput(declarationDiagnostics.Combine(context.CompilationProvider), static (production, input) =>
        {
            foreach (DiagnosticInfo diagnostic in input.Left)
                production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic, input.Right));
        });
        context.RegisterSourceOutput(models, static (production, payload) =>
        {
            if (payload.Result.Model is not { } model || !payload.Result.IsComplete)
                return;
            try
            {
                var renderContext = new RenderContext(payload.Options, AliasTable.For(payload.Options.AvailableAliases));
                if (model is UnionModel)
                {
                    Emit(production, model, renderContext, UnionRenderer.Instance.RenderBinary(model, renderContext), string.Empty);
                    Emit(production, model, renderContext, UnionRenderer.Instance.RenderJson(model, renderContext), ".Json");
                    return;
                }
                // A bounded untrusted root's binary file is rendered with the shared owners, whose read bodies it copies.
                if (!BoundedRoot(model))
                    Emit(production, model, renderContext, BinaryRenderer.Instance.Render(model, renderContext), string.Empty);
                Emit(production, model, renderContext, JsonRenderer.Instance.Render(model, renderContext), ".Json");
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                production.ReportDiagnostic(DiagnosticDescriptors.Create(
                    DiagnosticInfo.Unlocated(DiagnosticDescriptors.GeneratorFailure.Id, error.Message)));
            }
        });
        var collected = models.Collect();
        var customRegistrations = collected.Select(static (payloads, _) =>
        {
            var blocks = new List<string>();
            GeneratorOptions? options = null;
            foreach (ModeledPayload payload in payloads)
                if (payload.CustomRegistration != null)
                {
                    blocks.Add(payload.CustomRegistration);
                    options = payload.Options;
                }
            return options == null ? string.Empty : CustomRegistrationSource.Render(blocks, options.AssemblyName, options.Unity);
        });
        context.RegisterSourceOutput(customRegistrations, static (production, source) =>
        {
            if (source.Length != 0)
                production.AddSource("LuminPack.CustomFormatterRegistry.g.cs", source);
        });
        // The assembly declaration selects the registered fallback profile, the default JSON encoding and the input cap.
        var assemblyPolicy = context.CompilationProvider.Select(static (compilation, _) => TypeModelFactory.AssemblyPolicy(compilation));
        context.RegisterSourceOutput(assemblyPolicy, static (production, policy) =>
            production.AddSource("LuminPack.FormatterDispatch.Options.g.cs", DispatchOptionsSource.Render(ProfileNames.Type(policy.Strings),
                policy.Strings.Encoding == StringEncoding.Utf8 ? 0 : 1, policy.Input == InputPolicy.Untrusted, policy.Limits.Input)));
        var profiles = collected.Combine(assemblyPolicy).Select(static (input, _) =>
        {
            var retained = new List<TypeModel>();
            foreach (ModeledPayload payload in input.Left)
                if (payload.Result.IsComplete && payload.Result.Model is { } model)
                    retained.Add(model);
            return ProfileSource.Render(retained, input.Right);
        });
        context.RegisterSourceOutput(profiles, static (production, source) =>
        {
            if (source.Length != 0)
                production.AddSource("LuminPack.Profiles.g.cs", source);
        });
        // Ordinary facade calls are discovered per call site. Their non-packable closed roots join the shared owner requirements.
        var calls = context.SyntaxProvider.CreateSyntaxProvider(static (node, _) => SerializerCallAnalysis.IsCandidate(node),
                static (syntax, cancellationToken) => SerializerCallAnalysis.Analyze(syntax, cancellationToken))
            .Where(static call => call != null)
            .Select(static (call, _) => call!)
            .WithTrackingName("LuminPack.Next.SerializerCalls")
            .Collect();
        var projectOptions = context.CompilationProvider.Select(static (compilation, _) => GeneratorOptionsFactory.From(compilation));
        // The shared formatter owners are collected once. Sorted requirements are order-independent, so the same complete payloads
        // publish the same files and an unchanged set reuses its cached output across edits.
        var sharedFormatters = collected.Combine(calls).Combine(projectOptions)
            .Select(static (input, _) =>
            {
                var retained = new List<TypeModel>();
                foreach (ModeledPayload payload in input.Left.Left)
                    if (payload.Result.Model is { } model && payload.Result.IsComplete)
                        retained.Add(model);
                var roots = new List<MemberModel>();
                foreach (SerializerCallModel call in input.Left.Right)
                    if (call.Root != null)
                        roots.Add(call.Root);
                return retained.Count == 0 && roots.Count == 0 ? SharedFormatterSet.Empty :
                    SharedFormatterSet.Collect(retained, input.Right, roots: roots);
            })
            .WithTrackingName("LuminPack.Next.SharedFormatters");
        context.RegisterSourceOutput(sharedFormatters, static (production, set) =>
        {
            try
            {
                SharedFormatterPublisher.Publish(production, set, GeneratedOperations.All);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                production.ReportDiagnostic(DiagnosticDescriptors.Create(
                    DiagnosticInfo.Unlocated(DiagnosticDescriptors.GeneratorFailure.Id, error.Message)));
            }
        });
        InitializeEntries(context, collected, calls, sharedFormatters);
        InitializeBoundedRoots(context, collected, sharedFormatters);
    }

    /// <summary>Reports whether a payload is an untrusted record root with declared collection, string or depth limits.</summary>
    /// <param name="model">The payload.</param>
    /// <returns>Whether its reads take checked copies of their nested reads.</returns>
    private static bool BoundedRoot(TypeModel model) => model is RecordModel { Rejection: null, Versioned: false } &&
        model.Policy.Input == InputPolicy.Untrusted && model.Policy.Limits.Bounded;

    /// <summary>Renders each bounded untrusted root's binary file and the checked copies of the nested reads it reaches.</summary>
    /// <remarks>Under declared limits, a nested read whose own entry applies other checks runs as a copy of that entry's read
    /// body with the root's limits frozen, as the original input readers did. The copies need the shared owner bodies, so these
    /// roots are rendered here instead of in the per-payload output.</remarks>
    private static void InitializeBoundedRoots(IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<System.Collections.Immutable.ImmutableArray<ModeledPayload>> collected,
        IncrementalValueProvider<SharedFormatterSet> sharedFormatters)
    {
        context.RegisterSourceOutput(collected.Combine(sharedFormatters), static (production, input) =>
        {
            var roots = new List<(RecordModel Model, GeneratorOptions Options)>();
            foreach (ModeledPayload payload in input.Left)
                if (payload.Result.IsComplete && payload.Result.Model is RecordModel record && BoundedRoot(record))
                    roots.Add((record, payload.Options));
            if (roots.Count == 0)
                return;
            try
            {
                SharedFormatterSet set = input.Right;
                var sources = new List<string>();
                foreach (var source in SharedFormatterPublisher.Render(set, GeneratedOperations.All))
                    sources.Add(source.Value);
                // Scalar owners keep their lowered calls, as raw scalar reads need no input checks.
                var scalars = new HashSet<string>(StringComparer.Ordinal);
                foreach (string scalar in set.Scalars)
                    scalars.Add(ReadableTypeName.Of(GeneratedNames.PayloadKey(scalar.Split('=')[0])));
                foreach (var (root, options) in roots)
                {
                    PayloadRenderModel? rootModel = RenderModelFactory.Create(root, options);
                    if (rootModel == null)
                        continue;
                    var payloads = new Dictionary<string, PayloadRenderModel>(StringComparer.Ordinal);
                    var copyable = new HashSet<string>(StringComparer.Ordinal);
                    foreach (ModeledPayload payload in input.Left)
                        if (payload.Result.IsComplete && payload.Result.Model is RecordModel { Rejection: null, Versioned: false } record &&
                            RenderModelFactory.Create(record, options) is { Circle: false, IsStruct: false, Mirror: null, Version: null } candidate &&
                            candidate.Mirrors.Length == 0)
                        {
                            payloads[candidate.Readable] = candidate;
                            copyable.Add(candidate.Readable);
                        }
                    foreach (string source in sources)
                        foreach (string owner in RootsDeclaredIn(source))
                            if (!scalars.Contains(owner))
                                copyable.Add(owner);
                    var redirect = new InputRedirect(rootModel.Readable, GeneratedNames.Extension(options.AssemblyName), root.Policy.Limits, copyable);
                    string file = PayloadEmitter.Render(rootModel.WithInput(root.Policy.Limits, root.Recursive, redirect), json: false,
                        options.AvailableAliases, GeneratedOperations.All);
                    var renderContext = new RenderContext(options, AliasTable.For(options.AvailableAliases));
                    Emit(production, root, renderContext, file, string.Empty);
                    Emit(production, root, renderContext, InputReadRenderer.RenderCopies(redirect, payloads, sources, options.AvailableAliases,
                        GeneratedOperations.All), ".InputReads");
                }
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                production.ReportDiagnostic(DiagnosticDescriptors.Create(
                    DiagnosticInfo.Unlocated(DiagnosticDescriptors.GeneratorFailure.Id, error.Message)));
            }
        });
    }

    /// <summary>Lists the root containers a shared owner file declares.</summary>
    /// <param name="source">The owner file.</param>
    /// <returns>The readable names of its <c>LuminRoots_</c> containers that publish a root read.</returns>
    private static IEnumerable<string> RootsDeclaredIn(string source)
    {
        const string declaration = " class LuminRoots_";
        int search = 0;
        while ((search = source.IndexOf(declaration, search, StringComparison.Ordinal)) >= 0)
        {
            int start = search + declaration.Length, end = start;
            while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_'))
                end++;
            search = end;
            int next = source.IndexOf(declaration, end, StringComparison.Ordinal);
            string block = next < 0 ? source.Substring(end) : source.Substring(end, next - end);
            if (block.Contains(" RootRead<TLuminProfile>(") || block.Contains(" RootRead("))
                yield return source.Substring(start, end - start);
        }
    }

    /// <summary>Publishes the generic facade and binds closed ordinary calls to the concrete roots of complete payloads and
    /// published shared owners.</summary>
    /// <remarks>The facade is post-initialization source, so calls bind against it while this generator runs. Each call site is
    /// analyzed independently; the interception file depends only on the value-equal call list and root catalog.</remarks>
    private static void InitializeEntries(IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<System.Collections.Immutable.ImmutableArray<ModeledPayload>> collected,
        IncrementalValueProvider<System.Collections.Immutable.ImmutableArray<SerializerCallModel>> calls,
        IncrementalValueProvider<SharedFormatterSet> sharedFormatters)
    {
        context.RegisterPostInitializationOutput(static production =>
        {
            production.AddSource("LuminPack.Serializer.g.cs", SerializerFacadeSource.Binary);
            production.AddSource("LuminPack.Serializer.Json.g.cs", SerializerFacadeSource.Json);
            production.AddSource("LuminPack.FormatterDispatch.g.cs", SerializerFacadeSource.Dispatch);
        });
        var enabled = context.CompilationProvider.Select(static (compilation, _) =>
            GeneratorOptionsFactory.From(compilation).Interceptors && SerializerCallAnalysis.Supported);
        var roots = collected.Combine(sharedFormatters).Select(static (input, _) =>
        {
            var entries = new List<KeyValuePair<string, RootEntry>>();
            foreach (ModeledPayload payload in input.Left)
                if (payload.Result.IsComplete && payload.Result.Model is { } model && !model.Identity.IsGeneric)
                {
                    string type = model.Identity.GlobalName;
                    string profile = model.Policy.Declared ? ProfileNames.Type(model.Policy.Strings) : RootEntry.DefaultProfile;
                    // The original concrete facade froze the payload's own cap and checked nothing for a trusted payload.
                    WirePolicy policy = model.Policy;
                    StringProfile jsonInput = policy.JsonInput;
                    entries.Add(new KeyValuePair<string, RootEntry>(type,
                        new RootEntry(ReadableTypeName.Of(GeneratedNames.PayloadKey(type)), profile, binary: true, json: true,
                            limit: policy.Input == InputPolicy.Untrusted ? policy.Limits.Input : null,
                            encoding: policy.Strings.Encoding == StringEncoding.Utf8 ? 0 : 1,
                            jsonReadProfile: jsonInput.Equals(policy.Strings) ? null : ProfileNames.Type(jsonInput))));
                }
            return new RootCatalog(entries, input.Right.PublishedRoots(), input.Right.Extension);
        });
        context.RegisterSourceOutput(calls.Combine(roots).Combine(enabled), static (production, input) =>
        {
            if (!input.Right)
                return;
            try
            {
                string source = InterceptorRenderer.Render(input.Left.Left, input.Left.Right);
                if (source.Length != 0)
                    production.AddSource("LuminPack.Interceptors.g.cs", source);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                production.ReportDiagnostic(DiagnosticDescriptors.Create(
                    DiagnosticInfo.Unlocated(DiagnosticDescriptors.GeneratorFailure.Id, error.Message)));
            }
        });
    }

    private static void Emit(SourceProductionContext production, TypeModel model, RenderContext context, string source, string suffix)
    {
        if (source.Length == 0)
            return;
        production.AddSource(model.Identity.DisplayName.Replace('<', '_').Replace('>', '_').Replace(',', '_').Replace(' ', '_') + suffix + ".g.cs", source);
    }

    private readonly struct ModeledPayload : IEquatable<ModeledPayload>
    {
        internal ModeledPayload(ModelResult result, GeneratorOptions options, DiagnosticInfo[] diagnostics, string? customRegistration = null)
        {
            CustomRegistration = customRegistration;
            Diagnostics = diagnostics;
            Result = result;
            Options = options;
        }

        internal ModelResult Result { get; }

        internal GeneratorOptions Options { get; }

        internal DiagnosticInfo[] Diagnostics { get; }

        /// <summary>The registration block of a valid Custom declaration, or null.</summary>
        internal string? CustomRegistration { get; }

        public bool Equals(ModeledPayload other) => Result.Equals(other.Result) && Options.Equals(other.Options) &&
            CustomRegistration == other.CustomRegistration && SameDiagnostics(Diagnostics, other.Diagnostics);

        public override bool Equals(object? obj) => obj is ModeledPayload other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Result.GetHashCode() ^ Options.GetHashCode();
            foreach (DiagnosticInfo diagnostic in Diagnostics)
                hash ^= diagnostic.GetHashCode();
            return hash;
        }

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
