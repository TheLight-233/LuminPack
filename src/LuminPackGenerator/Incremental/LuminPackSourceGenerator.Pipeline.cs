using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

public sealed partial class LuminPackSourceGenerator
{
    /// <summary>Freezes semantic outputs before source-production equality checks.</summary>
    /// <param name="context">The generator pipeline registration context.</param>
    /// <param name="types">Attributed types discovered by the legacy discovery stage.</param>
    /// <param name="metadata">Value-compared project generation settings.</param>
    /// <param name="mode">The project-selected generation tier.</param>
    private static void InitializeTypePipeline(IncrementalGeneratorInitializationContext context,
        IncrementalValuesProvider<INamedTypeSymbol> types, IncrementalValueProvider<MetaInfo> metadata,
        IncrementalValueProvider<LuminPackGenerationTier> mode)
    {
        var cache = new TypeEmissionCache();
        var inputs = types.Combine(context.CompilationProvider).Combine(metadata).Combine(mode)
            .Select((input, _) =>
            {
                var symbol = input.Left.Left.Left;
                var compilation = input.Left.Left.Right;
                var meta = input.Left.Right;
                var tier = (LuminPackGenerationTier)Math.Max((int)input.Right, (int)LuminPackGenerationTierResolver.FromAssembly(compilation));
                return cache.PrepareInput(symbol, compilation, meta, tier);
            })
            .WithTrackingName("LuminPack.Incremental.TypeInput");
        var emissions = inputs.Select(static (input, _) =>
            {
                Interlocked.Increment(ref IncrementalGenerationMetrics.PackableAnalysisTransforms);
                return input.Evaluate(AnalyzeAndRender);
            })
            .WithTrackingName("LuminPack.Incremental.TypeEmission");

        context.RegisterSourceOutput(emissions, static (output, value) =>
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.PackableOutputCallbacks);
            try
            {
                if (!string.IsNullOrEmpty(value.Extension)) output.AddSource(value.HintName + ".Extension.g.cs", value.Extension);
                if (!string.IsNullOrEmpty(value.Union)) output.AddSource(value.HintName + ".UnionDispatch.g.cs", value.Union);
            }
            catch (Exception exception)
            {
                output.ReportDiagnostic(Diagnostic.Create(DiagnosticDescriptors.GeneratorFailure, Location.None, exception.GetType().FullName + ": " + exception.Message));
            }
        });

        // Exact weak tree handles let stable diagnostics remain independent of Compilation changes.
        var diagnostics = emissions.Select(static (value, _) => value.Diagnostics).Where(static values => !values.IsDefaultOrEmpty);
        context.RegisterSourceOutput(diagnostics, static (output, values) =>
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.PackableDiagnosticCallbacks);
            foreach (var diagnostic in values) output.ReportDiagnostic(diagnostic.MaterializeSource());
        });

        var layoutGroups = emissions.Select(static (value, _) => value.Layouts).WithTrackingName("LuminPack.Incremental.LayoutProjection");
        var layoutCache = new PartitionedSourceCache();
        var layouts = layoutGroups.Collect().Select((groups, _) => BuildLayoutFiles(groups, layoutCache))
            .WithTrackingName("LuminPack.Incremental.LayoutBatch");
        var layoutFiles = layouts.SelectMany(static (result, _) => result.Files).WithTrackingName("LuminPack.Incremental.LayoutFiles");
        context.RegisterSourceOutput(layoutFiles, static (output, file) =>
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.LayoutFileOutputCallbacks);
            output.AddSource(file.Name, file.Text);
        });
        context.RegisterSourceOutput(layouts.SelectMany(static (result, _) => result.Diagnostics), static (output, diagnostic) =>
        {
            output.ReportDiagnostic(diagnostic.MaterializeSource());
        });
    }

    /// <summary>Runs legacy semantic analysis and emitters once for a changed type.</summary>
    /// <param name="symbol">The current source type.</param>
    /// <param name="compilation">The compilation containing current symbols.</param>
    /// <param name="metadata">The current generation settings.</param>
    /// <param name="tier">The effective project generation tier.</param>
    /// <returns>The frozen source, diagnostic, and layout contributions.</returns>
    private static FrozenTypeEmission AnalyzeAndRender(INamedTypeSymbol symbol, Compilation compilation, MetaInfo metadata, LuminPackGenerationTier tier)
    {
        var data = CreateLuminDataInfo(symbol, compilation, metadata);
        var diagnostics = new List<Diagnostic>(data.Diagnostics);
        string name = data.classFullName ?? symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (name.StartsWith("global::", StringComparison.Ordinal)) name = name.Substring(8);
        name = name.Replace("<", "_").Replace('>', '_');
        string className = LuminPackExtensionGenerator.GetExtensionClassNameFor(compilation);
        var emptyLayouts = new FrozenLayoutGroup(className, metadata.ParseOptions, Array.Empty<FrozenLayoutFragment>(), null);
        if (data.Diagnostics.Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) || data.generatorType == GeneratorType.Custom ||
            tier == LuminPackGenerationTier.Minimal && !ReachabilityAnalysisCache.GetOrCreate(compilation, tier).ShouldEmitPackable(data.TypeSymbol))
            return new FrozenTypeEmission(name, string.Empty, string.Empty, emptyLayouts, diagnostics);

        string extension = string.Empty;
        string union = string.Empty;
        try
        {
            extension = LuminPackExtensionGenerator.CodeGenerator(data, metadata, compilation);
            union = data.isUnion && !data.isValueType ? LuminPackUnionDispatchCodeGenerator.Generate(data, compilation, metadata) : string.Empty;
            if (!string.IsNullOrEmpty(extension))
            {
                extension = GeneratedSourceDocumentation.Complete(extension, metadata.ParseOptions);
                if (!string.IsNullOrEmpty(union)) union = GeneratedSourceDocumentation.Complete(union, metadata.ParseOptions);
            }
            else union = string.Empty;
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
        return new FrozenTypeEmission(name, extension, union, layouts, diagnostics);
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
                if (emitted.Add(fragment.Key)) selected.Add((group, fragment));
        }
        foreach (var item in selected.OrderBy(item => item.Fragment.Key, StringComparer.Ordinal))
        {
            var file = cache.Wrap("LuminPack.LocalLayouts", item.Fragment.Key, item.Group.ClassName, item.Fragment.Text, string.Empty,
                () => GeneratedSourceDocumentation.Complete(LuminPackLocalLayoutGenerator.Wrap(item.Fragment.Text, item.Group.ClassName)
                    .Replace("one dedicated file for the whole assembly", "shared declarations across assembly partitions"), item.Group.Options), item.Group.Options);
            result.AddSource(file.Name, file.Text);
        }
        return result.Freeze();
    }
}
