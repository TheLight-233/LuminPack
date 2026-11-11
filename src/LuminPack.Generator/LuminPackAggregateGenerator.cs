using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Provides JSON facade and formatter output for hosts that manually pair the original generator types.</summary>
public sealed class LuminPackAggregateGenerator : IIncrementalGenerator
{
    /// <summary>Registers both publishers that complement the binary and shared source generator.</summary>
    /// <param name="context">The incremental initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => LuminPackSourceGenerator.InitializePipelines(context, GeneratorOutput.Json | GeneratorOutput.Formatter);
}
