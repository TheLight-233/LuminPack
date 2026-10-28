using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Projects current aggregate dependencies into comparable, semantic-object-free versions.</summary>
internal sealed class AggregateEmissionCache
{
    private readonly object _gate = new object();
    private Entry _previous;

    /// <summary>Checks ordered type, ownership, reachability, settings, and source dependencies.</summary>
    /// <param name="compilation">The current compilation at the semantic boundary.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <param name="mode">The requested generation tier.</param>
    /// <returns>A completed version or a weakly bound description of changed work.</returns>
    internal AggregateEmissionInput PrepareInput(Compilation compilation,
        MetaInfo metadata, LuminPackGenerationTier mode)
    {
        var analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
        try
        {
            return PrepareInputCore(compilation, metadata, mode, analysis);
        }
        catch (Exception)
        {
            // Guard construction is optional. Preserve the legacy aggregate operation's
            // diagnostic boundary when an invalid input cannot be safely projected.
            return new AggregateEmissionInput(this, compilation, null, Array.Empty<SyntaxTree>(), metadata, mode);
        }
    }

    /// <summary>Prepares a dependency version without changing the legacy failure-reporting boundary.</summary>
    /// <param name="compilation">The active input compilation.</param>
    /// <param name="metadata">Current language and generation settings.</param>
    /// <param name="mode">The requested generation tier.</param>
    /// <param name="analysis">The shared semantic index already evaluated by the legacy entry point.</param>
    /// <returns>A completed version or a description of changed work.</returns>
    private AggregateEmissionInput PrepareInputCore(Compilation compilation,
        MetaInfo metadata, LuminPackGenerationTier mode, CompilationTypeAnalysis analysis)
    {
        var observations = FileSemanticObservationCache.For(compilation);
        var custom = analysis.DeclaredTypes.Select(value => value.Symbol)
            .Where(LuminPackExtensionGenerator.IsCustomGeneratorType).ToArray();
        var tier = (LuminPackGenerationTier)Math.Max((int)mode, (int)LuminPackGenerationTierResolver.FromAssembly(compilation));
        bool usesLuminPack = analysis.UsesLuminPack || SerializerApiUsageAnalysis.GetOrCreate(compilation).HasSerializerUsage;
        var reachability = usesLuminPack && tier != LuminPackGenerationTier.Full
            ? ReachabilityAnalysisCache.GetOrCreate(compilation, tier) : null;
        string key = CreateKey(compilation, analysis, custom, reachability, usesLuminPack);
        var guard = new Guard(observations.ProjectGuard, metadata, mode, key);
        lock (_gate)
        {
            if (key != null && _previous != null && _previous.Dependencies.Matches(guard) &&
                _previous.Trees.All(compilation.ContainsSyntaxTree))
            {
                Interlocked.Increment(ref IncrementalGenerationMetrics.AggregateCacheHits);
                return new AggregateEmissionInput(_previous.Version, _previous.Result);
            }
        }
        var trees = key == null ? Array.Empty<SyntaxTree>()
            : observations.CaptureDependencies(analysis.FormatterTypes.Concat(custom));
        return new AggregateEmissionInput(this, compilation, guard, trees, metadata, mode);
    }

    /// <summary>Publishes a version only after its complete source and diagnostics have been built.</summary>
    /// <param name="input">The prepared dependency version.</param>
    /// <param name="result">Successfully completed aggregate output.</param>
    internal void Complete(AggregateEmissionInput input, FrozenAggregateResult result)
    {
        if (input.Dependencies?.Key == null) return;
        lock (_gate) _previous = new Entry(input.Dependencies, input.Trees, input.Version, result);
    }

    /// <summary>Encodes complete ordered semantic projections, declining unsupported type shapes.</summary>
    /// <param name="compilation">The compilation supplying current identities.</param>
    /// <param name="analysis">The actual shared current semantic index.</param>
    /// <param name="custom">Custom formatter declarations in registry order.</param>
    /// <param name="reachability">The current pruned emission sets, or null for Full.</param>
    /// <param name="usesLuminPack">Whether marked types or bound facade calls require serializer output.</param>
    /// <returns>A collision-free ordered key, or null to require fresh construction.</returns>
    private static string CreateKey(Compilation compilation, CompilationTypeAnalysis analysis,
        INamedTypeSymbol[] custom, ReachabilityAnalysis reachability, bool usesLuminPack)
    {
        var factory = new StableTypeIdentityFactory(compilation);
        var key = new StringBuilder().Append(usesLuminPack).Append('|');
        if (!AppendTypes(key, factory, analysis.FormatterTypes)) return null;
        foreach (var type in analysis.FormatterTypes)
        {
            var owner = analysis.GetOwner(type);
            if (owner == null) key.Append("0:");
            else if (!AppendTypes(key, factory, new[] { owner })) return null;
        }
        if (!AppendTypes(key, factory, custom)) return null;
        key.Append(reachability == null).Append('|');
        if (reachability != null && (!AppendTypes(key, factory, reachability.EmittedTypes) ||
            !AppendTypes(key, factory, reachability.ReachableNoOwnerTypes))) return null;
        if (reachability?.ApiUsage != null) key.Append(reachability.ApiUsage.Key);
        return key.ToString();
    }

    /// <summary>Appends length-prefixed identities and an explicit sequence boundary.</summary>
    /// <param name="key">The aggregate key being assembled.</param>
    /// <param name="factory">The current compilation's identity factory.</param>
    /// <param name="types">Types in exact legacy order.</param>
    /// <returns>Whether every type has a supported stable representation.</returns>
    private static bool AppendTypes(StringBuilder key, StableTypeIdentityFactory factory, IEnumerable<ITypeSymbol> types)
    {
        foreach (var type in types)
        {
            var identity = factory.Create(type);
            if (identity == null) return false;
            key.Append(identity.Key.Length).Append(':').Append(identity.Key);
        }
        key.Append('|');
        return true;
    }

    /// <summary>Stores only source identities and frozen values across compilations.</summary>
    private sealed class Entry
    {
        internal readonly Guard Dependencies;
        internal readonly SyntaxTree[] Trees;
        internal readonly object Version;
        internal readonly FrozenAggregateResult Result;

        /// <summary>Captures a successfully completed dependency version.</summary>
        /// <param name="dependencies">Complete symbol-free semantic guards.</param>
        /// <param name="trees">Its source dependency closure.</param>
        /// <param name="version">The immutable prepared version identity.</param>
        /// <param name="result">Exact completed output.</param>
        internal Entry(Guard dependencies, SyntaxTree[] trees, object version, FrozenAggregateResult result)
        { Dependencies = dependencies; Trees = trees; Version = version; Result = result; }
    }

    /// <summary>Compares aggregate semantic projections without retaining compilation or symbols.</summary>
    internal sealed class Guard
    {
        private readonly object _project;
        private readonly MetaInfo _metadata;
        private readonly LuminPackGenerationTier _mode;
        internal readonly string Key;

        /// <summary>Freezes settings at the current semantic boundary.</summary>
        /// <param name="project">The complete symbol-free project guard.</param>
        /// <param name="metadata">Language and generation settings to snapshot.</param>
        /// <param name="mode">The requested generation tier.</param>
        /// <param name="key">The ordered semantic key, or null for unsupported inputs.</param>
        internal Guard(object project, MetaInfo metadata, LuminPackGenerationTier mode, string key)
        {
            _project = project;
            _metadata = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8, metadata.IsNet9_OR_GREATER,
                metadata.AllowUnsafe, metadata.RegisterMode) { IsForUnity = metadata.IsForUnity };
            _mode = mode;
            Key = key;
        }

        /// <summary>Checks every value that can affect aggregate source or diagnostic production.</summary>
        /// <param name="other">Current dependency facts.</param>
        /// <returns>Whether source-tree validation may reuse the completed version.</returns>
        internal bool Matches(Guard other) => Key == other.Key && _mode == other._mode &&
            FileSemanticObservationCache.SameProjectGuard(_project, other._project) &&
            MetaInfoValueComparer.Instance.Equals(_metadata, other._metadata);
    }
}
