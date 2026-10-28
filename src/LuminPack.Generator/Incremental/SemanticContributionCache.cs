using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Reuses symbol-free contributions before invoking their legacy semantic analysis.</summary>
/// <typeparam name="T">The immutable contribution value retained between compilations.</typeparam>
internal sealed class SemanticContributionCache<T>
{
    private readonly object _gate = new object();
    private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private object _latestGuard;

    /// <summary>Checks complete settings and source dependency identities before analyzing a selected declaration.</summary>
    /// <param name="type">The currently selected declaration.</param>
    /// <param name="compilation">The compilation supplying current semantic objects.</param>
    /// <param name="metadata">Every language, framework, unsafe, and registration setting.</param>
    /// <param name="build">The semantic analysis and immutable projection to run on a miss.</param>
    /// <param name="hit">Execution accounting invoked only on an actual cache hit.</param>
    /// <returns>The frozen contribution, without reusing old semantic models.</returns>
    internal T GetOrBuild(INamedTypeSymbol type, Compilation compilation, MetaInfo metadata, Func<T> build, Action hit)
    {
        var observations = FileSemanticObservationCache.For(compilation);
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        lock (_gate)
        {
            if (!FileSemanticObservationCache.SameProjectGuard(_latestGuard, observations.ProjectGuard)) _entries.Clear();
            _latestGuard = observations.ProjectGuard;
            if (_entries.TryGetValue(name, out var previous) && MetaInfoValueComparer.Instance.Equals(previous.Metadata, metadata) &&
                previous.Dependencies.All(compilation.ContainsSyntaxTree))
            {
                hit();
                return previous.Value;
            }
            var value = build();
            var dependencies = observations.CaptureDependencies(new ITypeSymbol[] { type });
            var frozenMetadata = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8, metadata.IsNet9_OR_GREATER,
                metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
            _entries[name] = new Entry(frozenMetadata, dependencies, value);
            return value;
        }
    }

    /// <summary>Retains contribution values, immutable settings, and syntax dependencies only.</summary>
    private sealed class Entry
    {
        internal readonly MetaInfo Metadata;
        internal readonly SyntaxTree[] Dependencies;
        internal readonly T Value;

        /// <summary>Captures a successfully completed semantic contribution.</summary>
        /// <param name="metadata">The copied generation settings.</param>
        /// <param name="dependencies">The source dependency closure.</param>
        /// <param name="value">The symbol-free immutable value.</param>
        internal Entry(MetaInfo metadata, SyntaxTree[] dependencies, T value) { Metadata = metadata; Dependencies = dependencies; Value = value; }
    }
}
