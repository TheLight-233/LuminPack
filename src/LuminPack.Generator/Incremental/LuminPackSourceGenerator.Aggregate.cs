using System;
using System.Linq;
using System.Threading;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

public sealed partial class LuminPackSourceGenerator
{
    private static readonly CurrentSourceEmission<AggregateEmissionInput> CurrentAggregate = new();

    /// <summary>Registers assembly-wide support and publishes the files for one format.</summary>
    /// <param name="context">The incremental initialization context.</param>
    /// <param name="metaProvider">The shared project generation settings.</param>
    /// <param name="json">Whether this publisher owns JSON-only declarations.</param>
    private static void InitializeAggregatePipeline(IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<MetaInfo> metaProvider, bool json)
    {
        var serializerFragments = new SerializerFacadeCache();
        var invocationSupport = new InvocationSupportCache();
        var customRegistration = new SemanticContributionCache<FrozenCustomRegistration>();
        var customRegistrySource = new RegistrySourceCache();

        var aggregateCache = new AggregateEmissionCache();
        var aggregateInputs = context.CompilationProvider
            .Combine(metaProvider)
            .Select((pair, _) => CurrentAggregate.Get(pair.Left, string.Empty, pair.Right, json,
                () => aggregateCache.PrepareInput(pair.Left, pair.Right, LuminPackGenerationTier.Automatic)))
            .WithTrackingName("LuminPack.Incremental.AggregateInput");
        var aggregateResults = aggregateInputs
            .Select((pair, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return pair.Evaluate(() => Build(pair, cancellationToken));
            }).WithTrackingName(json ? "LuminPack.Incremental.JsonAggregateBatch" : "LuminPack.Incremental.AggregateBatch");

        var aggregateFiles = aggregateResults.Combine(metaProvider).SelectMany((result, _) =>
            result.Left.Files.Select(file => SelectFile(file, result.Right, json)).Where(file => file != null))
            .WithTrackingName("LuminPack.Incremental.AggregateFiles");
        context.RegisterSourceOutput(aggregateFiles, static (output, file) =>
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.AggregateFileOutputCallbacks);
            output.AddSource(file.Name, GeneratedSourceLayout.Apply(file.Text));
        });
        if (!json)
        {
            var aggregateDiagnostics = aggregateResults.SelectMany(static (result, _) => result.Diagnostics);
            context.RegisterSourceOutput(aggregateDiagnostics.Combine(context.CompilationProvider), static (output, input) =>
            {
                output.ReportDiagnostic(input.Left.Materialize(input.Right));
            });
        }

        FrozenAggregateResult Build(AggregateEmissionInput pair, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.AggregateBuilds);
            var spc = new AggregateResultBuilder();
            var compilation = pair.GetCompilation();
            var meta = pair.Metadata;
            var analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);

            // Unity auto-references managed plug-ins from every compilation. Merely
            // finding LuminPack in metadata therefore is not evidence that the target
            // assembly uses it; emitting support there pollutes dependent assemblies
            // with duplicate extension overloads.
            if (!analysis.UsesLuminPack &&
                !SerializerApiUsageAnalysis.GetOrCreate(compilation).HasSerializerUsage)
                return pair.Complete(spc.Freeze());

            try
            {
                // Validate [LuminPackable(GeneratorType.Custom)] types and emit the automatic
                // LuminPackSerializer.Register registration module initializer.
                var customBlocks = new System.Collections.Generic.List<string>();
                var registrationDiagnostics = new System.Collections.Generic.List<FrozenDiagnostic>();
                foreach (var declared in analysis.DeclaredTypes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var type = declared.Symbol;
                    if (!LuminPackExtensionGenerator.IsCustomGeneratorType(type) ||
                        !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
                        continue;
                    var contribution = customRegistration.GetOrBuild(type, compilation, meta,
                        () => FrozenCustomRegistration.Build(type, meta),
                        () => System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.CustomRegistrationCacheHits));
                    foreach (var diagnostic in contribution.Diagnostics)
                        spc.AddDiagnostic(diagnostic);
                    registrationDiagnostics.AddRange(contribution.RegistrationDiagnostics);
                    if (!string.IsNullOrEmpty(contribution.Registration))
                        customBlocks.Add(contribution.Registration);
                }
                foreach (var diagnostic in registrationDiagnostics)
                    spc.AddDiagnostic(diagnostic);
                if (customBlocks.Count != 0)
                {
                    bool isUnity = TypeMetaChecker.IsUnityProject(compilation);
                    string customCode = customRegistrySource.GetOrBuild(compilation.AssemblyName, isUnity, meta, customBlocks, () =>
                    {
                        string raw = CustomFormatterRegistrationGenerator.GenerateBlocks(customBlocks, compilation, meta, isUnity);
                        return string.IsNullOrEmpty(raw) ? raw : GeneratedSourceDocumentation.Complete(raw, meta.ParseOptions);
                    });
                    if (!string.IsNullOrEmpty(customCode))
                        spc.AddSource("LuminPackCustomFormatterRegistry.g.cs", customCode);
                }
                var reachability = ReachabilityAnalysisCache.GetOrCreate(compilation, LuminPackGenerationTier.Automatic);

                foreach (var selected in LuminPackExtensionGenerator.GetOrderedFormatterTypes(compilation, reachability).OfType<INamedTypeSymbol>())
                    foreach (var diagnostic in ReferencedTypeMetadata.ValidatePropertyStorage(selected, compilation))
                        spc.ReportDiagnostic(diagnostic);

                var optionProfiles = new System.Collections.Generic.Dictionary<string, CompiledOptions>(StringComparer.Ordinal);
                var assemblyProfile = CompiledOptions.Resolve(null, compilation);
                if (!assemblyProfile.IsDefault)
                    optionProfiles[assemblyProfile.Identity] = assemblyProfile;
                foreach (var diagnostic in CompiledOptions.Validate(compilation.Assembly))
                    spc.ReportDiagnostic(diagnostic);
                foreach (var type in analysis.FormatterTypes.Concat(
                    analysis.DeclaredTypes.Select(declaration => (ITypeSymbol)declaration.Symbol)).Distinct<ITypeSymbol>(SymbolEqualityComparer.Default))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (type is INamedTypeSymbol portable && !CodeEmitters.PortableStructPlan.IsSupported(portable))
                        spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsupportedPortableStorage, type.Locations.FirstOrDefault(), type.ToDisplayString()));
                    foreach (var diagnostic in CompiledOptions.Validate(type))
                        spc.ReportDiagnostic(diagnostic);
                    var profile = CompiledOptions.Resolve(type, compilation);
                    if (!profile.IsDefault)
                        optionProfiles[profile.Identity] = profile;
                    foreach (var member in type.GetMembers().Where(member => member is IFieldSymbol or IPropertySymbol))
                    {
                        foreach (var diagnostic in CompiledOptions.Validate(member))
                            spc.ReportDiagnostic(diagnostic);
                        profile = CompiledOptions.Resolve(member, compilation);
                        if (!profile.IsDefault)
                            optionProfiles[profile.Identity] = profile;
                    }
                }
                var optionDeclarations = optionProfiles.Values.SelectMany(profile => profile.Declarations()).GroupBy(pair => pair.Key, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => group.First().Value).ToArray();
                if (optionDeclarations.Length != 0)
                    spc.AddSource("LuminPackOptions.g.cs", GeneratedSourceDocumentation.Complete(string.Join(Environment.NewLine, optionDeclarations), meta.ParseOptions));

                var formatters = LuminPackExtensionGenerator.GenerateInvocationFiles(compilation, meta, reachability, invocationSupport,
                    out var navigationTargets, cancellationToken);
                foreach (var formatter in formatters)
                    spc.AddSource(formatter.Name, formatter.Text);

                foreach (var serializer in LuminPackSerializerGenerator.GenerateSerializerFiles(compilation, meta, reachability, serializerFragments,
                    cancellationToken, navigationTargets))
                    spc.AddSource(serializer.Name, serializer.Text);
                if (InterceptionSupport.IsEnabled(meta.ParseOptions))
                {
                    string interceptors = StaticSerializerBinding.For(compilation).Emit(
                        LuminPackExtensionGenerator.GetOrderedFormatterTypes(compilation, reachability));
                    if (interceptors.Length != 0)
                        spc.AddSource("LuminPack.Interceptors.g.cs", interceptors);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.GeneratorFailure,
                    Location.None,
                    ex.GetType().FullName + ": " + ex.Message + " | " +
                    (ex.StackTrace ?? string.Empty).Replace("\r", " ").Replace("\n", " ")));
                return spc.Freeze();
            }
            var result = spc.Freeze();
            var names = result.Files.Select(file => file.Name).ToArray();
            invocationSupport.Files.Retain(names.Concat(new[] { PartitionedSourceCache.CreateHintName("LuminPack.Dispatch", "Formatters") }));
            serializerFragments.Files.Retain(names.Concat(new[] { PartitionedSourceCache.CreateHintName("LuminPackSerializer.Common", "common") }));
            return pair.Complete(result);
        }
    }

    /// <summary>Assigns assembly-wide contributions to the binary, JSON, or shared publisher.</summary>
    /// <param name="file">The original generated contribution.</param>
    /// <param name="metadata">The current language and preprocessor settings.</param>
    /// <param name="json">Whether to select the JSON contribution.</param>
    /// <returns>A generated file for the current publisher, or null if it has no contribution.</returns>
    private static FrozenGeneratedSource SelectFile(FrozenGeneratedSource file, MetaInfo metadata, bool json)
    {
        if (file.Name.StartsWith("LuminPack.JsonSupport.", StringComparison.Ordinal))
            return json ? file : null;
        if (!file.Name.StartsWith("LuminPackSerializer.", StringComparison.Ordinal) &&
            !file.Name.StartsWith("LuminPack.Formatter", StringComparison.Ordinal) &&
            file.Name != "LuminPack.Dispatch.Formatters.g.cs")
            return json ? null : file;

        var partition = GeneratedFormatPartition.Get(file.Text, metadata.ParseOptions);
        if (json)
            return partition.Json.Length == 0 ? null : new FrozenGeneratedSource("LuminPackJson." + file.Name, partition.Json);
        return partition.Shared.Length == 0 ? null : new FrozenGeneratedSource(file.Name, partition.Shared);
    }
}
