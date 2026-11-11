using System;
using System.Collections.Generic;
using LuminPack.SourceGenerator.Next.Analysis;
using LuminPack.SourceGenerator.Next.Diagnostics;
using LuminPack.SourceGenerator.Next.Model;
using LuminPack.SourceGenerator.Next.Rendering;
using LuminPack.SourceGenerator.Next.Rendering.Binary;
using LuminPack.SourceGenerator.Next.Rendering.Emitters;
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
            static (attributeContext, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                GeneratorOptions options = GeneratorOptionsFactory.From(attributeContext.SemanticModel.Compilation);
                var symbol = (INamedTypeSymbol)attributeContext.TargetSymbol;
                Compilation compilation = attributeContext.SemanticModel.Compilation;
                try
                {
                    DiagnosticInfo[] diagnostics = TypeModelFactory.Diagnose(symbol, compilation, options);
                    bool errors = DiagnosticDescriptors.HasErrors(diagnostics) ||
                        DiagnosticDescriptors.HasErrors(TypeModelFactory.DiagnoseAssembly(compilation, options));
                    cancellationToken.ThrowIfCancellationRequested();
                    ModelResult result = errors ? ModelResult.Unsupported("invalid serialization declaration") :
                        TypeModelFactory.TryCreate(symbol, compilation, options);
                    return new ModeledPayload(result, options, diagnostics);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    DiagnosticInfo diagnostic = DiagnosticInfo.Capture(DiagnosticDescriptors.GeneratorFailure.Id,
                        symbol.Locations.Length == 0 ? null : symbol.Locations[0], new[] { error.GetType().FullName + ": " + error.Message });
                    return new ModeledPayload(ModelResult.Unsupported("declaration analysis failed"), options, new[] { diagnostic });
                }
            })
            .WithTrackingName("LuminPack.Next.PayloadModels");
        var assemblyDiagnostics = context.CompilationProvider.Select(static (compilation, _) =>
            TypeModelFactory.DiagnoseAssembly(compilation, GeneratorOptionsFactory.From(compilation)))
            .WithComparer(DiagnosticInfoComparer.Instance);
        context.RegisterSourceOutput(assemblyDiagnostics.Combine(context.CompilationProvider), static (production, input) =>
        {
            foreach (DiagnosticInfo diagnostic in input.Left)
                production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic, input.Right));
        });
        var declarationDiagnostics = models.Select(static (payload, _) => payload.Diagnostics)
            .WithComparer(DiagnosticInfoComparer.Instance);
        context.RegisterSourceOutput(declarationDiagnostics.Combine(context.CompilationProvider), static (production, input) =>
        {
            foreach (DiagnosticInfo diagnostic in input.Left)
                production.ReportDiagnostic(DiagnosticDescriptors.Create(diagnostic, input.Right));
        });
        context.RegisterSourceOutput(models, static (production, payload) =>
        {
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
            catch (Exception error) when (error is not OperationCanceledException)
            {
                production.ReportDiagnostic(DiagnosticDescriptors.Create(
                    DiagnosticInfo.Unlocated(DiagnosticDescriptors.GeneratorFailure.Id, error.Message)));
            }
        });
        var collected = models.Collect();
        var profiles = collected.Select(static (payloads, _) =>
        {
            var retained = new List<TypeModel>();
            foreach (ModeledPayload payload in payloads)
                if (payload.Result.IsComplete && payload.Result.Model is { } model)
                    retained.Add(model);
            return ProfileSource.Render(retained);
        });
        context.RegisterSourceOutput(profiles, static (production, source) =>
        {
            if (source.Length != 0)
                production.AddSource("LuminPack.Profiles.g.cs", source);
        });
        // The shared formatter owners are collected once. Sorted requirements are order-independent, so the same complete payloads
        // publish the same files and an unchanged set reuses its cached output across edits.
        var sharedFormatters = collected
            .Select(static (payloads, _) =>
            {
                var retained = new List<TypeModel>();
                GeneratorOptions? options = null;
                foreach (ModeledPayload payload in payloads)
                    if (payload.Result.Model is { } model && payload.Result.IsComplete)
                    {
                        retained.Add(model);
                        options ??= payload.Options;
                    }
                return options == null ? SharedFormatterSet.Empty : SharedFormatterSet.Collect(retained, options);
            })
            .WithTrackingName("LuminPack.Next.SharedFormatters");
        context.RegisterSourceOutput(sharedFormatters, static (production, set) =>
        {
            try
            {
                SharedFormatterPublisher.Publish(production, set, GeneratedOperations.All);
            }
            catch (Exception error) when (error is not OperationCanceledException)
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
