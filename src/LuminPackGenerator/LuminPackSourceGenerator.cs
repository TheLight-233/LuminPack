using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator
{
    /// <summary>Discovers packable types and emits formatter extensions, union dispatch, and shared layout mirrors.</summary>
    [Generator(LanguageNames.CSharp)]
    public sealed partial class LuminPackSourceGenerator : IIncrementalGenerator
    {
        /// <summary>Stores the lumin packable attribute.</summary>
        public const string LUMIN_PACKABLE_ATTRIBUTE = "LuminPack.Attribute.LuminPackableAttribute";
        /// <summary>Stores the lumin union attribute.</summary>
        public const string LUMIN_UNION_ATTRIBUTE = "LuminPack.Attribute.LuminPackUnionAttribute";
        /// <summary>Stores the lumin generated namespace.</summary>
        public const string LUMIN_GENERATED_NAMESPACE = "LuminPack.Generated";
        /// <summary>Stores the lumin registers namespace.</summary>
        public const string LUMIN_REGISTERS_NAMESPACE = "LuminPackRegisters";

        [ThreadStatic]
        private static AnalysisState _analysisState;

        /// <summary>Provides analysis state functionality.</summary>
        private sealed class AnalysisState
        {
            /// <summary>Initializes a new instance of <c>AnalysisState</c>.</summary>
            /// <param name="mainSymbol">The main symbol used by this operation.</param>
            /// <param name="metadata">The language, framework, and generation settings.</param>
            internal AnalysisState(ISymbol mainSymbol, MetaInfo metadata)
            {
                MainSymbol = mainSymbol;
                Location = mainSymbol.Locations.FirstOrDefault();
                Metadata = metadata;
            }

            internal ISymbol CurrentSymbol;
            internal readonly ISymbol MainSymbol;
            internal readonly Location Location;
            internal readonly MetaInfo Metadata;
            internal readonly HashSet<INamedTypeSymbol> ProcessedTypes =
                new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        }

        /// <summary>Gets or sets the current symbol.</summary>
        private static ISymbol _currentSymbol
        {
            get => _analysisState.CurrentSymbol;
            set => _analysisState.CurrentSymbol = value;
        }

        /// <summary>Gets the main symbol.</summary>
        private static ISymbol _mainSymbol => _analysisState.MainSymbol;
        /// <summary>Gets the location.</summary>
        private static Location _location => _analysisState.Location;
        /// <summary>Gets the metadata.</summary>
        private static MetaInfo _metadata => _analysisState.Metadata;
        /// <summary>Gets the processed types.</summary>
        private static HashSet<INamedTypeSymbol> ProcessedTypes => _analysisState.ProcessedTypes;

        /// <summary>Registers the incremental analysis and source production pipeline with Roslyn.</summary>
        /// <param name="context">The context used for analysis, source production, or initialization.</param>
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var parseOptionsInfo = context.ParseOptionsProvider.Select((parseOptions, _) =>
            {
                var csOptions = (CSharpParseOptions)parseOptions;
                var langVersion = csOptions.LanguageVersion;
                var net8 = csOptions.PreprocessorSymbolNames.Contains("NET8_0_OR_GREATER");
                var net9OrGreater = csOptions.PreprocessorSymbolNames.Contains("NET9_0_OR_GREATER");
                return (csOptions, langVersion, net8, net9OrGreater);
            }).WithTrackingName("LuminPack.LuminPackable.0_ParseOptionsProvider");

            var metaInfo = parseOptionsInfo
                .Combine(context.CompilationProvider)
                .Select((combined, _) =>
                {
                    var (parseInfo, compilation) = combined;
                    var (csOptions, langVersion, net8, net9OrGreater) = parseInfo;

                    // Read the unsafe setting from CompilationOptions.
                    var allowUnsafe = compilation.Options is CSharpCompilationOptions csharpOptions
                        ? csharpOptions.AllowUnsafe
                        : false;
                    var registerMode = LuminPackGenerationTierResolver.RegisterModeFromAssembly(compilation);

                    return new MetaInfo(csOptions, langVersion, net8, net9OrGreater, allowUnsafe, registerMode);
                }).WithTrackingName("LuminPack.LuminPackable.0_MetaInfo");


            var typeDeclarations = context.SyntaxProvider.CreateSyntaxProvider(
                static (node, _) => node
                    is TypeDeclarationSyntax
                    or StructDeclarationSyntax
                    or InterfaceDeclarationSyntax
                    or RecordDeclarationSyntax,
                static (context, _) =>
                {
                    return GetTypeWithAttribute(context, LUMIN_PACKABLE_ATTRIBUTE);
                })
                .Where(static symbol => symbol != null)
                .Select(static (symbol, _) => symbol!)
                .WithTrackingName("LuminPack.LuminPackable.1_ForAttributeLuminPackableAttribute");

            var generationMode = context.AnalyzerConfigOptionsProvider
                .Select(static (options, _) =>
                {
                    options.GlobalOptions.TryGetValue("build_property.LuminPackGenerationMode", out string? value);
                    return LuminPackGenerationTierResolver.FromProperty(value);
                })
                .WithTrackingName("LuminPack.LuminPackable.0_GenerationMode");

            var provider = typeDeclarations
                .Combine(context.CompilationProvider)
                .Combine(metaInfo)
                .Combine(generationMode)
                .Select((source, _) =>
                {
                    var symbol = source.Left.Left.Left;
                    var compilation = source.Left.Left.Right;
                    var metadata = source.Left.Right;
                    var tier = source.Right;
                    var dataInfo = CreateLuminDataInfo(symbol, compilation, metadata);
                    return ((dataInfo, compilation), (metadata, tier));
                })
                .WithTrackingName("LuminPack.LuminPackable.2_LuminPackCombined");

            context.RegisterSourceOutput(provider, static (context, source) =>
            {
                try
                {
                    var dataInfo = source.Item1.Item1;
                    if (dataInfo.Diagnostics.Length != 0)
                    {
                        foreach (var diagnostic in dataInfo.Diagnostics)
                        {
                            context.ReportDiagnostic(diagnostic);
                        }

                        if (dataInfo.Diagnostics.Any(static diagnostic =>
                                diagnostic.Severity == DiagnosticSeverity.Error))
                        {
                            return;
                        }
                    }

                    var compilation = source.Item1.Item2;
                    var metaInfo = source.Item2.Item1;
                    var propertyTier = source.Item2.Item2;

                    // Custom types emit no extension methods; the aggregate pipeline in
                    // LuminMapSourceGenerator validates them and emits registration code.
                    if (dataInfo.generatorType == GeneratorType.Custom)
                    {
                        return;
                    }

                    LuminPackGenerationTier assemblyTier = LuminPackGenerationTierResolver.FromAssembly(compilation);
                    LuminPackGenerationTier effectiveTier = (LuminPackGenerationTier)Math.Max((int)propertyTier, (int)assemblyTier);

                    if (effectiveTier == LuminPackGenerationTier.Minimal &&
                        !ReachabilityAnalysisCache.GetOrCreate(compilation, effectiveTier).ShouldEmitPackable(dataInfo.TypeSymbol))
                    {
                        // Minimal tier: skip [LuminPackable] types that are never passed to a
                        // serializer call site (nor reachable from one).
                        return;
                    }

                    var extension = LuminPackExtensionGenerator.CodeGenerator(dataInfo, metaInfo, compilation);
                    var unionDispatch = dataInfo.isUnion && !dataInfo.isValueType
                        ? LuminPackUnionDispatchCodeGenerator.Generate(dataInfo, compilation, metaInfo)
                        : string.Empty;
                    if (string.IsNullOrEmpty(extension)) return;

                    var name = dataInfo.classFullName;
                    if (name.StartsWith("global::"))
                    {
                        name = name.Substring(8);
                    }
                    name = name.Replace("<", "_").Replace('>', '_');

                    context.AddSource($"{name}.Extension.g.cs", GeneratedSourceDocumentation.Complete(extension, metaInfo.ParseOptions));
                    if (!string.IsNullOrEmpty(unionDispatch))
                        context.AddSource($"{name}.UnionDispatch.g.cs", GeneratedSourceDocumentation.Complete(unionDispatch, metaInfo.ParseOptions));


                }
                catch (Exception ex)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.GeneratorFailure,
                        Location.None,
                        ex.GetType().FullName + ": " + ex.Message));
                }
            });

            // Local layout mirrors are an assembly-wide concern, so they are emitted into one
            // dedicated file instead of being scattered across the per-type extension files.
            // Collecting every analyzed type lets a mirror be emitted exactly once and be shared
            // by every formatter that needs it, which removes the previous nesting-depth ceiling.
            context.RegisterSourceOutput(provider.Collect(), static (context, sources) =>
            {
                try
                {
                    var body = new StringBuilder();
                    var registry = new LuminPackLocalLayoutGenerator.LocalLayoutRegistry();
                    Compilation compilation = null;

                    foreach (var source in sources)
                    {
                        var dataInfo = source.Item1.Item1;
                        if (dataInfo.TypeSymbol is null)
                        {
                            continue;
                        }

                        if (dataInfo.Diagnostics.Length != 0 &&
                            dataInfo.Diagnostics.Any(static diagnostic =>
                                diagnostic.Severity == DiagnosticSeverity.Error))
                        {
                            continue;
                        }

                        // Custom types emit no extension methods and therefore never reach a Local.
                        if (dataInfo.generatorType == GeneratorType.Custom)
                        {
                            continue;
                        }

                        var innerCompilation = source.Item1.Item2;
                        var tier = source.Item2.Item2;
                        LuminPackGenerationTier assemblyTier = LuminPackGenerationTierResolver.FromAssembly(innerCompilation);
                        LuminPackGenerationTier effectiveTier = (LuminPackGenerationTier)Math.Max((int)tier, (int)assemblyTier);

                        if (effectiveTier == LuminPackGenerationTier.Minimal &&
                            !ReachabilityAnalysisCache.GetOrCreate(innerCompilation, effectiveTier).ShouldEmitPackable(dataInfo.TypeSymbol))
                        {
                            continue;
                        }

                        compilation = innerCompilation;

                        LuminPackLocalLayoutGenerator.AppendRootLocalClass(body, registry, dataInfo);
                        LuminPackLocalLayoutGenerator.AppendNestedLocalClasses(body, registry, dataInfo.fields);
                        LuminPackLocalLayoutGenerator.AppendLocalMemberAccessors(body, registry, dataInfo.fields);
                    }

                    if (compilation is null || body.Length == 0)
                    {
                        return;
                    }

                    string wrapped = LuminPackLocalLayoutGenerator.Wrap(
                        body.ToString(),
                        LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation));
                    if (!string.IsNullOrEmpty(wrapped))
                    {
                        context.AddSource("LuminPack.LocalLayouts.g.cs", GeneratedSourceDocumentation.Complete(wrapped, sources[0].Item2.Item1.ParseOptions));
                    }
                }
                catch (Exception ex)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.GeneratorFailure,
                        Location.None,
                        ex.GetType().FullName + ": " + ex.Message));
                }
            });
        }
    }
}
