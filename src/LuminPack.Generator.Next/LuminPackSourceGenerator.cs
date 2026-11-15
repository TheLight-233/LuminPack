using LuminPack.SourceGenerator.Next.Pipeline;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Generates binary, JSON and formatter files through one shared incremental pipeline.</summary>
[Generator(LanguageNames.CSharp)]
public sealed class LuminPackSourceGenerator : IIncrementalGenerator
{
    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context) => NextPipeline.Initialize(context);
}
