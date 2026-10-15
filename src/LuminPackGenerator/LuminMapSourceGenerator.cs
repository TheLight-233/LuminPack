using System;
using System.Linq;
using System.Text;
using LuminPack.Code.Core;
using LuminPack.SourceGenerator.Mapping;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Provides lumin map source generator functionality.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class LuminMapSourceGenerator : IIncrementalGenerator
{
    private const string LuminMapToAttr = "LuminPack.Attribute.LuminMapToAttribute";
    private const string LuminMapperAttr = "LuminPack.Attribute.LuminMapperAttribute";

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
            }).WithComparer(MetaInfoValueComparer.Instance).WithTrackingName("LuminPack.Mapping.MetaInfo");

        var generationMode = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) =>
            {
                options.GlobalOptions.TryGetValue("build_property.LuminPackGenerationMode", out string? value);
                return LuminPackGenerationTierResolver.FromProperty(value);
            });

        var automaticCache = new SemanticContributionCache<FrozenMapperEmission>();
        var manualCache = new SemanticContributionCache<FrozenMapperEmission>();
        var automaticTypes = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
                static (syntax, _) => GetTypeWithAttribute(syntax, LuminMapToAttr))
            .Where(static type => type != null).Select(static (type, _) => type!);
        var manualTypes = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax declaration &&
                    declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.StaticKeyword)) &&
                    declaration.Modifiers.Any(modifier => modifier.IsKind(SyntaxKind.PartialKeyword)),
                static (syntax, _) => GetTypeWithAttribute(syntax, LuminMapperAttr))
            .Where(static type => type != null).Select(static (type, _) => type!);
        var autoMapDeclarations = automaticTypes.Combine(context.CompilationProvider).Combine(metaProvider)
            .Select((input, _) => automaticCache.GetOrBuild(input.Left.Left, input.Left.Right, input.Right,
                () => FrozenMapperEmission.Automatic(input.Left.Left, input.Left.Right, input.Right),
                () => System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.AutomaticMapperCacheHits)))
            .WithTrackingName("LuminPack.Incremental.AutomaticMapper");
        var manualMapDeclarations = manualTypes.Combine(context.CompilationProvider).Combine(metaProvider)
            .Select((input, _) => manualCache.GetOrBuild(input.Left.Left, input.Left.Right, input.Right,
                () => FrozenMapperEmission.Manual(input.Left.Left, input.Left.Right, input.Right),
                () => System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.ManualMapperCacheHits)))
            .WithTrackingName("LuminPack.Incremental.ManualMapper");
        RegisterMapperOutputs(context, autoMapDeclarations, automatic: true);
        RegisterMapperOutputs(context, manualMapDeclarations, automatic: false);
        var serializerFragments = new SerializerFacadeCache();
        var invocationSupport = new InvocationSupportCache();
        var customRegistration = new SemanticContributionCache<FrozenCustomRegistration>();
        var customRegistrySource = new RegistrySourceCache(custom: true);
        var mapperRegistrySource = new RegistrySourceCache(custom: false);
        var allAuto = autoMapDeclarations.Collect();
        var allManual = manualMapDeclarations.Collect();

        var aggregateCache = new AggregateEmissionCache();
        var aggregateInputs = allAuto.Combine(allManual)
            .Combine(context.CompilationProvider)
            .Combine(metaProvider)
            .Combine(generationMode)
            .Select((pair, _) => aggregateCache.PrepareInput(pair.Left.Left.Right,
                pair.Left.Left.Left.Left, pair.Left.Left.Left.Right, pair.Left.Right, pair.Right))
            .WithTrackingName("LuminPack.Incremental.AggregateInput");
        var aggregateResults = aggregateInputs
            .Select((pair, _) =>
            {
                if (pair.Completed != null) return pair.Completed;
                System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.AggregateBuilds);
                var spc = new AggregateResultBuilder();
                var compilation = pair.GetCompilation();
                var autoInfos = pair.Automatic;
                var manualInfos = pair.Manual;
                var meta = pair.Metadata;
                var mode = pair.Mode;

                // Unity auto-references managed plug-ins from every compilation. Merely
                // finding LuminPack in metadata therefore is not evidence that the target
                // assembly uses it; emitting support there pollutes dependent assemblies
                // with duplicate extension overloads.
                if (!CompilationTypeAnalysisCache.GetOrCreate(compilation).UsesLuminPack)
                    return pair.Complete(spc.Freeze());

                // Skip emission when there are no packable types or mapper declarations.
                bool hasMappers = !autoInfos.IsEmpty || !manualInfos.IsEmpty;

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
                    if (hasMappers)
                    {
                        var blocks = autoInfos.Select(info => info.Registration).Concat(manualInfos.Select(info => info.Registration)).ToArray();
                        string code = mapperRegistrySource.GetOrBuild(compilation.AssemblyName, TypeMetaChecker.IsUnityProject(compilation), meta, blocks, () =>
                        {
                            string raw = GenerateMappersRegistry(autoInfos, manualInfos, compilation, meta);
                            return string.IsNullOrEmpty(raw) ? raw : GeneratedSourceDocumentation.Complete(raw, meta.ParseOptions);
                        });
                        if (!string.IsNullOrEmpty(code))
                            spc.AddSource("GeneratedMappersRegistry.g.cs", code);
                    }

                    LuminPackGenerationTier propertyTier = mode;
                    LuminPackGenerationTier assemblyTier = LuminPackGenerationTierResolver.FromAssembly(compilation);
                    LuminPackGenerationTier effectiveTier = (LuminPackGenerationTier)Math.Max((int)propertyTier, (int)assemblyTier);

                    ReachabilityAnalysis? reachability = effectiveTier != LuminPackGenerationTier.Full
                        ? ReachabilityAnalysisCache.GetOrCreate(compilation, effectiveTier)
                        : null;

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
                return pair.Complete(spc.Freeze());
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

    /// <summary>Registers individually comparable mapper files and current-tree diagnostic reporting.</summary>
    /// <param name="context">The generator initialization context.</param>
    /// <param name="contributions">Immutable contributions in the legacy declaration order.</param>
    /// <param name="automatic">Whether to preserve the automatic mapper's legacy failure format.</param>
    private static void RegisterMapperOutputs(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<FrozenMapperEmission> contributions, bool automatic)
    {
        var files = contributions.SelectMany(static (value, _) => value.Output.Files);
        context.RegisterSourceOutput(files, (output, file) =>
        {
            System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.MapperFileOutputCallbacks);
            try { output.AddSource(file.Name, file.Text); }
            catch (Exception exception)
            {
                string message = automatic
                    ? exception.GetType().FullName + ": " + exception.Message + " | " + (exception.StackTrace ?? string.Empty).Replace("\r", " ").Replace("\n", " ")
                    : exception.ToString();
                output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GeneratorFailure, Location.None, message));
            }
        });
        var diagnostics = contributions.SelectMany(static (value, _) => value.Output.Diagnostics);
        context.RegisterSourceOutput(diagnostics.Combine(context.CompilationProvider), static (output, input) =>
            output.ReportDiagnostic(input.Left.Materialize(input.Right)));
    }

    /// <summary>Builds the mappers registry source from the supplied metadata.</summary>
    /// <param name="autoInfos">The auto infos used by this operation.</param>
    /// <param name="manualInfos">The manual infos used by this operation.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <param name="meta">The meta used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string GenerateMappersRegistry(
        System.Collections.Immutable.ImmutableArray<FrozenMapperEmission> autoInfos,
        System.Collections.Immutable.ImmutableArray<FrozenMapperEmission> manualInfos,
        Compilation compilation,
        MetaInfo meta)
    {
        bool isUnity = TypeMetaChecker.IsUnityProject(compilation);
        string asmRaw = compilation.AssemblyName ?? "Unknown";
        string asmSafe = SanitizeId(asmRaw);

        string registryFqn = asmSafe == "Unknown"
            ? "global::GeneratedMappersRegistry"
            : $"global::LuminPackRegisters.{asmSafe}.GeneratedMappersRegistry";

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("// LuminMapper registry — do not modify");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using global::System;");
        sb.AppendLine("using global::System.Collections.Generic;");
        sb.AppendLine("using global::System.Runtime.CompilerServices;");
        sb.AppendLine("using global::LuminPack;");
        sb.AppendLine("using global::LuminPack.Mapping;");
        sb.AppendLine();

        bool hasNs = asmSafe != "Unknown";
        if (hasNs) { sb.AppendLine($"namespace LuminPackRegisters.{asmSafe}"); sb.AppendLine("{"); }

        sb.AppendLine("    public static class GeneratedMappersRegistry");
        sb.AppendLine("    {");

        if (isUnity)
        {
            sb.AppendLine("#if UNITY_EDITOR");
            sb.AppendLine("        [global::UnityEditor.InitializeOnLoadMethod]");
            sb.AppendLine("#endif");
            sb.AppendLine("        [global::UnityEngine.RuntimeInitializeOnLoadMethod(global::UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]");
        }
        else
        {
            sb.AppendLine("#if NET5_0_OR_GREATER");
            sb.AppendLine("        [System.Runtime.CompilerServices.ModuleInitializerAttribute]");
            sb.AppendLine("#else");
            sb.AppendLine("        // Initialization must be invoked explicitly.");
            sb.AppendLine($"        // Call from the application entry point: {registryFqn}.Initialize()");
            sb.AppendLine("#endif");
        }
        sb.AppendLine("        public static void Initialize()");
        sb.AppendLine("        {");

        foreach (var info in autoInfos) sb.Append(info.Registration);
        foreach (var info in manualInfos) sb.Append(info.Registration);

        sb.AppendLine("        }");
        sb.AppendLine();

        sb.AppendLine("    }");
        if (hasNs) sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>Obtains the type with attribute for the supplied input.</summary>
    /// <param name="context">The context used for analysis, source production, or initialization.</param>
    /// <param name="metadataName">The metadata name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static INamedTypeSymbol? GetTypeWithAttribute(
        GeneratorSyntaxContext context,
        string metadataName)
    {
        if (context.Node is not TypeDeclarationSyntax typeDeclaration)
            return null;

        var symbol = context.SemanticModel.GetDeclaredSymbol(typeDeclaration) as INamedTypeSymbol;
        if (symbol == null)
            return null;

        foreach (var attribute in symbol.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                == "global::" + metadataName)
                return symbol;
        }

        return null;
    }

    /// <summary>Performs the safe file name step for the current operation.</summary>
    /// <param name="fqn">The fqn used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string SafeFileName(string fqn)
        => fqn.Replace("global::", "").Replace(".", "_").Replace("<", "_").Replace(">", "_");

    /// <summary>Performs the sanitize id step for the current operation.</summary>
    /// <param name="name">The name used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static string SanitizeId(string name)
    {
        if (string.IsNullOrEmpty(name)) return "Unknown";
        var sb = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i == 0) sb.Append(char.IsLetter(c) || c == '_' ? c : char.IsDigit(c) ? '_' : '_');
            else sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');
        }
        return sb.ToString();
    }

}
