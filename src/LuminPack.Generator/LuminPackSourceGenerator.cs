using System;
using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace LuminPack.SourceGenerator
{
    /// <summary>Emits the binary serializer facade and shared assembly support.</summary>
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
            /// <param name="types">The consuming compilation's explicitly marked type index.</param>
            /// <param name="compilation">The current compilation used for complete referenced storage views.</param>
            internal AnalysisState(ISymbol mainSymbol, MetaInfo metadata, CompilationTypeAnalysis types, Compilation compilation)
            {
                MainSymbol = mainSymbol;
                Location = mainSymbol.Locations.FirstOrDefault();
                Metadata = metadata;
                Types = types;
                Compilation = compilation;
                CurrentOptions = CompiledOptions.Resolve(mainSymbol, compilation);
            }

            internal ISymbol CurrentSymbol;
            internal readonly ISymbol MainSymbol;
            internal readonly Location Location;
            internal readonly MetaInfo Metadata;
            internal readonly CompilationTypeAnalysis Types;
            internal readonly Compilation Compilation;
            internal CompiledOptions CurrentOptions;
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
            => InitializePipelines(context, GeneratorOutput.Binary);

        /// <summary>Registers shared analysis and the requested source publishers.</summary>
        /// <param name="context">The incremental initialization context.</param>
        /// <param name="outputs">The source groups owned by this publisher.</param>
        internal static void InitializePipelines(IncrementalGeneratorInitializationContext context, GeneratorOutput outputs)
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
                }).WithComparer(MetaInfoValueComparer.Instance).WithTrackingName("LuminPack.LuminPackable.0_MetaInfo");


            var types = LuminPackFormatterGenerator.InitializeTypePipeline(context, metaInfo, outputs);
            InitializeAggregatePipeline(context, metaInfo, outputs, types);
        }
    }
}
