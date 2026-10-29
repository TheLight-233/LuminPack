using System;
using System.Linq;
using System.Text;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Emits the per-assembly serializer facade, invocation dispatch, option profiles and custom formatter registry.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class LuminPackAggregateGenerator : IIncrementalGenerator
{
    /// <summary>Registers the incremental analysis and source production pipeline with Roslyn.</summary>
    /// <param name="context">The context used for analysis, source production, or initialization.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var metaProvider = context.ParseOptionsProvider
            .Combine(context.CompilationProvider)
            .Select((pair, _) =>
            {
                var (opts, compilation) = pair;
                var cs = (CSharpParseOptions)opts;
                var net8 = cs.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER");
                var net9_OR_GREATER = cs.PreprocessorSymbolNames.Contains("NET9_0_OR_GREATER");
                var allowUnsafe = compilation.Options is CSharpCompilationOptions csharpOptions
                    ? csharpOptions.AllowUnsafe
                    : false;
                var registerMode = LuminPackGenerationTierResolver.RegisterModeFromAssembly(compilation);
                return new MetaInfo(cs, cs.LanguageVersion, net8, net9_OR_GREATER, allowUnsafe, registerMode);
            }).WithComparer(MetaInfoValueComparer.Instance).WithTrackingName("LuminPack.Aggregate.MetaInfo");

        var generationMode = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.LuminPackGenerationMode", out string? value);
                return LuminPackGenerationTierResolver.FromProperty(value);
            });

        var serializerFragments = new SerializerFacadeCache();
        var invocationSupport = new InvocationSupportCache();
        var customRegistration = new SemanticContributionCache<FrozenCustomRegistration>();
        var customRegistrySource = new RegistrySourceCache();

        var aggregateCache = new AggregateEmissionCache();
        var aggregateInputs = context.CompilationProvider
            .Combine(metaProvider)
            .Combine(generationMode)
            .Select((pair, _) => aggregateCache.PrepareInput(pair.Left.Left, pair.Left.Right, pair.Right))
            .WithTrackingName("LuminPack.Incremental.AggregateInput");
        var aggregateResults = aggregateInputs
            .Select((pair, _) =>
            {
                if (pair.Completed != null) return pair.Completed;
                System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.AggregateBuilds);
                var spc = new AggregateResultBuilder();
                var compilation = pair.GetCompilation();
                var meta = pair.Metadata;
                var mode = pair.Mode;

                // Unity auto-references managed plug-ins from every compilation. Merely
                // finding LuminPack in metadata therefore is not evidence that the target
                // assembly uses it; emitting support there pollutes dependent assemblies
                // with duplicate extension overloads.
                if (!CompilationTypeAnalysisCache.GetOrCreate(compilation).UsesLuminPack &&
                    !SerializerApiUsageAnalysis.GetOrCreate(compilation).HasSerializerUsage)
                    return pair.Complete(spc.Freeze());

                try
                {
                    // Validate [LuminPackable(GeneratorType.Custom)] types and emit the automatic
                    // LuminPackSerializer.Register registration module initializer.
                    var customBlocks = new System.Collections.Generic.List<string>();
                    var registrationDiagnostics = new System.Collections.Generic.List<FrozenDiagnostic>();
                    foreach (var declared in CompilationTypeAnalysisCache.GetOrCreate(compilation).DeclaredTypes)
                    {
                        var type = declared.Symbol;
                        if (!LuminPackExtensionGenerator.IsCustomGeneratorType(type) ||
                            !SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly)) continue;
                        var contribution = customRegistration.GetOrBuild(type, compilation, meta,
                            () => FrozenCustomRegistration.Build(type, meta),
                            () => System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.CustomRegistrationCacheHits));
                        foreach (var diagnostic in contribution.Diagnostics) spc.AddDiagnostic(diagnostic);
                        registrationDiagnostics.AddRange(contribution.RegistrationDiagnostics);
                        if (!string.IsNullOrEmpty(contribution.Registration)) customBlocks.Add(contribution.Registration);
                    }
                    foreach (var diagnostic in registrationDiagnostics) spc.AddDiagnostic(diagnostic);
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
                    LuminPackGenerationTier propertyTier = mode;
                    LuminPackGenerationTier assemblyTier = LuminPackGenerationTierResolver.FromAssembly(compilation);
                    LuminPackGenerationTier effectiveTier = (LuminPackGenerationTier)Math.Max((int)propertyTier, (int)assemblyTier);

                    ReachabilityAnalysis? reachability = effectiveTier != LuminPackGenerationTier.Full
                        ? ReachabilityAnalysisCache.GetOrCreate(compilation, effectiveTier)
                        : null;

                    foreach (var selected in LuminPackExtensionGenerator.GetOrderedFormatterTypes(compilation, reachability).OfType<INamedTypeSymbol>())
                        foreach (var diagnostic in ReferencedTypeMetadata.ValidatePropertyStorage(selected, compilation))
                            spc.ReportDiagnostic(diagnostic);

                    var optionProfiles = new System.Collections.Generic.Dictionary<string, CompiledOptions>(StringComparer.Ordinal);
                    var assemblyProfile = CompiledOptions.Resolve(null, compilation);
                    if (!assemblyProfile.IsDefault) optionProfiles[assemblyProfile.Identity] = assemblyProfile;
                    foreach (var diagnostic in CompiledOptions.Validate(compilation.Assembly)) spc.ReportDiagnostic(diagnostic);
                    foreach (var type in CompilationTypeAnalysisCache.GetOrCreate(compilation).FormatterTypes.Concat(
                        CompilationTypeAnalysisCache.GetOrCreate(compilation).DeclaredTypes.Select(declaration => (ITypeSymbol)declaration.Symbol)).Distinct<ITypeSymbol>(SymbolEqualityComparer.Default))
                    {
                        if (type is INamedTypeSymbol portable && !CodeEmitters.PortableStructPlan.IsSupported(portable))
                            spc.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.UnsupportedPortableStorage, type.Locations.FirstOrDefault(), type.ToDisplayString()));
                        foreach (var diagnostic in CompiledOptions.Validate(type)) spc.ReportDiagnostic(diagnostic);
                        var profile = CompiledOptions.Resolve(type, compilation);
                        if (!profile.IsDefault) optionProfiles[profile.Identity] = profile;
                        foreach (var member in type.GetMembers().Where(member => member is IFieldSymbol or IPropertySymbol))
                        {
                            foreach (var diagnostic in CompiledOptions.Validate(member)) spc.ReportDiagnostic(diagnostic);
                            profile = CompiledOptions.Resolve(member, compilation);
                            if (!profile.IsDefault) optionProfiles[profile.Identity] = profile;
                        }
                    }
                    var optionDeclarations = optionProfiles.Values.SelectMany(profile => profile.Declarations()).GroupBy(pair => pair.Key, StringComparer.Ordinal)
                        .OrderBy(group => group.Key, StringComparer.Ordinal).Select(group => group.First().Value).ToArray();
                    if (optionDeclarations.Length != 0)
                        spc.AddSource("LuminPackOptions.g.cs", string.Join(Environment.NewLine, optionDeclarations));

                    foreach (var formatter in LuminPackExtensionGenerator.GenerateInvocationFiles(compilation, meta, reachability, invocationSupport))
                        spc.AddSource(formatter.Name, formatter.Text);

                    foreach (var serializer in LuminPackSerializerGenerator.GenerateSerializerFiles(compilation, meta, reachability, serializerFragments))
                        spc.AddSource(serializer.Name, serializer.Text);
                }
                catch (Exception ex)
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
            }).WithTrackingName("LuminPack.Incremental.AggregateBatch");

        var aggregateFiles = aggregateResults.SelectMany(static (result, _) => result.Files)
            .WithTrackingName("LuminPack.Incremental.AggregateFiles");
        context.RegisterSourceOutput(aggregateFiles, static (output, file) =>
        {
            System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.AggregateFileOutputCallbacks);
            output.AddSource(file.Name, file.Text);
        });
        var aggregateDiagnostics = aggregateResults.SelectMany(static (result, _) => result.Diagnostics);
        context.RegisterSourceOutput(aggregateDiagnostics.Combine(context.CompilationProvider), static (output, input) =>
        {
            output.ReportDiagnostic(input.Left.Materialize(input.Right));
        });
    }
}
