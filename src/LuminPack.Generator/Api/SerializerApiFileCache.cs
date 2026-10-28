using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;

namespace LuminPack.Code.Core;

/// <summary>Rebinds verified facade-aware method identities without revisiting unchanged source expressions.</summary>
internal sealed class SerializerApiFileCache
{
    private static readonly ConditionalWeakTable<object, ProjectEntries> Projects = new();
    private readonly Compilation _input;
    private readonly SyntaxTree _signatures;
    private readonly FileSemanticObservationCache _observations;
    private readonly ProjectEntries _project;
    private readonly StableMethodIdentityFactory _factory;
    private readonly CurrentMethodResolver _resolver;
    private readonly StableTypeIdentityFactory _typeFactory;
    private readonly CurrentTypeResolver _typeResolver;

    /// <summary>Creates helpers whose reconstructed methods belong to the current analysis compilation.</summary>
    /// <param name="input">The original source compilation.</param>
    /// <param name="binding">The compilation containing the current signature facade.</param>
    /// <param name="signatures">The exact signature tree, or null for an existing source facade.</param>
    internal SerializerApiFileCache(Compilation input, Compilation binding, SyntaxTree signatures)
    {
        _input = input;
        _signatures = signatures;
        _observations = FileSemanticObservationCache.For(input);
        _project = Projects.GetValue(_observations.ProjectGuard, _ => new ProjectEntries());
        _factory = new StableMethodIdentityFactory(binding);
        _resolver = new CurrentMethodResolver(binding);
        _typeFactory = new StableTypeIdentityFactory(binding);
        _typeResolver = new CurrentTypeResolver(binding);
    }

    /// <summary>Reuses one file only when its signature universe, project guard, and semantic dependencies match.</summary>
    /// <param name="tree">The source file being observed.</param>
    /// <param name="bind">Fresh semantic binding used when reuse cannot be verified.</param>
    /// <returns>Current methods and the unresolved-call retention decision.</returns>
    internal SerializerApiFileMethods Read(SyntaxTree tree, Func<SerializerApiFileMethods> bind)
    {
        Entry previous;
        lock (_project.Entries) _project.Entries.TryGetValue(tree.FilePath, out previous);
        if (previous != null && ReferenceEquals(previous.Tree, tree) && ReferenceEquals(previous.Signatures, _signatures) &&
            previous.Dependencies.All(_input.ContainsSyntaxTree))
        {
            var methods = previous.Methods.Select(_resolver.Resolve).ToArray();
            var payloads = previous.StatePayloads.Select(_typeResolver.Resolve).ToArray();
            if (methods.All(method => method != null) && payloads.All(payload => payload != null))
            {
                System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.ApiFileCacheHits);
                return new SerializerApiFileMethods(methods, previous.Conservative, payloads);
            }
        }

        var current = bind();
        var identities = current.Methods.Select(_factory.Create).ToArray();
        var statePayloads = current.StatePayloads.Select(_typeFactory.Create).ToArray();
        var dependencies = _observations.ObservedDependencies(tree);
        if (dependencies != null && identities.All(identity => identity != null) && statePayloads.All(identity => identity != null))
        {
            var entry = new Entry(tree, _signatures, dependencies, identities, current.Conservative, statePayloads);
            lock (_project.Entries) _project.Entries[tree.FilePath] = entry;
        }
        else
        {
            lock (_project.Entries) _project.Entries.Remove(tree.FilePath);
        }
        return current;
    }

    /// <summary>Stores value identities and source trees without retaining compilation symbols.</summary>
    private sealed class Entry
    {
        internal readonly SyntaxTree Tree;
        internal readonly SyntaxTree Signatures;
        internal readonly SyntaxTree[] Dependencies;
        internal readonly StableMethodIdentity[] Methods;
        internal readonly StableTypeIdentity[] StatePayloads;
        internal readonly bool Conservative;

        /// <summary>Captures one successfully observed file.</summary>
        /// <param name="tree">The original source tree.</param>
        /// <param name="signatures">The signature universe used for overload resolution.</param>
        /// <param name="dependencies">Complete original semantic dependencies.</param>
        /// <param name="methods">Verifiable bound method identities.</param>
        /// <param name="conservative">Whether unresolved serializer expressions require retention.</param>
        /// <param name="statePayloads">The payload identities observed at fluent binary state calls.</param>
        internal Entry(SyntaxTree tree, SyntaxTree signatures, SyntaxTree[] dependencies, StableMethodIdentity[] methods, bool conservative, StableTypeIdentity[] statePayloads)
        { Tree = tree; Signatures = signatures; Dependencies = dependencies; Methods = methods; Conservative = conservative; StatePayloads = statePayloads; }
    }

    /// <summary>Retains the most recent supported file observation under a weak project guard.</summary>
    private sealed class ProjectEntries
    {
        internal readonly Dictionary<string, Entry> Entries = new(StringComparer.Ordinal);
    }
}

/// <summary>Keeps current bound methods local to one compilation's API analysis.</summary>
internal sealed class SerializerApiFileMethods
{
    internal readonly IMethodSymbol[] Methods;
    internal readonly ITypeSymbol[] StatePayloads;
    internal readonly bool Conservative;

    /// <summary>Groups current method symbols and unresolved-call handling.</summary>
    /// <param name="methods">Bound methods from invocations and method groups.</param>
    /// <param name="conservative">Whether unknown serializer expressions were observed.</param>
    /// <param name="statePayloads">The types passed to complete Write and Read operations.</param>
    internal SerializerApiFileMethods(IMethodSymbol[] methods, bool conservative, ITypeSymbol[] statePayloads = null)
    { Methods = methods; Conservative = conservative; StatePayloads = statePayloads ?? Array.Empty<ITypeSymbol>(); }
}
