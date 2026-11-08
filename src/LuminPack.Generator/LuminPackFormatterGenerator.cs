using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Emits binary and JSON formatters, their dispatch, and their storage declarations.</summary>
[Generator(LanguageNames.CSharp)]
public sealed partial class LuminPackFormatterGenerator : IIncrementalGenerator
{
    /// <summary>Registers the formatter output pipeline with Roslyn.</summary>
    /// <param name="context">The incremental initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => LuminPackSourceGenerator.InitializePipelines(context, GeneratorOutput.Formatter);
}
