using System;
using System.Runtime.CompilerServices;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Stops unchanged dependency versions before the per-type analysis transform.</summary>
internal sealed class TypeEmissionInput : IEquatable<TypeEmissionInput>
{
    private readonly object _version;
    private readonly FrozenTypeEmission _completed;
    private readonly TypeEmissionCache _cache;
    private readonly WeakReference<INamedTypeSymbol> _type;
    private readonly WeakReference<Compilation> _compilation;
    private readonly MetaInfo _metadata;
    private readonly LuminPackGenerationTier _tier;

    /// <summary>References a completed dependency version without retaining semantic inputs.</summary>
    /// <param name="version">The identity assigned when that version was analyzed.</param>
    /// <param name="completed">Its immutable emission result.</param>
    internal TypeEmissionInput(object version, FrozenTypeEmission completed)
    {
        _version = version;
        _completed = completed;
    }

    /// <summary>Describes changed work using weak handles to the active Roslyn input tables.</summary>
    /// <param name="cache">The generator-local cache that publishes completed dependency versions.</param>
    /// <param name="type">The current type rooted by the upstream syntax provider.</param>
    /// <param name="compilation">The active compilation rooted by its upstream provider.</param>
    /// <param name="metadata">Current generation settings without semantic objects.</param>
    /// <param name="tier">The effective generation tier.</param>
    internal TypeEmissionInput(TypeEmissionCache cache, INamedTypeSymbol type, Compilation compilation,
        MetaInfo metadata, LuminPackGenerationTier tier)
    {
        _version = new object();
        _cache = cache;
        _type = new WeakReference<INamedTypeSymbol>(type);
        _compilation = new WeakReference<Compilation>(compilation);
        _metadata = metadata;
        _tier = tier;
    }

    /// <summary>Analyzes a changed version while its active upstream inputs remain rooted.</summary>
    /// <param name="build">The existing semantic analysis and rendering operation.</param>
    /// <returns>The immutable output for this dependency version.</returns>
    internal FrozenTypeEmission Evaluate(Func<INamedTypeSymbol, Compilation, MetaInfo, LuminPackGenerationTier, FrozenTypeEmission> build)
    {
        if (_completed != null) return _completed;
        if (!_type.TryGetTarget(out var type) || !_compilation.TryGetTarget(out var compilation))
            return LuminPackExceptionHelper.ThrowInvalidOperationException<FrozenTypeEmission>("Active type analysis inputs are unavailable.");
        return _cache.GetOrBuild(type, compilation, _metadata, _tier,
            () => build(type, compilation, _metadata, _tier), version: _version);
    }

    /// <summary>Compares validated dependency versions before semantic analysis is scheduled.</summary>
    /// <param name="other">The preceding type input.</param>
    /// <returns>Whether the analysis transform can reuse its previous result.</returns>
    public bool Equals(TypeEmissionInput other) => other != null && ReferenceEquals(_version, other._version);

    /// <summary>Compares an arbitrary object with this dependency version.</summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>Whether both objects identify the same analyzed dependency version.</returns>
    public override bool Equals(object obj) => Equals(obj as TypeEmissionInput);

    /// <summary>Hashes the immutable dependency version identity.</summary>
    /// <returns>A hash consistent with version equality.</returns>
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(_version);
}
