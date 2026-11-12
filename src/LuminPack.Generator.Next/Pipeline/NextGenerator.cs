using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Pipeline;

/// <summary>Generates binary, JSON and formatter files through one shared incremental pipeline.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class NextGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) => NextPipeline.Initialize(context);
}

/// <summary>The independent source generator publications.</summary>
internal enum GenerationKind
{
    Binary,
    Json,
    Formatter,
}
