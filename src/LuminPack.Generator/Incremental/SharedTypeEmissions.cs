using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Shares one rendering of each annotated type between the generators that publish its parts.</summary>
/// <remarks>The binary, JSON and formatter generators each publish a different part of the same type emission. Within one
/// compilation and one set of generation settings, the first generator that needs a type renders it and the others reuse that
/// value, the same way <see cref="CurrentSourceEmission"/> shares the aggregate input. A rendering is dropped once every
/// output has taken it, and a generator that asks again for an output it already took starts a new rendering, so a later run
/// over the same compilation renders afresh. The table is keyed weakly by the compilation, so no compilation, semantic model or
/// symbol survives it, and each generator's own cache still decides independently whether a type changed.</remarks>
internal static class SharedTypeEmissions
{
    private static readonly ConditionalWeakTable<Compilation, Dictionary<string, List<Shared>>> Values = new();

    /// <summary>Returns the type's emission for this compilation, rendering it once for all outputs.</summary>
    /// <param name="compilation">The compilation whose symbols the emission describes.</param>
    /// <param name="type">The annotated type.</param>
    /// <param name="metadata">The exact generation settings.</param>
    /// <param name="tier">The effective generation tier.</param>
    /// <param name="owners">The outputs of the asking generator.</param>
    /// <param name="render">Renders the complete emission.</param>
    /// <returns>The shared emission.</returns>
    internal static FrozenTypeEmission Get(Compilation compilation, INamedTypeSymbol type, MetaInfo metadata, LuminPackGenerationTier tier,
        GeneratorOutput owners, Func<FrozenTypeEmission> render)
    {
        var values = Values.GetValue(compilation, static _ => new Dictionary<string, List<Shared>>(StringComparer.Ordinal));
        string name = FormatterTypeName.Get(type);
        Lazy<FrozenTypeEmission> value;
        lock (values)
        {
            if (!values.TryGetValue(name, out var list))
                values.Add(name, list = new List<Shared>());
            int index = list.FindIndex(item => item.Tier == tier && MetaInfoValueComparer.Instance.Equals(item.Metadata, metadata));
            if (index >= 0 && (list[index].Owners & owners) == 0)
            {
                var previous = list[index];
                value = previous.Value;
                var seen = previous.Owners | owners;
                if (seen == GeneratorOutput.All)
                    list.RemoveAt(index);
                else
                    list[index] = new Shared(previous.Metadata, previous.Tier, seen, value);
            }
            else
            {
                // Only a new rendering counts as a model build; the other generators reuse it.
                value = new Lazy<FrozenTypeEmission>(() =>
                {
                    Interlocked.Increment(ref IncrementalGenerationMetrics.PackableModelBuilds);
                    return render();
                }, LazyThreadSafetyMode.ExecutionAndPublication);
                var shared = new Shared(metadata, tier, owners, value);
                if (index >= 0)
                    list[index] = shared;
                else
                    list.Add(shared);
            }
            if (list.Count == 0)
                values.Remove(name);
        }
        try
        {
            return value.Value;
        }
        finally
        {
            // A failed rendering is not shared, so the next generator reports it from its own attempt.
            if (!value.IsValueCreated)
                lock (values)
                    if (values.TryGetValue(name, out var list))
                        list.RemoveAll(item => ReferenceEquals(item.Value, value));
        }
    }

    /// <summary>One rendering and the outputs that have taken it.</summary>
    private readonly struct Shared
    {
        internal readonly MetaInfo Metadata;
        internal readonly LuminPackGenerationTier Tier;
        internal readonly GeneratorOutput Owners;
        internal readonly Lazy<FrozenTypeEmission> Value;

        /// <summary>Initializes a <c>Shared</c> from the supplied data.</summary>
        /// <param name="metadata">The metadata supplied to <c>Shared</c>.</param>
        /// <param name="tier">The tier supplied to <c>Shared</c>.</param>
        /// <param name="owners">The owners supplied to <c>Shared</c>.</param>
        /// <param name="value">The value to process.</param>
        internal Shared(MetaInfo metadata, LuminPackGenerationTier tier, GeneratorOutput owners, Lazy<FrozenTypeEmission> value)
        {
            Metadata = metadata;
            Tier = tier;
            Owners = owners;
            Value = value;
        }
    }
}
