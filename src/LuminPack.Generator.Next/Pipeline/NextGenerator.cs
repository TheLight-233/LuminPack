using System;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering;
using LuminPack.SourceGenerator.Next.Rendering.Binary;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
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
    /// <remarks>The provider is keyed per attributed type: editing one declaration re-runs that payload's transform and its
    /// source output, while unrelated payloads keep their cached model. Project facts are cached per compilation and compared
    /// by value, so an edit that leaves them unchanged does not invalidate every model.</remarks>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider.ForAttributeWithMetadataName(PackableAttribute,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, _) =>
            {
                GeneratorOptions options = GeneratorOptionsFactory.From(attributeContext.SemanticModel.Compilation);
                var symbol = (INamedTypeSymbol)attributeContext.TargetSymbol;
                ModelResult result = TypeModelFactory.TryCreate(symbol, attributeContext.SemanticModel.Compilation, options);
                return new ModeledPayload(result, options, TypeModelFactory.Diagnose(symbol, result.Model));
            })
            .WithTrackingName("LuminPack.Next.PayloadModels");
        context.RegisterSourceOutput(models, static (production, payload) =>
        {
            foreach (DiagnosticInfo diagnostic in payload.Diagnostics)
                production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic));
            if (payload.Result.Model is not { } model || !payload.Result.IsComplete)
                return;
            try
            {
                var renderContext = new RenderContext(payload.Options, AliasTable.For(payload.Options.AvailableAliases));
                if (model is UnionModel)
                {
                    Emit(production, model, renderContext, UnionRenderer.Instance.RenderBinary(model, renderContext), string.Empty);
                    Emit(production, model, renderContext, UnionRenderer.Instance.RenderJson(model, renderContext), ".Json");
                    return;
                }
                Emit(production, model, renderContext, BinaryRenderer.Instance.Render(model, renderContext), string.Empty);
                Emit(production, model, renderContext, JsonRenderer.Instance.Render(model, renderContext), ".Json");
            }
            catch (Exception error)
            {
                production.ReportDiagnostic(DiagnosticDescriptors.Create(
                    DiagnosticInfo.Unlocated(DiagnosticDescriptors.GeneratorFailure.Id, error.Message)));
            }
        });
        // The shared formatter owners are collected once: the flag set is order-independent, so the same complete payloads
        // publish the same files and an unchanged set reuses its cached output across edits.
        var sharedFormatters = models.Collect()
            .Select(static (payloads, _) =>
            {
                SharedFormatterSet set = SharedFormatterSet.Empty;
                foreach (ModeledPayload payload in payloads)
                    if (payload.Result.Model is { } model && payload.Result.IsComplete)
                        set = set.Include(model, payload.Options);
                return set;
            })
            .WithTrackingName("LuminPack.Next.SharedFormatters");
        context.RegisterSourceOutput(sharedFormatters, static (production, set) =>
        {
            try
            {
                SharedFormatterPublisher.Publish(production, set);
            }
            catch (Exception error)
            {
                production.ReportDiagnostic(DiagnosticDescriptors.Create(
                    DiagnosticInfo.Unlocated(DiagnosticDescriptors.GeneratorFailure.Id, error.Message)));
            }
        });
    }

    private static void Emit(SourceProductionContext production, TypeModel model, RenderContext context, string source, string suffix)
    {
        if (source.Length == 0)
            return;
        production.AddSource(model.Identity.DisplayName.Replace('<', '_').Replace('>', '_').Replace(',', '_').Replace(' ', '_') + suffix + ".g.cs", source);
    }

    private readonly struct ModeledPayload : IEquatable<ModeledPayload>
    {
        internal ModeledPayload(ModelResult result, GeneratorOptions options, DiagnosticInfo[] diagnostics)
        {
            Diagnostics = diagnostics;
            Result = result;
            Options = options;
        }

        internal ModelResult Result { get; }

        internal GeneratorOptions Options { get; }

        internal DiagnosticInfo[] Diagnostics { get; }

        public bool Equals(ModeledPayload other) => Result.Equals(other.Result) && Options.Equals(other.Options) &&
            SameDiagnostics(Diagnostics, other.Diagnostics);

        public override bool Equals(object? obj) => obj is ModeledPayload other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Result.GetHashCode() ^ Options.GetHashCode();
            foreach (DiagnosticInfo diagnostic in Diagnostics)
                hash ^= diagnostic.GetHashCode();
            return hash;
        }

        private static bool SameDiagnostics(DiagnosticInfo[] left, DiagnosticInfo[] right)
        {
            if (left.Length != right.Length)
                return false;
            for (int index = 0; index < left.Length; index++)
                if (!left[index].Equals(right[index]))
                    return false;
            return true;
        }
    }
}
