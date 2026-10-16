using System;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Reuses complete invocation source from exact dispatch values and guarded formatter bodies.</summary>
internal sealed class InvocationSupportCache
{
    internal readonly TypeEmissionCache Fragments = new TypeEmissionCache();
    internal readonly PartitionedSourceCache Files = new PartitionedSourceCache();
    internal readonly BinaryOperationPlanCache Operations = new BinaryOperationPlanCache();
    private readonly System.Collections.Generic.Dictionary<string, InvocationSupportCache> _parts = new(System.StringComparer.Ordinal);
    private readonly object _gate = new object();
    private object _project;
    private string _key;
    private MetaInfo _metadata;
    private SyntaxTree[] _dependencies;
    private string _source;

    /// <summary>Finds one fixed dispatch contribution's independently guarded rendering cache.</summary>
    /// <param name="name">The registration or dispatch operation identity.</param>
    /// <returns>A cache containing value source and semantic dependency guards.</returns>
    internal InvocationSupportCache Part(string name)
    {
        lock (_parts)
        {
            if (!_parts.TryGetValue(name, out var cache)) _parts.Add(name, cache = new InvocationSupportCache());
            return cache;
        }
    }

    /// <summary>Checks semantic validity before constructing or documenting the aggregate source.</summary>
    /// <param name="compilation">The compilation containing current semantic objects.</param>
    /// <param name="metadata">Language, framework, unsafe, and registration settings.</param>
    /// <param name="key">Complete ordered dispatch values and unowned formatter identities, or null to decline reuse.</param>
    /// <param name="bodyTypes">Types whose generated bodies require source dependency protection.</param>
    /// <param name="build">The legacy source and documentation operation.</param>
    /// <returns>The complete exact legacy source, reused before rendering whenever valid.</returns>
    internal string GetOrBuild(Compilation compilation, MetaInfo metadata, string key, ITypeSymbol[] bodyTypes, Func<string> build)
    {
        var observations = FileSemanticObservationCache.For(compilation);
        lock (_gate)
        {
            if (key != null && _source != null && ReferenceEquals(_project, observations.ProjectGuard) && _key == key &&
                MetaInfoValueComparer.Instance.Equals(_metadata, metadata) && _dependencies.All(compilation.ContainsSyntaxTree))
            {
                Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationSupportCacheHits);
                return _source;
            }
        }
        Interlocked.Increment(ref IncrementalGenerationMetrics.InvocationSupportBuilds);
        string source = build();
        if (key == null) return source;
        var dependencies = observations.CaptureDependencies(bodyTypes);
        var frozenMetadata = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8, metadata.IsNet9_OR_GREATER,
            metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
        lock (_gate)
        {
            _project = observations.ProjectGuard;
            _key = key;
            _metadata = frozenMetadata;
            _dependencies = dependencies;
            _source = source;
        }
        return source;
    }
}
