using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
using LuminPack.SourceGenerator.Next.Rendering.Facade;
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
        // Check declaration-owned options even when the attributed declaration has no generated model.
        var optionScopes = context.SyntaxProvider.ForAttributeWithMetadataName(
            "LuminPack.Attribute.LuminPackOptionAttribute", static (node, _) => ConsumerTree(node.SyntaxTree),
            static (attributeContext, _) => InvalidOptionScopes(attributeContext))
            .Where(static diagnostics => !diagnostics.IsEmpty)
            .Collect();
        context.RegisterSourceOutput(optionScopes.Combine(context.CompilationProvider), static (production, input) =>
        {
            var seen = new HashSet<DiagnosticInfo>();
            foreach (ImmutableArray<DiagnosticInfo> items in input.Left)
                foreach (DiagnosticInfo diagnostic in items)
                    if (seen.Add(diagnostic))
                        production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic, input.Right));
        });

        var models = context.SyntaxProvider.ForAttributeWithMetadataName(PackableAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, token) => AnalyzeDeclaration((INamedTypeSymbol)attributeContext.TargetSymbol,
                attributeContext.SemanticModel.Compilation, token))
            .WithTrackingName("LuminPack.Next.PayloadModels");
        var derived = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null } or RecordDeclarationSyntax { BaseList: not null },
                static (syntax, token) => AnalyzeDerivedDeclaration(syntax, token))
            .Where(static payload => payload.HasValue).Select(static (payload, _) => payload.GetValueOrDefault())
            .WithTrackingName("LuminPack.Next.DerivedPayloadModels")
            .Collect().WithComparer(FactArrayComparer<ModeledPayload>.Instance);
        var facts = context.CompilationProvider.Select(static (compilation, _) => new ProjectFacts(compilation));
        var calls = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => SerializerCallAnalysis.IsCandidate(node) && ConsumerTree(node.SyntaxTree),
            static (syntax, _) => (InvocationExpressionSyntax)syntax.Node)
            .Collect().Combine(context.CompilationProvider)
            .Select(static (input, token) => SerializerCallAnalysis.Analyze(input.Right, input.Left, token))
            .WithComparer(FactArrayComparer<CallFact>.Instance)
            .WithTrackingName("LuminPack.Next.CollectedCalls");
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
        var declared = models.Collect().WithComparer(FactArrayComparer<ModeledPayload>.Instance).Combine(derived)
            .Select(static (input, _) => input.Right.IsEmpty ? input.Left : input.Left.AddRange(input.Right))
            .WithComparer(FactArrayComparer<ModeledPayload>.Instance).Combine(external)
            .Select(static (input, _) => input.Right.IsEmpty ? input.Left : input.Left.AddRange(input.Right))
            .WithComparer(FactArrayComparer<ModeledPayload>.Instance);
        var cppExport = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.LuminPackExportCppContract", out string? value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        var cppCompilation = context.CompilationProvider.Combine(cppExport)
            .Select(static (input, _) => input.Right ? input.Left : null);
        context.RegisterSourceOutput(declared.Combine(cppCompilation), static (production, input) =>
        {
            if (input.Right is not { } compilation)
                return;

            var roots = new List<TypeModel>();
            var custom = new List<(string Id, string Type, bool Reference, bool Json)>();
            foreach (ModeledPayload payload in input.Left)
            {
                if (payload.Result.IsComplete && payload.Result.Model is { } model)
                    roots.Add(model);
                if (payload.CustomContract is { } registration)
                    custom.Add(registration);
            }
            if (roots.Count != 0 || custom.Count != 0)
            {
                GeneratorOptions options = GeneratorOptionsFactory.From(compilation);
                production.AddSource("LuminPackGen.CppContracts.g.cs",
                    CppContractExport.Render(roots, options, model => TypeModelFactory.TryCreateCppNativeStorage(model, compilation, options),
                        TypeModelFactory.CreateCppRawCompoundStorage(compilation, options), custom, TypeModelFactory.AssemblyPolicy(compilation),
                        production.CancellationToken));
            }
        });
        var report = context.AnalyzerConfigOptionsProvider.Select(static (options, _) =>
            options.GlobalOptions.TryGetValue("build_property.LuminPackReportGeneratorOutput", out string? value) &&
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase));
        var requests = declared
            .Combine(calls).Combine(facts.Combine(usage)).Combine(report).Select(static (request, _) =>
        {
            var input = request.Left;
            ImmutableArray<ModeledPayload> payloads = input.Left.Left;
            var bound = ImmutableArray.CreateBuilder<SerializerCallModel>();
            ProjectFacts project = input.Right.Left;
            var diagnostics = new List<DiagnosticInfo>(project.Diagnostics);
            HashSet<DiagnosticInfo>? callDiagnostics = null;
            GeneratedOperations usage = input.Right.Right;
            GeneratedOperations fallback = GeneratedOperations.None;
            foreach (CallFact call in input.Left.Right)
            {
                fallback |= call.Whole;
                if (call.Call != null)
                {
                    bound.Add(call.Call);
                    foreach (DiagnosticInfo diagnostic in call.Call.Binding.Diagnostics)
                        if ((callDiagnostics ??= new HashSet<DiagnosticInfo>()).Add(diagnostic))
                            diagnostics.Add(diagnostic);
                }
            }
            foreach (ModeledPayload payload in payloads)
                diagnostics.AddRange(payload.Diagnostics);
            GeneratedOperations fixedOperations = project.Fixed;
            if (!request.Right)
            {
                fixedOperations |= usage | fallback;
                usage = fallback = GeneratedOperations.None;
            }
            return new PublicationRequest(payloads, bound.ToImmutable(),
                project.Options, project.Policy, diagnostics.ToArray(), fixedOperations, usage, fallback);
        });
        var diagnostics = requests.Select(static (request, _) => request.Diagnostics).WithComparer(DiagnosticInfoComparer.Instance);
        context.RegisterSourceOutput(diagnostics.Combine(context.CompilationProvider), static (production, input) =>
        {
            foreach (DiagnosticInfo diagnostic in input.Left)
                production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic, input.Right));
        });
        InitializePublication(context, requests, report);
    }

    /// <summary>Models a declared payload after checking its source and assembly contracts.</summary>
    /// <param name="symbol">The payload declaration.</param>
    /// <param name="compilation">The compilation that contains the declaration.</param>
    /// <param name="cancellationToken">Cancels declaration analysis.</param>
    /// <returns>The normalized payload, registration and diagnostic facts.</returns>
    private static ModeledPayload AnalyzeDeclaration(INamedTypeSymbol symbol, Compilation compilation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        GeneratorOptions options = GeneratorOptionsFactory.From(compilation);
        // Union case and formatter names captured by the model spell ambiguous simple names with their namespace.
        using var names = ReadableTypeName.Use(options.AmbiguousNames);
        try
        {
            DiagnosticInfo[] diagnostics = TypeModelFactory.Diagnose(symbol, compilation, options);
            bool declarationErrors = DiagnosticDescriptors.HasErrors(diagnostics);
            bool errors = declarationErrors || DiagnosticDescriptors.HasErrors(TypeModelFactory.DiagnoseAssembly(compilation, options));
            cancellationToken.ThrowIfCancellationRequested();
            bool compilerUnion = CompilerUnion.IsUnion(symbol, compilation);
            MemberModel? formatterRoot = !errors && compilerUnion ? TypeModelFactory.TryCreateRoot(symbol, compilation, options) : null;
            ModelResult result = errors ? ModelResult.Unsupported("invalid serialization declaration") :
                compilerUnion ? ModelResult.Unsupported("compiler Union uses a shared formatter owner") :
                TypeModelFactory.TryCreate(symbol, compilation, options);
            // A valid Custom declaration registers its own static operations.
            bool customJson = false;
            string? customType = declarationErrors ? null : TypeModelFactory.CustomRegistration(symbol, options, out customJson);
            string? custom = customType == null ? null : CustomRegistrationSource.Block(customType, customJson);
            return new ModeledPayload(result, options, diagnostics, custom,
                result.IsComplete ? OperationUsageAnalysis.Declaration(symbol, compilation) : OperationReach.Empty,
                formatterRoot, formatterRoot != null ? TypeModelFactory.ReachKey(symbol) : null,
                customContract: errors || customType == null ? null :
                    (TypeModelFactory.ReachKey(symbol)!, customType, symbol.IsReferenceType, customJson));
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            DiagnosticInfo diagnostic = DiagnosticInfo.Capture(DiagnosticDescriptors.GeneratorFailure.Id,
                symbol.Locations.Length == 0 ? null : symbol.Locations[0], new[] { error.GetType().FullName + ": " + error.Message });
            return new ModeledPayload(ModelResult.Unsupported("declaration analysis failed"), options, new[] { diagnostic });
        }
    }

    /// <summary>Reports invalid declaration-owned generation and reference options.</summary>
    /// <param name="context">The bound option attribute and its target.</param>
    /// <returns>The declaration option diagnostics.</returns>
    private static ImmutableArray<DiagnosticInfo> InvalidOptionScopes(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is IAssemblySymbol)
            return ImmutableArray<DiagnosticInfo>.Empty;

        ImmutableArray<DiagnosticInfo>.Builder? diagnostics = null;
        bool seen = false;
        foreach (AttributeData option in context.TargetSymbol.GetAttributes())
        {
            if (!TypeModelFactory.IsUntargetedOption(option) || option.AttributeConstructor == null)
                continue;
            if (seen)
                Report(DiagnosticDescriptors.InvalidOptionTarget.Id, option, new[] { "Apply LuminPackOption only once per type, field or property." });
            seen = true;
        }
        foreach (AttributeData attribute in context.Attributes)
        {
            if (attribute.AttributeConstructor == null)
                continue;
            if (!TypeModelFactory.IsUntargetedOption(attribute))
            {
                Report(DiagnosticDescriptors.InvalidOptionTarget.Id, attribute, new[] { "Use the typeof argument only on an assembly LuminPackOption." });
                continue;
            }
            foreach (var argument in attribute.NamedArguments)
            {
                string? error = argument.Key switch
                {
                    "TrackReferences" when context.TargetSymbol is not INamedTypeSymbol { TypeKind: TypeKind.Class } =>
                        "TrackReferences applies only to class declarations.",
                    "GeneratorType" => TypeModelFactory.GenerationOptionError(context.TargetSymbol, argument.Value),
                    "IncludeDerivedTypes" => TypeModelFactory.DerivedInclusionOptionError(context.TargetSymbol, argument.Value),
                    _ => null,
                };
                if (error != null)
                    Report(DiagnosticDescriptors.InvalidCompiledOption.Id, attribute, new[] { argument.Key, error });
            }
        }
        return diagnostics?.ToImmutable() ?? ImmutableArray<DiagnosticInfo>.Empty;

        void Report(string id, AttributeData option, string[] arguments) =>
            (diagnostics ??= ImmutableArray.CreateBuilder<DiagnosticInfo>()).Add(DiagnosticInfo.Capture(
                id, option.ApplicationSyntaxReference?.GetSyntax().GetLocation(), arguments));
    }

    /// <summary>Models the referenced types the consuming assembly marks as external roots.</summary>
    /// <param name="compilation">The consuming compilation.</param>
    /// <param name="cancellationToken">Cancels analysis.</param>
    /// <returns>The external payloads, or an empty array when the assembly marks none.</returns>
    /// <remarks>A marked type is analyzed through the complete-metadata view, so private and automatic-property storage of the
    /// referenced assembly is visible to its layout mirror. An imported Union root writes through the virtual dispatch its own
    /// assembly generated. Open generic definitions publish through their closed call or member formatter contracts.</remarks>
    private static ImmutableArray<ModeledPayload> ExternalPayloads(Compilation compilation, CancellationToken cancellationToken)
    {
        IReadOnlyList<INamedTypeSymbol> marked = TypeModelFactory.MarkedTypes(compilation);
        if (marked.Count == 0)
            return ImmutableArray<ModeledPayload>.Empty;
        GeneratorOptions options = GeneratorOptionsFactory.From(compilation);
        if (DiagnosticDescriptors.HasErrors(TypeModelFactory.DiagnoseAssembly(compilation, options)))
            return ImmutableArray<ModeledPayload>.Empty;
        Compilation view = TypeModelFactory.Complete(compilation);
        using var names = ReadableTypeName.Use(options.AmbiguousNames);
        var payloads = ImmutableArray.CreateBuilder<ModeledPayload>(marked.Count);
        foreach (INamedTypeSymbol type in marked)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (type.IsGenericType && type.IsDefinition && !TypeModelFactory.IsUnion(type))
                continue;
            if (TypeModelFactory.ExternalCustomRegistration(type, compilation, options, out bool customJson) is { } customType)
            {
                payloads.Add(new ModeledPayload(ModelResult.Unsupported("Custom owns its formatter"), options,
                    Array.Empty<DiagnosticInfo>(), CustomRegistrationSource.Block(customType, customJson),
                    customContract: (TypeModelFactory.ReachKey(type)!, customType, type.IsReferenceType, customJson)));
                continue;
            }
            INamedTypeSymbol complete = TypeModelFactory.InView(type, view);
            DiagnosticInfo[] missing = TypeModelFactory.MissingStorage(complete);
            ModelResult result = missing.Length != 0 ? ModelResult.Unsupported("imported storage is incomplete") :
                TypeModelFactory.TryCreate(type, compilation, options);
            payloads.Add(new ModeledPayload(result, options, missing, null,
                result.IsComplete ? OperationUsageAnalysis.Declaration(complete, view) : OperationReach.Empty));
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
        internal readonly DiagnosticInfo[] Diagnostics;

        /// <summary>The operations every payload keeps regardless of calls.</summary>
        internal readonly GeneratedOperations Fixed;

        /// <summary>Captures compiler-free settings and assembly diagnostics.</summary>
        /// <param name="compilation">The current compiler input.</param>
        internal ProjectFacts(Compilation compilation)
        {
            Options = GeneratorOptionsFactory.From(compilation);
            Policy = TypeModelFactory.AssemblyPolicy(compilation);
            Diagnostics = TypeModelFactory.DiagnoseAssembly(compilation, Options);
            Fixed = OperationUsageAnalysis.Fixed(compilation);
        }

        /// <inheritdoc/>
        public bool Equals(ProjectFacts? other) => other != null && Fixed == other.Fixed && Options.Equals(other.Options) && Policy.Equals(other.Policy) &&
            Same(Diagnostics, other.Diagnostics);

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
        /// <param name="formatterRoot">The optional shared formatter for the declaration.</param>
        /// <param name="formatterKey">The declaration identity used by operation reachability.</param>
        /// <param name="customContract">The opaque identity and operations used by the C++ contract export.</param>
        internal ModeledPayload(ModelResult result, GeneratorOptions options, DiagnosticInfo[] diagnostics, string? customRegistration = null,
            OperationReach? reach = null, MemberModel? formatterRoot = null, string? formatterKey = null,
            (string Id, string Type, bool Reference, bool Json)? customContract = null)
        {
            Reach = reach ?? OperationReach.Empty;
            CustomRegistration = customRegistration;
            Diagnostics = diagnostics;
            Result = result;
            Options = options;
            FormatterRoot = formatterRoot;
            FormatterKey = formatterKey;
            CustomContract = customContract;
        }

        internal ModelResult Result
        {
            get;
        }

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

        /// <summary>The validated Custom identity and registered operations, or null.</summary>
        internal (string Id, string Type, bool Reference, bool Json)? CustomContract { get; }

        /// <summary>The shared formatter root published by a compiler Union declaration.</summary>
        internal MemberModel? FormatterRoot { get; }

        /// <summary>The formatter declaration identity used to retain its operations.</summary>
        internal string? FormatterKey { get; }

        /// <inheritdoc/>
        public bool Equals(ModeledPayload other) => Result.Equals(other.Result) && Options.Equals(other.Options) &&
            CustomRegistration == other.CustomRegistration && CustomContract == other.CustomContract && Reach.Equals(other.Reach) &&
            Equals(FormatterRoot, other.FormatterRoot) && FormatterKey == other.FormatterKey &&
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
