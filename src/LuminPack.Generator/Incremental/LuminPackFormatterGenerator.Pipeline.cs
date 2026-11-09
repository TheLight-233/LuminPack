using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

public sealed partial class LuminPackFormatterGenerator
{
    /// <summary>Freezes semantic outputs before source-production equality checks.</summary>
    /// <param name="context">The generator pipeline registration context.</param>
    /// <param name="metadata">Value-compared project generation settings.</param>
    /// <param name="outputs">The source groups the publishing generator owns.</param>
    /// <returns>The completed type contributions before publication.</returns>
    internal static IncrementalValuesProvider<FrozenTypeEmission> InitializeTypePipeline(IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<MetaInfo> metadata, GeneratorOutput outputs)
    {
        var cache = new TypeEmissionCache();
        var operations = new BinaryOperationPlanCache();
        var aliases = GeneratedSourceLayout.AvailableAliases(context);
        // Roslyn's attribute index binds only declarations whose attribute names can match, instead of every attributed type.
        var types = context.SyntaxProvider.ForAttributeWithMetadataName(LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE,
            static (node, _) => node is TypeDeclarationSyntax,
            static (syntax, _) => syntax.TargetSymbol as INamedTypeSymbol)
            .Where(static symbol => symbol != null)
            .Select(static (symbol, _) => symbol!)
            .WithTrackingName("LuminPack.LuminPackable.1_ForAttributeLuminPackableAttribute");
        var inputs = types.Combine(context.CompilationProvider).Combine(metadata)
            .Select((input, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var symbol = input.Left.Left;
                var compilation = input.Left.Right;
                var meta = input.Right;
                return cache.PrepareInput(symbol, compilation, meta, LuminPackGenerationTier.Automatic);
            })
            .WithTrackingName("LuminPack.Incremental.TypeInput");
        // Each generator publishes its own part of the same rendering, which the first of them produces for the compilation.
        Func<INamedTypeSymbol, Compilation, MetaInfo, LuminPackGenerationTier, FrozenTypeEmission> analyze =
            (symbol, compilation, meta, tier) => SharedTypeEmissions.Get(compilation, symbol, meta, tier, outputs,
                () => AnalyzeAndRender(symbol, compilation, meta, tier, operations));
        // Roslyn runs the per-type step below on one thread. Changed types are analyzed here on several threads first;
        // each result is published to the type cache, and the per-type step then reads it in pipeline order.
        // The batch value never changes, so unchanged types stay cached.
        var analyzed = inputs.Collect().Select((all, cancellationToken) =>
            {
                var pending = all.Where(input => input.IsPending).ToArray();
                // The first type initializes the compilation-wide analyses (which parallelize internally) on this thread.
                ParallelWork.For(Math.Min(1, pending.Length), index => Prepare(pending[index]), cancellationToken);
                ParallelWork.For(pending.Length - Math.Min(1, pending.Length), index => Prepare(pending[index + 1]), cancellationToken);
                return true;
            })
            .WithTrackingName("LuminPack.Incremental.TypeAnalysisBatch");
        var emissions = inputs.Combine(analyzed).Select((pair, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref IncrementalGenerationMetrics.PackableAnalysisTransforms);
                return pair.Left.Evaluate(analyze);
            })
            .WithTrackingName("LuminPack.Incremental.TypeEmission");

        void Prepare(TypeEmissionInput input)
        {
            try
            {
                input.Evaluate(analyze);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The per-type step repeats a failed analysis and reports it exactly as before.
            }
        }

        if ((outputs & GeneratorOutput.Formatter) == 0)
            return emissions;

        // Exact weak tree handles let stable diagnostics remain independent of Compilation changes.
        var diagnostics = emissions.Select(static (value, _) => value.Diagnostics).Where(static values => !values.IsDefaultOrEmpty);
        context.RegisterSourceOutput(diagnostics, static (output, values) =>
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.PackableDiagnosticCallbacks);
            foreach (var diagnostic in values)
                output.ReportDiagnostic(diagnostic.MaterializeSource());
        });

        var layoutGroups = emissions.Select(static (value, _) => value.Layouts).WithTrackingName("LuminPack.Incremental.LayoutProjection");
        var layoutCache = new PartitionedSourceCache();
        var layouts = layoutGroups.Collect().Select((groups, _) => BuildLayoutFiles(groups, layoutCache))
            .WithTrackingName("LuminPack.Incremental.LayoutBatch");
        var layoutFiles = layouts.SelectMany(static (result, _) => result.Files).WithTrackingName("LuminPack.Incremental.LayoutFiles");
        context.RegisterSourceOutput(layoutFiles.Combine(aliases), static (output, input) =>
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.LayoutFileOutputCallbacks);
            output.AddSource(input.Left.Name, GeneratedSourceLayout.Apply(input.Left.Text, input.Right));
        });
        context.RegisterSourceOutput(layouts.SelectMany(static (result, _) => result.Diagnostics), static (output, diagnostic) =>
        {
            output.ReportDiagnostic(diagnostic.MaterializeSource());
        });
        return emissions;
    }

    /// <summary>Publishes the selected type roots and Union slots, one file per type and format.</summary>
    /// <param name="context">The incremental generator context.</param>
    /// <param name="emissions">The completed type contributions.</param>
    /// <param name="called">The operations retained by the consumer.</param>
    /// <param name="forwarding">The shared state forwarding decisions.</param>
    /// <param name="outputs">The source groups owned by this publisher.</param>
    internal static void PublishTypeFiles(IncrementalGeneratorInitializationContext context, IncrementalValuesProvider<FrozenTypeEmission> emissions,
        IncrementalValueProvider<GeneratedOperationUsage> called, IncrementalValueProvider<StateForwardingPlan> forwarding, GeneratorOutput outputs)
    {
        bool binary = (outputs & GeneratorOutput.Binary) != 0, json = (outputs & GeneratorOutput.Json) != 0;
        if (!binary && !json)
            return;

        var sources = emissions.Where(static value => !string.IsNullOrEmpty(value.Extension)).Combine(called)
            .SelectMany((pair, _) => TypeFiles(pair.Left, binary, json, outputs != GeneratorOutput.Binary && outputs != GeneratorOutput.Json,
                pair.Right.ForPayload(pair.Left.Payload)))
            .Combine(forwarding).Select(static (pair, _) => pair.Right.Apply(pair.Left));
        context.RegisterSourceOutput(sources.Combine(GeneratedSourceLayout.AvailableAliases(context)), static (output, input) =>
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.PackableOutputCallbacks);
            try
            {
                output.AddSource(input.Left.Name, GeneratedSourceLayout.Apply(input.Left.Text, input.Right));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GeneratorFailure, Location.None, exception.GetType().FullName + ": " + exception.Message));
            }
        });
    }

    /// <summary>Runs legacy semantic analysis and emitters once for a changed type.</summary>
    /// <param name="symbol">The current source type.</param>
    /// <param name="compilation">The compilation containing current symbols.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <param name="tier">The effective project generation tier.</param>
    /// <param name="operations">Shared dependency-guarded operation plans.</param>
    /// <returns>The frozen source, diagnostic, and layout contributions.</returns>
    private static FrozenTypeEmission AnalyzeAndRender(INamedTypeSymbol symbol, Compilation compilation, MetaInfo metadata, LuminPackGenerationTier tier, BinaryOperationPlanCache operations)
    {
        var data = LuminPackSourceGenerator.GetSerializationMetadata(symbol, compilation, metadata);
        var diagnostics = new List<Diagnostic>(data.Diagnostics);
        string name = data.classFullName ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (name.StartsWith("global::", StringComparison.Ordinal))
            name = name.Substring(8);
        name = PartitionedSourceCache.CreateHintName("LuminPackGen", name);
        name = name.Substring(0, name.Length - ".g.cs".Length);
        string className = LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation);
        var emptyLayouts = new FrozenLayoutGroup(className, metadata.ParseOptions, Array.Empty<FrozenLayoutFragment>(), null);
        if (data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) || data.generatorType == GeneratorType.Custom)
            return new FrozenTypeEmission(name, string.Empty, string.Empty, emptyLayouts, diagnostics);

        string extension = string.Empty;
        string union = string.Empty;
        try
        {
            var emittedTypes = ReachabilityAnalysisCache.GetOrCreate(compilation, tier);
            if (emittedTypes != null)
                CompilationTypeAnalysisCache.GetOrCreate(compilation).IndexReachableTypes(emittedTypes.EmittedTypes
                    .Where(type => !LuminPackExtensionGenerator.ContainsTypeParameter(type)));
            extension = LuminPackExtensionGenerator.CodeGenerator(data, metadata, compilation);
            extension = SerializerCoreProjection.Project(extension, compilation, metadata, operations);
            union = data.isUnion && !data.isValueType ? LuminPackUnionDispatchCodeGenerator.Generate(data, compilation, metadata,
                emittedTypes?.EmittedTypes) : string.Empty;
            if (!string.IsNullOrEmpty(union))
                union = SerializerCoreProjection.RewriteCalls(union, compilation, metadata);
            if (!string.IsNullOrEmpty(extension))
            {
                if (!string.IsNullOrEmpty(union))
                    union = GeneratedSourceDocumentation.Complete(union, metadata.ParseOptions);
            }
            else
                union = string.Empty;
        }
        catch (Exception exception)
        {
            diagnostics.Add(Diagnostic.Create(DiagnosticDescriptors.GeneratorFailure, Location.None, exception.GetType().FullName + ": " + exception.Message));
        }

        var layouts = emptyLayouts;
        try
        {
            var body = new StringBuilder();
            var positions = new List<(string Key, int Position)>();
            var registry = new LuminPackLocalLayoutGenerator.LocalLayoutRegistry { Registered = key => positions.Add((key, body.Length)) };
            LuminPackLocalLayoutGenerator.AppendRootLocalClass(body, registry, data);
            LuminPackLocalLayoutGenerator.AppendNestedLocalClasses(body, registry, data.fields);
            LuminPackLocalLayoutGenerator.AppendLocalMemberAccessors(body, registry, data.fields);
            string text = body.ToString();
            var fragments = new List<FrozenLayoutFragment>();
            for (int index = 0; index < positions.Count; index++)
            {
                var start = positions[index];
                int end = index + 1 < positions.Count ? positions[index + 1].Position : text.Length;
                fragments.Add(new FrozenLayoutFragment(start.Key, text.Substring(start.Position, end - start.Position)));
            }
            layouts = new FrozenLayoutGroup(className, metadata.ParseOptions, fragments, null);
        }
        catch (Exception exception)
        {
            layouts = new FrozenLayoutGroup(className, metadata.ParseOptions, Array.Empty<FrozenLayoutFragment>(), exception.GetType().FullName + ": " + exception.Message);
        }
        return new FrozenTypeEmission(name, extension, union, layouts, diagnostics, FormatterTypeName.Get(symbol));
    }

    /// <summary>Selects the binary or JSON part of a type's roots and Union slots as one file per format.</summary>
    /// <param name="value">The shared type emission.</param>
    /// <param name="binary">Whether the publishing generator owns binary code.</param>
    /// <param name="json">Whether the publishing generator owns JSON code.</param>
    /// <param name="combined">Whether the publisher also owns other output, such as the compatibility generator that publishes
    /// JSON and formatter code together; its JSON file then carries a role so it cannot share a formatter file's name.</param>
    /// <param name="called">The operations the project calls.</param>
    /// <returns>The files of the selected formats.</returns>
    private static IEnumerable<FrozenGeneratedSource> TypeFiles(FrozenTypeEmission value, bool binary, bool json, bool combined, GeneratedOperations called)
    {
        // The binary part also holds the helpers both formats share, so only its operations are filtered out.
        json &= (called & (GeneratedOperations.JsonWrite | GeneratedOperations.JsonRead)) != 0;
        var options = value.Layouts.Options;
        var roots = GeneratedFormatPartition.Get(value.Extension, options);
        var union = string.IsNullOrEmpty(value.Union) ? null : GeneratedFormatPartition.Get(value.Union, options);
        string name = value.HintName + ".g.cs";
        if (binary && GeneratedOperationFilter.Apply(GeneratedSourceMerge.Join(roots.Shared, union?.Shared, options), called, options) is { Length: > 0 } binaryText)
            yield return new FrozenGeneratedSource(name, binaryText);
        if (json && GeneratedOperationFilter.Apply(GeneratedSourceMerge.Join(roots.Json, union?.Json, options), called, options) is { Length: > 0 } jsonText)
            yield return new FrozenGeneratedSource(combined ? value.HintName + ".Json.g.cs" : name, jsonText);
    }

    /// <summary>Preserves first-registration layout selection while publishing declarations independently.</summary>
    /// <param name="groups">Type contributions in the original pipeline order.</param>
    /// <param name="cache">Stable complete-file contributions keyed by the original registry identity.</param>
    /// <returns>Independently comparable layout files or the existing failure diagnostic.</returns>
    private static FrozenAggregateResult BuildLayoutFiles(ImmutableArray<FrozenLayoutGroup> groups, PartitionedSourceCache cache)
    {
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var selected = new List<(FrozenLayoutGroup Group, FrozenLayoutFragment Fragment)>();
        var result = new AggregateResultBuilder();
        foreach (var group in groups)
        {
            if (group.Error != null)
            {
                result.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GeneratorFailure, Location.None, group.Error));
                return result.Freeze();
            }
            foreach (var fragment in group.Fragments)
                if (emitted.Add(fragment.Key))
                    selected.Add((group, fragment));
        }
        foreach (var item in selected.OrderBy(item => item.Fragment.Key, StringComparer.Ordinal))
        {
            var file = cache.Wrap("LuminPackGen.Layouts", item.Fragment.Key, item.Group.ClassName, item.Fragment.Text, string.Empty,
                () => GeneratedSourceDocumentation.Complete(LuminPackLocalLayoutGenerator.Wrap(item.Fragment.Text, item.Group.ClassName)
                    .Replace("one dedicated file for the whole assembly", "shared declarations across assembly partitions"), item.Group.Options), item.Group.Options);
            result.AddSource(file.Name, file.Text);
        }
        var frozen = result.Freeze();
        cache.Retain(frozen.Files.Select(file => file.Name));
        return frozen;
    }
}
