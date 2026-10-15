using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Retains per-packable ownership contributions as values before rebuilding the project index.</summary>
internal static class PackableGraphCache
{
    private static readonly ConditionalWeakTable<object, SharedEntries> Projects = new ConditionalWeakTable<object, SharedEntries>();
    private static readonly ConditionalWeakTable<Compilation, CurrentContext> Contexts = new ConditionalWeakTable<Compilation, CurrentContext>();

    /// <summary>Gets the legacy graph contribution without inspecting unchanged packable members again.</summary>
    /// <param name="compilation">The compilation supplying current symbols.</param>
    /// <param name="root">The current source packable definition.</param>
    /// <param name="build">The legacy traversal, receiving empty owned and analyzed sets.</param>
    /// <returns>The current-symbol projection of a frozen contribution or freshly analyzed fallback.</returns>
    internal static CurrentGraph GetOrBuild(Compilation compilation, INamedTypeSymbol root, Action<ISet<ITypeSymbol>, ISet<ITypeSymbol>> build)
        => Contexts.GetValue(compilation, current => new CurrentContext(current)).Get(root, build);

    /// <summary>Stores only frozen type identities and syntax dependency identities between compilations.</summary>
    private sealed class SharedEntries
    {
        internal readonly Dictionary<string, Entry> Entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    }

    /// <summary>Owns rebinding objects only within their current compilation.</summary>
    private sealed class CurrentContext
    {
        private readonly Compilation _compilation;
        private readonly FileSemanticObservationCache _observations;
        private readonly SharedEntries _project;
        private readonly StableTypeIdentityFactory _factory;
        private readonly CurrentTypeResolver _resolver;
        private readonly object _gate = new object();

        /// <summary>Acquires the guarded project's symbol-free entry collection.</summary>
        /// <param name="compilation">The current compilation.</param>
        internal CurrentContext(Compilation compilation)
        {
            _compilation = compilation;
            _observations = FileSemanticObservationCache.For(compilation);
            _project = Projects.GetValue(_observations.ProjectGuard, _ => new SharedEntries());
            _factory = new StableTypeIdentityFactory(compilation);
            _resolver = new CurrentTypeResolver(compilation);
        }

        /// <summary>Checks dependencies before invoking member and attribute traversal.</summary>
        /// <param name="root">The current source packable.</param>
        /// <param name="build">The unchanged legacy graph traversal.</param>
        /// <returns>Current-compilation types in legacy contribution order.</returns>
        internal CurrentGraph Get(INamedTypeSymbol root, Action<ISet<ITypeSymbol>, ISet<ITypeSymbol>> build)
        {
            lock (_gate)
            {
                var identity = _factory.Create(root);
                Entry entry = null;
                if (identity != null)
                    lock (_project.Entries) _project.Entries.TryGetValue(identity.Key, out entry);
                if (entry != null && entry.Dependencies.All(_compilation.ContainsSyntaxTree))
                {
                    var owned = entry.Owned.Select(_resolver.Resolve).ToArray();
                    var analyzed = entry.Analyzed.Select(_resolver.Resolve).ToArray();
                    if (owned.All(type => type != null) && analyzed.All(type => type != null))
                    {
                        Interlocked.Increment(ref IncrementalGenerationMetrics.OwnershipGraphCacheHits);
                        return new CurrentGraph(owned, analyzed);
                    }
                }

                Interlocked.Increment(ref IncrementalGenerationMetrics.OwnershipGraphBuilds);
                var ownedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                var analyzedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                build(ownedTypes, analyzedTypes);
                var result = new CurrentGraph(ownedTypes.ToArray(), analyzedTypes.ToArray());
                var frozenOwned = result.Owned.Select(_factory.Create).ToArray();
                var frozenAnalyzed = result.Analyzed.Select(_factory.Create).ToArray();
                if (identity != null && frozenOwned.All(type => type != null) && frozenAnalyzed.All(type => type != null))
                {
                    var frozen = new Entry(frozenOwned, frozenAnalyzed, _observations.CaptureDependencies(new ITypeSymbol[] { root }));
                    lock (_project.Entries) _project.Entries[identity.Key] = frozen;
                }
                else
                {
                    Interlocked.Increment(ref IncrementalGenerationMetrics.OwnershipGraphFallbacks);
                    if (identity != null) lock (_project.Entries) _project.Entries.Remove(identity.Key);
                }
                return result;
            }
        }
    }

    /// <summary>Contains one symbol-free graph and its complete source dependency closure.</summary>
    private sealed class Entry
    {
        internal readonly StableTypeIdentity[] Owned;
        internal readonly StableTypeIdentity[] Analyzed;
        internal readonly SyntaxTree[] Dependencies;

        /// <summary>Freezes a successfully encoded legacy graph contribution.</summary>
        /// <param name="owned">Owned types in first-occurrence order.</param>
        /// <param name="analyzed">All observed graph types in first-occurrence order.</param>
        /// <param name="dependencies">Source trees required for reuse.</param>
        internal Entry(StableTypeIdentity[] owned, StableTypeIdentity[] analyzed, SyntaxTree[] dependencies)
        { Owned = owned; Analyzed = analyzed; Dependencies = dependencies; }
    }

    /// <summary>Projects one contribution into symbols belonging to the current compilation only.</summary>
    internal sealed class CurrentGraph
    {
        internal readonly ITypeSymbol[] Owned;
        internal readonly ITypeSymbol[] Analyzed;

        /// <summary>Captures current types without retaining them in the shared project cache.</summary>
        /// <param name="owned">The current owned type sequence.</param>
        /// <param name="analyzed">The current complete type sequence.</param>
        internal CurrentGraph(ITypeSymbol[] owned, ITypeSymbol[] analyzed) { Owned = owned; Analyzed = analyzed; }
    }
}
