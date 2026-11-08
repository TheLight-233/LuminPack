using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Emits the JSON serializer facade for a consuming compilation.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class LuminPackJsonGenerator : IIncrementalGenerator
{
    /// <summary>Registers the JSON output pipeline with Roslyn.</summary>
    /// <param name="context">The incremental initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => LuminPackSourceGenerator.InitializePipelines(context, GeneratorOutput.Json);
}
