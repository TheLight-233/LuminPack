using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace LuminPack.SourceGenerator;

/// <summary>Skips registry concatenation and documentation when all exact emission inputs remain equal.</summary>
internal sealed class RegistrySourceCache
{
    private readonly object _gate = new object();
    private readonly bool _custom;
    private string _assemblyName;
    private bool _unity;
    private MetaInfo _metadata;
    private string[] _blocks;
    private string _source;

    /// <summary>Creates an independent cache for one registry output kind.</summary>
    /// <param name="custom">Whether execution counts belong to custom rather than mapper registration.</param>
    internal RegistrySourceCache(bool custom) => _custom = custom;

    /// <summary>Checks immutable ordered inputs before running legacy wrapper and XML rendering.</summary>
    /// <param name="assemblyName">The complete original assembly identity used by the wrapper.</param>
    /// <param name="unity">The current Unity attribute-selection fact.</param>
    /// <param name="metadata">All generation and documentation settings.</param>
    /// <param name="blocks">Exact registration contributions in original order, including empty contributions.</param>
    /// <param name="build">The legacy wrapper and documentation operation, invoked only on a miss.</param>
    /// <returns>The exact complete registry source.</returns>
    internal string GetOrBuild(string assemblyName, bool unity, MetaInfo metadata, IReadOnlyList<string> blocks, Func<string> build)
    {
        lock (_gate)
        {
            if (_source != null && _assemblyName == assemblyName && _unity == unity &&
                MetaInfoValueComparer.Instance.Equals(_metadata, metadata) && _blocks.SequenceEqual(blocks))
            {
                if (_custom) Interlocked.Increment(ref IncrementalGenerationMetrics.CustomRegistrySourceCacheHits);
                else Interlocked.Increment(ref IncrementalGenerationMetrics.MapperRegistrySourceCacheHits);
                return _source;
            }
            if (_custom) Interlocked.Increment(ref IncrementalGenerationMetrics.CustomRegistrySourceBuilds);
            else Interlocked.Increment(ref IncrementalGenerationMetrics.MapperRegistrySourceBuilds);
            string source = build();
            _assemblyName = assemblyName;
            _unity = unity;
            _metadata = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8, metadata.IsNet9_OR_GREATER,
                metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
            _blocks = blocks.ToArray();
            return _source = source;
        }
    }
}
