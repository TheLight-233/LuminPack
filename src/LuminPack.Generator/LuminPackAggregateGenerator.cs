using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Provides the JSON publisher for hosts that manually pair the original generator types.</summary>
public sealed class LuminPackAggregateGenerator : IIncrementalGenerator
{
    /// <summary>Registers the JSON pipeline alongside the binary and shared source generator.</summary>
    /// <param name="context">The incremental initialization context.</param>
    public void Initialize(IncrementalGeneratorInitializationContext context)
        => new LuminPackJsonGenerator().Initialize(context);
}
