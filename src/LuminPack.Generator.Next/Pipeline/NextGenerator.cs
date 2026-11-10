using System;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering;
using LuminPack.SourceGenerator.Next.Rendering.Binary;
using LuminPack.SourceGenerator.Next.Rendering.Json;
using LuminPack.SourceGenerator.Next.Rendering.Union;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator.Next.Pipeline;

/// <summary>The Next generator: symbols enter, normalized models come out, and each payload kind owns one renderer.</summary>
/// <remarks>The pipeline has three boundaries. The analysis boundary (<see cref="TypeModelFactory"/>) is the only place that
/// sees Roslyn. The model boundary holds immutable, value-equal facts. The rendering boundary produces the final source through
/// one writer and one layout pass. No legacy analysis type or emitter text participates.</remarks>
[Generator(LanguageNames.CSharp)]
public sealed class NextGenerator : IIncrementalGenerator
{
    private const string PackableAttribute = "LuminPack.Attribute.LuminPackableAttribute";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var symbols = context.SyntaxProvider.ForAttributeWithMetadataName(PackableAttribute,
                static (node, _) => node is TypeDeclarationSyntax,
                static (attributeContext, _) => (INamedTypeSymbol)attributeContext.TargetSymbol)
            .Where(static symbol => symbol != null);
        var models = symbols.Combine(context.CompilationProvider).Select(static (pair, _) =>
        {
            GeneratorOptions options = GeneratorOptionsFactory.From(pair.Right);
            return new ModeledPayload(TypeModelFactory.TryCreate(pair.Left, pair.Right, options), options);
        });
        context.RegisterSourceOutput(models, static (production, payload) =>
        {
            if (payload.Result.Model is not { } model || !payload.Result.IsComplete)
                return;
            var renderContext = new RenderContext(payload.Options, AliasTable.For(payload.Options.AvailableAliases));
            Emit(production, model, BinaryRenderer.Instance, renderContext, string.Empty);
            Emit(production, model, JsonRenderer.Instance, renderContext, ".Json");
            if (model is UnionModel)
                Emit(production, model, UnionRenderer.Instance, renderContext, ".Union");
        });
    }

    private static void Emit(SourceProductionContext production, TypeModel model, IRenderer renderer, RenderContext context, string suffix)
    {
        string source = renderer.Render(model, context);
        if (source.Length == 0)
            return;
        production.AddSource(model.Identity.DisplayName.Replace('<', '_').Replace('>', '_').Replace(',', '_').Replace(' ', '_') + suffix + ".g.cs", source);
    }

    private readonly struct ModeledPayload
    {
        internal ModeledPayload(ModelResult result, GeneratorOptions options)
        {
            Result = result;
            Options = options;
        }

        internal ModelResult Result { get; }

        internal GeneratorOptions Options { get; }
    }
}
