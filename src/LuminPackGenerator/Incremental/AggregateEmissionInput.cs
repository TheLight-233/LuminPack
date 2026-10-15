using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Stops unchanged aggregate dependency versions before facade and registry construction.</summary>
internal sealed class AggregateEmissionInput : IEquatable<AggregateEmissionInput>
{
    private readonly WeakReference<Compilation> _compilation;
    private readonly AggregateEmissionCache _cache;
    internal readonly object Version;
    internal readonly AggregateEmissionCache.Guard Dependencies;
    internal readonly SyntaxTree[] Trees;
    internal readonly ImmutableArray<FrozenMapperEmission> Automatic;
    internal readonly ImmutableArray<FrozenMapperEmission> Manual;
    internal readonly MetaInfo Metadata;
    internal readonly LuminPack.Code.Core.LuminPackGenerationTier Mode;
    internal readonly FrozenAggregateResult Completed;

    /// <summary>References a successfully completed version without retaining semantic objects.</summary>
    /// <param name="version">The identity assigned to the completed dependency version.</param>
    /// <param name="completed">The exact frozen aggregate output.</param>
    internal AggregateEmissionInput(object version, FrozenAggregateResult completed)
    {
        Version = version;
        Completed = completed;
    }

    /// <summary>Describes changed work with a weak handle to the active upstream compilation.</summary>
    /// <param name="cache">The generator-local cache that will publish the completed version.</param>
    /// <param name="compilation">The current compilation rooted by the upstream provider.</param>
    /// <param name="dependencies">Symbol-free settings and ordered semantic identities.</param>
    /// <param name="trees">The complete source dependency closure, when reusable.</param>
    /// <param name="automatic">Current automatic mapper contributions.</param>
    /// <param name="manual">Current manual mapper contributions.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <param name="mode">The requested generation tier.</param>
    internal AggregateEmissionInput(AggregateEmissionCache cache, Compilation compilation,
        AggregateEmissionCache.Guard dependencies, SyntaxTree[] trees,
        ImmutableArray<FrozenMapperEmission> automatic, ImmutableArray<FrozenMapperEmission> manual,
        MetaInfo metadata, LuminPack.Code.Core.LuminPackGenerationTier mode)
    {
        Version = new object();
        _cache = cache;
        _compilation = new WeakReference<Compilation>(compilation);
        Dependencies = dependencies;
        Trees = trees;
        Automatic = automatic;
        Manual = manual;
        Metadata = metadata;
        Mode = mode;
    }

    /// <summary>Obtains the active compilation only when changed aggregate work executes.</summary>
    /// <returns>The compilation rooted by the upstream incremental input table.</returns>
    internal Compilation GetCompilation() => _compilation.TryGetTarget(out var compilation)
        ? compilation
        : LuminPackExceptionHelper.ThrowInvalidOperationException<Compilation>("Active aggregate analysis inputs are unavailable.");

    /// <summary>Publishes a completed result after successful aggregate construction.</summary>
    /// <param name="result">The exact source and diagnostic values.</param>
    /// <returns>The same result for downstream per-file projection.</returns>
    internal FrozenAggregateResult Complete(FrozenAggregateResult result)
    {
        _cache.Complete(this, result);
        return result;
    }

    /// <summary>Compares validated versions before aggregate construction is scheduled.</summary>
    /// <param name="other">The preceding prepared input.</param>
    /// <returns>Whether Roslyn can reuse the completed aggregate transform.</returns>
    public bool Equals(AggregateEmissionInput other) => other != null && ReferenceEquals(Version, other.Version);

    /// <summary>Compares an arbitrary value with this dependency version.</summary>
    /// <param name="obj">The value to compare.</param>
    /// <returns>Whether both values identify the same completed version.</returns>
    public override bool Equals(object obj) => Equals(obj as AggregateEmissionInput);

    /// <summary>Hashes the immutable version identity.</summary>
    /// <returns>A hash consistent with version equality.</returns>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Version);
}
