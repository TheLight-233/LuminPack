using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

public static partial class LuminPackExtensionGenerator
{
    /// <summary>Separates no-owner formatter bodies from generic dispatch and static registration.</summary>
    /// <param name="compilation">The current consuming compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="reachability">The formatter roots, or null for Full.</param>
    /// <param name="cache">Generator-instance type and dispatch contribution caches.</param>
    /// <returns>Independently comparable partial extension files.</returns>
    internal static ImmutableArray<FrozenGeneratedSource> GenerateInvocationFiles(Compilation compilation, MetaInfo metadata,
        ReachabilityAnalysis reachability, InvocationSupportCache cache)
    {
        var analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
        var candidates = reachability == null ? analysis.FormatterTypes.Where(type => !analysis.HasOwner(type)) : reachability.ReachableNoOwnerTypes;
        var types = candidates.Where(type => !ContainsTypeParameter(type) && IsAotVisible(type) && IsStaticFormatterCandidate(type) && !IsCustomGeneratorType(type))
            .OrderBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal).GroupBy(type => FormatterTypeName.Get(type), StringComparer.Ordinal)
            .Select(group => group.First()).ToArray();
        string extension = GetExtensionClassName(compilation);
        var files = ImmutableArray.CreateBuilder<FrozenGeneratedSource>();
        foreach (var type in types)
        {
            string name = FormatterTypeName.Get(type);
            var fragment = cache.Fragments.GetOrBuild(type, compilation, metadata, LuminPackGenerationTier.Full, () =>
            {
                var body = new StringBuilder();
                GenerateFormatterExtensions(body, new LuminLocalFieldData { TypeName = name, TypeSymbol = type, Name = "value", IsValue = type.IsValueType },
                    metadata, new HashSet<string>(StringComparer.Ordinal), emitOptionalVariants: reachability == null);
                string source = GeneratedSourceDocumentation.Complete(GenerateExtension(body, extension), metadata.ParseOptions);
                System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationFormatterBuilds);
                return new FrozenTypeEmission(name, source, string.Empty,
                    new FrozenLayoutGroup(extension, metadata.ParseOptions, Array.Empty<FrozenLayoutFragment>(), null), Array.Empty<Diagnostic>());
            }, serializerFragment: true, outputVariant: "invocation:" + (reachability == null));
            if (!string.IsNullOrEmpty(fragment.Extension))
                files.Add(cache.Files.Wrap("LuminPack.Formatter", name, string.Empty, fragment.Extension, string.Empty));
        }

        var dispatch = GetOrderedFormatterTypes(compilation, reachability).Select((type, index) => new DispatchEntry(index, FormatterTypeName.Get(type),
            CanRegisterFormatter(type, compilation, json: false), CanRegisterFormatter(type, compilation, json: true),
            CanRegisterCalculateFormatter(type, compilation))).Where(entry => entry.Binary || entry.Json || entry.Calculate).ToArray();
        var key = new StringBuilder(extension).Append('|');
        foreach (var entry in dispatch)
            key.Append(entry.Id).Append(':').Append(entry.Name.Length).Append(':').Append(entry.Name).Append(':')
                .Append(entry.Binary).Append(':').Append(entry.Json).Append(':').Append(entry.Calculate).Append(';');
        string identity = key.ToString();
        foreach (var part in DispatchFiles(compilation, metadata, cache, extension, dispatch, identity)) files.Add(part);
        return files.ToImmutable();
    }

    /// <summary>Renders fixed dispatch operation files only when their complete dispatch projection changes.</summary>
    /// <param name="compilation">The current consuming compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="cache">The generator-instance dispatch contribution cache.</param>
    /// <param name="extension">The shared partial extension container.</param>
    /// <param name="dispatch">Current dense ids and operation capabilities.</param>
    /// <param name="identity">The complete value projection used for cache validation.</param>
    /// <returns>Registration and five generic operation files.</returns>
    private static IEnumerable<FrozenGeneratedSource> DispatchFiles(Compilation compilation, MetaInfo metadata,
        InvocationSupportCache cache, string extension, DispatchEntry[] dispatch, string identity)
    {
        // One guarded cache holds the small operation array as individually reusable source values.
        var rendered = new List<FrozenGeneratedSource>();
        string[] names = { "Registration", "BinaryWrite", "BinaryRead", "Calculate", "JsonWrite", "JsonRead" };
        Dictionary<string, string> changed = null;
        foreach (string name in names)
        {
            string source = cache.Part(name).GetOrBuild(compilation, metadata, identity, Array.Empty<ITypeSymbol>(), () =>
            {
                if (changed == null)
                {
                    changed = new Dictionary<string, string>(StringComparer.Ordinal);
                    BuildGenericDispatchParts(metadata, extension, dispatch, (part, body) => changed.Add(part,
                        GeneratedSourceDocumentation.Complete(GenerateExtension(new StringBuilder(body), extension), metadata.ParseOptions)));
                }
                return changed[name];
            });
            var file = cache.Files.Wrap("LuminPack.Dispatch", name, string.Empty, source, string.Empty);
            rendered.Add(new FrozenGeneratedSource("LuminPack.Dispatch." + name + ".g.cs", file.Text));
        }
        return rendered;
    }
}
