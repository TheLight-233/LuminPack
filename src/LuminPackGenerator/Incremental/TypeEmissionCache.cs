using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Reuses frozen type outputs while their semantic dependencies and project facts remain valid.</summary>
internal sealed class TypeEmissionCache
{
    private readonly object _gate = new object();
    private static readonly ConditionalWeakTable<Compilation, CompilationFacts> _contexts = new ConditionalWeakTable<Compilation, CompilationFacts>();
    private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    private string _latestProjectIdentity;

    /// <summary>Projects current semantic guards into a comparable input before per-type analysis.</summary>
    /// <param name="type">The current annotated type.</param>
    /// <param name="compilation">The active input compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="tier">The effective generation tier.</param>
    /// <returns>A completed version or a weakly bound changed-work description.</returns>
    internal TypeEmissionInput PrepareInput(INamedTypeSymbol type, Compilation compilation, MetaInfo metadata, LuminPackGenerationTier tier)
    {
        var lookup = Lookup(type, compilation, metadata, tier);
        if (lookup.Previous != null)
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.PackableCacheHits);
            return new TypeEmissionInput(lookup.Previous.Version, (FrozenTypeEmission)lookup.Previous.Emission);
        }
        return new TypeEmissionInput(this, type, compilation, metadata, tier);
    }

    /// <summary>Finds a valid frozen plan before invoking the legacy semantic analysis and emitters.</summary>
    /// <param name="type">The current annotated type.</param>
    /// <param name="compilation">The current compilation used only at the semantic boundary.</param>
    /// <param name="metadata">The current immutable-in-practice generation settings.</param>
    /// <param name="tier">The effective generation tier.</param>
    /// <param name="build">The analysis and emission operation to run on a cache miss.</param>
    /// <param name="serializerFragment">Whether the operation builds a concrete support fragment for separate execution accounting.</param>
    /// <param name="version">The prepared input version to publish after successful analysis, if present.</param>
    /// <param name="outputVariant">An additional immutable guard for selected facade overloads.</param>
    /// <returns>A semantic-object-free emission plan.</returns>
    internal FrozenTypeEmission GetOrBuild(ITypeSymbol type, Compilation compilation, MetaInfo metadata, LuminPackGenerationTier tier, Func<FrozenTypeEmission> build, bool serializerFragment = false, object version = null, string outputVariant = null)
        => GetOrBuildValue(type, compilation, metadata, tier, build, serializerFragment, version, outputVariant);

    /// <summary>Reuses a semantic-object-free operation value under the same complete dependency guards as type output.</summary>
    /// <typeparam name="T">The immutable value retained by this cache instance.</typeparam>
    /// <param name="type">The operation root.</param>
    /// <param name="compilation">The current semantic input.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="tier">The effective generation tier.</param>
    /// <param name="build">Builds a changed value without retaining semantic objects.</param>
    /// <param name="serializerFragment">Existing output counter selection, or null for operation-specific accounting.</param>
    /// <param name="version">An optional prepared version identity.</param>
    /// <param name="outputVariant">Additional output selection facts.</param>
    /// <returns>The unchanged cached value or the newly built value.</returns>
    internal T GetOrBuildValue<T>(ITypeSymbol type, Compilation compilation, MetaInfo metadata, LuminPackGenerationTier tier,
        Func<T> build, bool? serializerFragment = null, object version = null, string outputVariant = null) where T : class
    {
        var lookup = Lookup(type, compilation, metadata, tier, outputVariant);
        if (lookup.Previous != null)
        {
            if (serializerFragment == true) Interlocked.Increment(ref IncrementalGenerationMetrics.SerializerFragmentCacheHits);
            else if (serializerFragment == false) Interlocked.Increment(ref IncrementalGenerationMetrics.PackableCacheHits);
            return (T)lookup.Previous.Emission;
        }
        if (serializerFragment == true) Interlocked.Increment(ref IncrementalGenerationMetrics.SerializerFragmentBuilds);
        else if (serializerFragment == false) Interlocked.Increment(ref IncrementalGenerationMetrics.PackableModelBuilds);
        var emission = build();
        var dependencies = lookup.Facts.Dependencies(type, compilation, lookup.Union);
        var entry = new Entry(lookup.Facts.ProjectGuard, metadata, tier, lookup.DependencyGuard, dependencies, emission, version ?? new object());
        lock (_gate)
        {
            if (_latestProjectIdentity == lookup.Facts.Identity) _entries[lookup.Name] = entry;
        }
        return emission;
    }

    /// <summary>Checks current project and source guards without constructing a new semantic model.</summary>
    /// <param name="type">The root being considered.</param>
    /// <param name="compilation">The active input compilation.</param>
    /// <param name="metadata">Current generation settings.</param>
    /// <param name="tier">The effective generation tier.</param>
    /// <param name="outputVariant">Additional output selection facts, when applicable.</param>
    /// <returns>Current guard facts and any reusable completed entry; this object stays local to the transform.</returns>
    private CacheLookup Lookup(ITypeSymbol type, Compilation compilation, MetaInfo metadata, LuminPackGenerationTier tier, string outputVariant = null)
    {
        var facts = _contexts.GetValue(compilation, current => new CompilationFacts(current));
        string name = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        string owned = facts.Ownership.TryGetValue(name, out var types) ? types : string.Empty;
        bool union = type.IsAbstract || type.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() is
            LuminPackSourceGenerator.LUMIN_UNION_ATTRIBUTE or "System.Runtime.CompilerServices.UnionAttribute");
        string dependencyGuard = owned + (union ? "\nunion:" + facts.UnionUniverse : string.Empty);
        if (outputVariant != null) dependencyGuard += "\noutput-variant:" + outputVariant;
        if (tier == LuminPackGenerationTier.Minimal)
            dependencyGuard += "\nminimal-emitted:" + ReachabilityAnalysisCache.GetOrCreate(compilation, tier).ShouldEmitPackable(type);
        lock (_gate)
        {
            if (_latestProjectIdentity != facts.Identity)
            {
                _entries.Clear();
                _latestProjectIdentity = facts.Identity;
            }
            if (_entries.TryGetValue(name, out var previous) &&
                previous.Matches(compilation, facts, metadata, tier, dependencyGuard))
            {
                return new CacheLookup(facts, name, dependencyGuard, union, previous);
            }
        }

        return new CacheLookup(facts, name, dependencyGuard, union, null);
    }

    /// <summary>Keeps current semantic facts local to lookup and publication, outside cached input values.</summary>
    private sealed class CacheLookup
    {
        internal readonly CompilationFacts Facts;
        internal readonly string Name;
        internal readonly string DependencyGuard;
        internal readonly bool Union;
        internal readonly Entry Previous;

        /// <summary>Groups the guard values observed for one root lookup.</summary>
        /// <param name="facts">The current compilation-local facts.</param>
        /// <param name="name">The stable root name.</param>
        /// <param name="dependencyGuard">The current ownership, union, and emission decisions.</param>
        /// <param name="union">Whether derived candidates affect this root.</param>
        /// <param name="previous">A valid completed entry, when available.</param>
        internal CacheLookup(CompilationFacts facts, string name, string dependencyGuard, bool union, Entry previous)
        {
            Facts = facts; Name = name; DependencyGuard = dependencyGuard; Union = union; Previous = previous;
        }
    }

    /// <summary>Stores validity facts without retaining a compilation or its symbols.</summary>
    private sealed class Entry
    {
        private readonly object _projectGuard;
        private readonly MetaInfo _metadata;
        private readonly LuminPackGenerationTier _tier;
        private readonly string _dependencyGuard;
        private readonly SyntaxTree[] _trees;
        internal readonly object Emission;
        internal readonly object Version;

        /// <summary>Captures dependency identities after successful analysis.</summary>
        /// <param name="projectGuard">Shared validated project settings without semantic symbols.</param>
        /// <param name="metadata">The analyzed generation settings.</param>
        /// <param name="tier">The analyzed tier.</param>
        /// <param name="dependencyGuard">The ownership and union facts read by the type.</param>
        /// <param name="trees">The source dependency closure.</param>
        /// <param name="emission">The frozen output plan.</param>
        /// <param name="version">The immutable identity exposed to the incremental analysis input.</param>
        internal Entry(object projectGuard, MetaInfo metadata, LuminPackGenerationTier tier, string dependencyGuard, SyntaxTree[] trees, object emission, object version)
        {
            _projectGuard = projectGuard;
            _metadata = new MetaInfo(metadata.ParseOptions, metadata.CSharpVersion, metadata.IsNet8, metadata.IsNet9_OR_GREATER, metadata.AllowUnsafe, metadata.RegisterMode)
                { IsForUnity = metadata.IsForUnity };
            _tier = tier;
            _dependencyGuard = dependencyGuard;
            _trees = trees;
            Emission = emission;
            Version = version;
        }

        /// <summary>Checks current project settings and all recorded source dependencies.</summary>
        /// <param name="compilation">The current compilation.</param>
        /// <param name="facts">Shared current project validity checks.</param>
        /// <param name="metadata">The current generation settings.</param>
        /// <param name="tier">The current tier.</param>
        /// <param name="dependencyGuard">The current ownership and union facts.</param>
        /// <returns>Whether expensive type analysis can be skipped.</returns>
        internal bool Matches(Compilation compilation, CompilationFacts facts, MetaInfo metadata, LuminPackGenerationTier tier, string dependencyGuard)
        {
            if (!facts.MatchesProject(_projectGuard) || !MetaInfoValueComparer.Instance.Equals(_metadata, metadata) ||
                _tier != tier || _dependencyGuard != dependencyGuard) return false;
            return _trees.All(compilation.ContainsSyntaxTree);
        }
    }

    /// <summary>Shares current-compilation semantic index and dependency discovery among all cache lookups.</summary>
    private sealed class CompilationFacts
    {
        internal readonly string Identity;
        internal readonly object ProjectGuard;
        internal readonly string UnionUniverse;
        internal readonly Dictionary<string, string> Ownership;
        private readonly INamedTypeSymbol[] _unionCandidates;
        private readonly Dictionary<ISymbol, DirectDependencies> _direct = new Dictionary<ISymbol, DirectDependencies>(SymbolEqualityComparer.Default);
        private readonly Dictionary<object, bool> _priorGuards = new Dictionary<object, bool>();

        /// <summary>Validates each prior project guard at most once for all type and facade entries.</summary>
        /// <param name="previous">The settings captured by a previous frozen type result.</param>
        /// <returns>Whether all settings and global lookup facts are still equivalent.</returns>
        internal bool MatchesProject(object previous)
        {
            if (ReferenceEquals(previous, ProjectGuard)) return true;
            lock (_priorGuards)
            {
                if (!_priorGuards.TryGetValue(previous, out bool equal))
                    _priorGuards.Add(previous, equal = FileSemanticObservationCache.SameProjectGuard(previous, ProjectGuard));
                return equal;
            }
        }

        /// <summary>Projects the existing semantic index down to stable cache guards.</summary>
        /// <param name="compilation">The current compilation.</param>
        internal CompilationFacts(Compilation compilation)
        {
            var analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
            ProjectGuard = FileSemanticObservationCache.For(compilation).ProjectGuard;
            var identity = new StringBuilder(compilation.AssemblyName).Append('\n');
            foreach (var tree in compilation.SyntaxTrees)
            {
                identity.Append(tree.FilePath.Length).Append(':').Append(tree.FilePath).Append('\n');
                var root = tree.GetCompilationUnitRoot();
                foreach (var import in root.Usings.Where(directive => directive.GlobalKeyword.RawKind != 0)) identity.Append(import.ToString()).Append('\n');
                foreach (var attribute in root.AttributeLists) identity.Append(attribute.ToString()).Append('\n');
            }
            foreach (var attribute in compilation.Assembly.GetAttributes()) identity.Append(attribute.ToString()).Append('\n');
            foreach (var declaration in analysis.DeclaredTypes)
                identity.Append(declaration.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)).Append('\n');
            Identity = identity.ToString();

            var ownership = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var type in analysis.FormatterTypes)
            {
                var owner = analysis.GetOwner(type);
                if (owner == null) continue;
                string name = owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (!ownership.TryGetValue(name, out var owned)) ownership.Add(name, owned = new List<string>());
                owned.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
            }
            Ownership = ownership.ToDictionary(pair => pair.Key, pair => string.Join("\n", pair.Value.OrderBy(value => value, StringComparer.Ordinal)), StringComparer.Ordinal);
            _unionCandidates = analysis.FormatterTypes.OfType<INamedTypeSymbol>().ToArray();
            UnionUniverse = string.Join("\n", _unionCandidates.Select(type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ":" +
                type.BaseType?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + ":" +
                string.Join(",", type.Interfaces.Select(@interface => @interface.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))));
        }

        /// <summary>Collects the source-tree closure of a type and its dependent semantic facts.</summary>
        /// <param name="root">The root being analyzed.</param>
        /// <param name="compilation">The current compilation.</param>
        /// <param name="union">Whether project-local derived members affect this root.</param>
        /// <returns>The declaring syntax trees that must survive for reuse.</returns>
        internal SyntaxTree[] Dependencies(ITypeSymbol root, Compilation compilation, bool union)
        {
            var pending = new Stack<ITypeSymbol>();
            var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            var trees = new HashSet<SyntaxTree>();
            pending.Push(root);
            if (union)
            {
                foreach (var candidate in _unionCandidates)
                {
                    bool related = candidate.AllInterfaces.Any(@interface => SymbolEqualityComparer.Default.Equals(@interface.OriginalDefinition, root.OriginalDefinition));
                    for (var parent = candidate.BaseType; parent != null && !related; parent = parent.BaseType)
                        related = SymbolEqualityComparer.Default.Equals(parent.OriginalDefinition, root.OriginalDefinition);
                    if (related) pending.Push(candidate);
                }
            }
            while (pending.Count != 0)
            {
                var type = pending.Pop();
                if (!visited.Add(type)) continue;
                if (type is IArrayTypeSymbol array) { pending.Push(array.ElementType); continue; }
                if (type is IPointerTypeSymbol pointer) { pending.Push(pointer.PointedAtType); continue; }
                if (type is ITypeParameterSymbol parameter)
                {
                    foreach (var constraint in parameter.ConstraintTypes) pending.Push(constraint);
                    continue;
                }
                if (type is not INamedTypeSymbol named) continue;
                foreach (var argument in named.TypeArguments) pending.Push(argument);
                if (!SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition)) { pending.Push(named.OriginalDefinition); continue; }
                if (named.DeclaringSyntaxReferences.Length == 0) continue;
                DirectDependencies direct;
                lock (_direct)
                {
                    if (!_direct.TryGetValue(named, out direct)) _direct.Add(named, direct = GetDirectDependencies(named, compilation));
                }
                trees.UnionWith(direct.Trees);
                foreach (var dependency in direct.Types) pending.Push(dependency);
            }
            return trees.ToArray();
        }

        /// <summary>Finds direct semantic dependencies, including constants and aliases inside attributes.</summary>
        /// <param name="type">The source type whose members are inspected.</param>
        /// <param name="compilation">The compilation that binds its attribute expressions.</param>
        /// <returns>Direct source and type dependencies, memoized by the caller.</returns>
        private static DirectDependencies GetDirectDependencies(INamedTypeSymbol type, Compilation compilation)
        {
            var types = new List<ITypeSymbol>();
            void Add(ITypeSymbol value) { if (value != null) types.Add(value); }
            void AddConstant(TypedConstant constant)
            {
                if (constant.Kind == TypedConstantKind.Type) Add(constant.Value as ITypeSymbol);
                if (constant.Kind == TypedConstantKind.Array && !constant.IsNull) foreach (var value in constant.Values) AddConstant(value);
            }
            void Attributes(ISymbol symbol)
            {
                foreach (var attribute in symbol.GetAttributes())
                {
                    Add(attribute.AttributeClass);
                    foreach (var argument in attribute.ConstructorArguments) AddConstant(argument);
                    foreach (var argument in attribute.NamedArguments) AddConstant(argument.Value);
                    var syntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
                    if (syntax?.ArgumentList == null) continue;
                    var model = compilation.GetSemanticModel(syntax.SyntaxTree);
                    foreach (var identifier in syntax.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>())
                    {
                        var target = model.GetSymbolInfo(identifier).Symbol;
                        Add(target as ITypeSymbol ?? target?.ContainingType);
                    }
                }
            }
            Add(type.BaseType);
            Add(type.ContainingType);
            foreach (var @interface in type.Interfaces) Add(@interface);
            foreach (var parameter in type.TypeParameters) foreach (var constraint in parameter.ConstraintTypes) Add(constraint);
            Attributes(type);
            foreach (var member in type.GetMembers())
            {
                Attributes(member);
                switch (member)
                {
                    case IFieldSymbol field: Add(field.Type); break;
                    case IPropertySymbol property: Add(property.Type); break;
                    case IEventSymbol @event: Add(@event.Type); break;
                    case IMethodSymbol method:
                        Add(method.ReturnType);
                        foreach (var parameter in method.Parameters) { Add(parameter.Type); Attributes(parameter); }
                        foreach (var parameter in method.TypeParameters) foreach (var constraint in parameter.ConstraintTypes) Add(constraint);
                        break;
                }
            }
            return new DirectDependencies(type.DeclaringSyntaxReferences.Select(reference => reference.SyntaxTree).Distinct().ToArray(), types.ToArray());
        }
    }

    /// <summary>Stores short-lived semantic adjacency only inside the current-compilation context.</summary>
    private sealed class DirectDependencies
    {
        internal readonly SyntaxTree[] Trees;
        internal readonly ITypeSymbol[] Types;

        /// <summary>Captures one source type's declaring trees and direct type relationships.</summary>
        /// <param name="trees">The declaring trees.</param>
        /// <param name="types">The directly referenced types.</param>
        internal DirectDependencies(SyntaxTree[] trees, ITypeSymbol[] types) { Trees = trees; Types = types; }
    }
}
