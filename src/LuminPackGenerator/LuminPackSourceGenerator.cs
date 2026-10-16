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
            /// <param name="includeExternalProperties">Whether public read/write properties are included for an external root.</param>
            internal AnalysisState(ISymbol mainSymbol, MetaInfo metadata, bool includeExternalProperties)
            {
                MainSymbol = mainSymbol;
                Location = mainSymbol.Locations.FirstOrDefault();
                Metadata = metadata;
                IncludeExternalProperties = includeExternalProperties;
            }

            internal ISymbol CurrentSymbol;
            internal readonly ISymbol MainSymbol;
            internal readonly Location Location;
            internal readonly MetaInfo Metadata;
            internal readonly bool IncludeExternalProperties;
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
                }).WithComparer(MetaInfoValueComparer.Instance).WithTrackingName("LuminPack.LuminPackable.0_MetaInfo");


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

            InitializeTypePipeline(context, typeDeclarations, metaInfo, generationMode);
        }
    }
}
