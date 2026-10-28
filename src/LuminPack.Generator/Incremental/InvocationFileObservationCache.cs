using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Retains verified invocation identities for files whose full binding dependencies remain unchanged.</summary>
internal sealed class InvocationFileObservationCache
{
    private static readonly ConditionalWeakTable<Compilation, InvocationFileObservationCache> Contexts = new ConditionalWeakTable<Compilation, InvocationFileObservationCache>();
    private static readonly ConditionalWeakTable<object, ProjectEntries> Projects = new ConditionalWeakTable<object, ProjectEntries>();
    private readonly Compilation _compilation;
    private readonly FileSemanticObservationCache _observations;
    private readonly ProjectEntries _project;
    private readonly StableMethodIdentityFactory _factory;
    private readonly CurrentMethodResolver _resolver;
    private readonly Dictionary<SyntaxTree, IMethodSymbol[]> _current = new Dictionary<SyntaxTree, IMethodSymbol[]>();
    private readonly object _gate = new object();

    /// <summary>Creates current-compilation method helpers under the shared semantic project guard.</summary>
    /// <param name="compilation">The current input compilation.</param>
    private InvocationFileObservationCache(Compilation compilation)
    {
        _compilation = compilation;
        _observations = FileSemanticObservationCache.For(compilation);
        _project = Projects.GetValue(_observations.ProjectGuard, _ => new ProjectEntries());
        _factory = new StableMethodIdentityFactory(compilation);
        _resolver = new CurrentMethodResolver(compilation);
    }

    /// <summary>Gets method observations whose resolved symbols belong only to the current compilation.</summary>
    /// <param name="compilation">The current compilation.</param>
    /// <returns>The compilation-local invocation cache.</returns>
    internal static InvocationFileObservationCache For(Compilation compilation) => Contexts.GetValue(compilation, current => new InvocationFileObservationCache(current));

    /// <summary>Rebinds an unchanged file's complete method list or repeats its legacy invocation binding.</summary>
    /// <param name="tree">The current file in legacy compilation order.</param>
    /// <returns>Every bound method in syntax order, including null results from fresh binding.</returns>
    internal IMethodSymbol[] Read(SyntaxTree tree)
    {
        lock (_gate)
        {
            if (_current.TryGetValue(tree, out var current)) return current;
            FileEntry entry;
            lock (_project.Entries) _project.Entries.TryGetValue(tree.FilePath, out entry);
            if (entry != null && ReferenceEquals(entry.Tree, tree) && entry.Dependencies.All(_compilation.ContainsSyntaxTree))
            {
                var rebound = entry.Methods.Select(_resolver.Resolve).ToArray();
                if (rebound.All(method => method != null))
                {
                    Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationFileCacheHits);
                    _current[tree] = rebound;
                    return rebound;
                }
            }

            Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationFileObservations);
            var model = _observations.GetSemanticModel(tree);
            var methods = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Select(invocation =>
            {
                Interlocked.Increment(ref IncrementalGenerationMetrics.ReachabilityInvocationBindings);
                return model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            }).ToArray();
            var identities = methods.Select(_factory.Create).ToArray();
            var dependencies = _observations.ObservedDependencies(tree);
            if (dependencies != null && identities.All(identity => identity != null))
            {
                var frozen = new FileEntry(tree, identities, dependencies);
                lock (_project.Entries) _project.Entries[tree.FilePath] = frozen;
            }
            else
            {
                Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationFileFallbacks);
                lock (_project.Entries) _project.Entries.Remove(tree.FilePath);
            }
            _current[tree] = methods;
            return methods;
        }
    }

    /// <summary>Stores no semantic objects from a preceding compilation.</summary>
    private sealed class FileEntry
    {
        internal readonly SyntaxTree Tree;
        internal readonly StableMethodIdentity[] Methods;
        internal readonly SyntaxTree[] Dependencies;

        /// <summary>Captures one file's verified identities and complete source dependency closure.</summary>
        /// <param name="tree">The exact observed syntax tree.</param>
        /// <param name="methods">Invocation identities in legacy order.</param>
        /// <param name="dependencies">Every source tree required for safe binding reuse.</param>
        internal FileEntry(SyntaxTree tree, StableMethodIdentity[] methods, SyntaxTree[] dependencies)
        {
            Tree = tree; Methods = methods; Dependencies = dependencies;
        }
    }

    /// <summary>Keeps per-file entries while their weakly held project guard remains alive.</summary>
    private sealed class ProjectEntries
    {
        internal readonly Dictionary<string, FileEntry> Entries = new Dictionary<string, FileEntry>(System.StringComparer.Ordinal);
    }
}
