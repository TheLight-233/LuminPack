using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.SourceGenerator;

/// <summary>Reuses unchanged files' type observations as values and rebinds them to the current compilation.</summary>
internal sealed class FileSemanticObservationCache
{
    private static readonly object ProjectsGate = new object();
    private static readonly Dictionary<string, WeakReference<ProjectState>> Projects = new Dictionary<string, WeakReference<ProjectState>>(StringComparer.Ordinal);
    private static readonly ConditionalWeakTable<Compilation, FileSemanticObservationCache> Contexts = new ConditionalWeakTable<Compilation, FileSemanticObservationCache>();
    private static readonly ConditionalWeakTable<SyntaxTree, SyntaxHeader> Headers = new ConditionalWeakTable<SyntaxTree, SyntaxHeader>();
    private readonly Compilation _compilation;
    private readonly ProjectState _project;
    private readonly CurrentTypeResolver _resolver;
    private readonly StableTypeIdentityFactory _factory;
    private readonly Dictionary<ISymbol, DependencyNode> _direct = new Dictionary<ISymbol, DependencyNode>(SymbolEqualityComparer.Default);
    private readonly Dictionary<SyntaxTree, SyntaxTree[]> _observedDependencies = new Dictionary<SyntaxTree, SyntaxTree[]>();
    private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModels = new Dictionary<SyntaxTree, SemanticModel>();

    /// <summary>Acquires a project-local value cache with complete project-wide invalidation guards.</summary>
    /// <param name="compilation">The current compilation.</param>
    private FileSemanticObservationCache(Compilation compilation)
    {
        _compilation = compilation;
        _resolver = new CurrentTypeResolver(compilation);
        _factory = new StableTypeIdentityFactory(compilation);
        var files = compilation.SyntaxTrees.ToArray();
        string global = string.Join("\n", files.Select(tree => Headers.GetValue(tree, current => new SyntaxHeader(current)).GlobalFacts));
        string projectKey = compilation.AssemblyName + "\n" + string.Join("\n", files.Select(tree => tree.FilePath));
        lock (ProjectsGate)
        {
            if (!Projects.TryGetValue(projectKey, out var weak) || !weak.TryGetTarget(out var project) || !project.Matches(compilation, files, global))
            {
                project = new ProjectState(compilation, files, global);
                Projects[projectKey] = new WeakReference<ProjectState>(project);
            }
            _project = project;
            if (Projects.Count > 64)
                foreach (var stale in Projects.Where(pair => !pair.Value.TryGetTarget(out _)).Select(pair => pair.Key).ToArray()) Projects.Remove(stale);
        }
    }

    /// <summary>Gets the current compilation's shared semantic-observation context.</summary>
    /// <param name="compilation">The compilation whose current symbols are required.</param>
    /// <returns>A context containing only current-compilation semantic objects.</returns>
    internal static FileSemanticObservationCache For(Compilation compilation) => Contexts.GetValue(compilation, current => new FileSemanticObservationCache(current));

    /// <summary>Shares source binding across analysis passes within this compilation only.</summary>
    /// <param name="tree">The source tree belonging to the current compilation.</param>
    /// <returns>The same semantic model for repeated observations of this tree.</returns>
    internal SemanticModel GetSemanticModel(SyntaxTree tree)
    {
        lock (_semanticModels)
        {
            if (!_semanticModels.TryGetValue(tree, out var model))
                _semanticModels.Add(tree, model = _compilation.GetSemanticModel(tree));
            return model;
        }
    }

    /// <summary>Gets the symbol-free project guard shared while global semantic settings remain identical.</summary>
    internal object ProjectGuard => _project;

    /// <summary>Returns dependencies already observed by the current compilation's semantic index.</summary>
    /// <param name="tree">The file whose invocation bindings may be retained.</param>
    /// <returns>The complete observed closure, or null when the file has not been analyzed.</returns>
    internal SyntaxTree[] ObservedDependencies(SyntaxTree tree)
    {
        lock (_observedDependencies)
            return _observedDependencies.TryGetValue(tree, out var dependencies) ? dependencies : null;
    }

    /// <summary>Compares project settings by value when retained drivers revisit an earlier compilation.</summary>
    /// <param name="before">The previous symbol-free guard.</param>
    /// <param name="after">The current symbol-free guard.</param>
    /// <returns>Whether every project-wide semantic invalidation fact is equivalent.</returns>
    internal static bool SameProjectGuard(object before, object after) =>
        ReferenceEquals(before, after) || before is ProjectState previous && after is ProjectState current && previous.Equivalent(current);

    /// <summary>Captures source dependencies of unowned formatter bodies before their text is retained.</summary>
    /// <param name="types">The current types whose complete bodies will be emitted.</param>
    /// <returns>The source trees that must remain present for body reuse.</returns>
    internal SyntaxTree[] CaptureDependencies(IEnumerable<ITypeSymbol> types)
    {
        var trees = new HashSet<SyntaxTree>();
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in types) AddDependencies(type, trees, visited);
        return trees.ToArray();
    }

    /// <summary>Reads cached current symbols or re-observes a changed or semantically affected file.</summary>
    /// <param name="tree">The input file.</param>
    /// <param name="isRelevant">The unchanged legacy type relevance predicate.</param>
    /// <returns>Observed types in legacy first-occurrence order.</returns>
    internal FileTypeObservations Read(SyntaxTree tree, Func<ITypeSymbol, bool> isRelevant)
    {
        FileEntry entry;
        lock (_project.Entries)
            _project.Entries.TryGetValue(tree.FilePath, out entry);
        if (entry != null && ReferenceEquals(entry.Tree, tree) && entry.Dependencies.All(_compilation.ContainsSyntaxTree))
        {
            var rebound = entry.Types.Select(_resolver.Resolve).ToArray();
            if (rebound.All(type => type != null))
            {
                Interlocked.Increment(ref IncrementalGenerationMetrics.SemanticFileCacheHits);
                lock (_observedDependencies) _observedDependencies[tree] = entry.Dependencies;
                return new FileTypeObservations(rebound, entry.UsesLuminPack);
            }
        }

        Interlocked.Increment(ref IncrementalGenerationMetrics.SemanticFileObservations);
        var model = GetSemanticModel(tree);
        var types = new List<ITypeSymbol>();
        var seenTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var seenDependencies = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var dependencies = new HashSet<SyntaxTree> { tree };
        bool usesLuminPack = false;
        void AddType(ITypeSymbol type)
        {
            if (type == null) return;
            usesLuminPack |= isRelevant(type);
            AddDependencies(type, dependencies, seenDependencies);
            if (type is not ITypeParameterSymbol && seenTypes.Add(type)) types.Add(type);
        }
        foreach (var node in tree.GetRoot().DescendantNodesAndSelf())
        {
            if (node is TypeSyntax syntax) AddType(model.GetTypeInfo(syntax).Type);
            else if (node is ExpressionSyntax expression)
            {
                var info = model.GetTypeInfo(expression);
                AddType(info.Type);
                AddType(info.ConvertedType);
            }
            if (node is ExpressionSyntax)
            {
                var info = model.GetSymbolInfo(node);
                foreach (var target in info.Symbol == null ? info.CandidateSymbols : ImmutableArray.Create(info.Symbol))
                {
                    // Namespace declarations are protected by the global declaration-name guard.
                    // Their declaring references include every namespace block in the project;
                    // treating those as member dependencies would invalidate every file per edit.
                    if (target is INamespaceSymbol) continue;
                    if (target is IAliasSymbol alias) AddDependencies(alias.Target as ITypeSymbol, dependencies, seenDependencies);
                    AddDependencies(target as ITypeSymbol ?? target.ContainingType, dependencies, seenDependencies);
                    foreach (var declaration in target.DeclaringSyntaxReferences) dependencies.Add(declaration.SyntaxTree);
                }
            }
        }
        var observedDependencies = dependencies.ToArray();
        lock (_observedDependencies) _observedDependencies[tree] = observedDependencies;
        var values = types.Select(_factory.Create).ToArray();
        if (values.All(value => value != null))
        {
            var frozen = new FileEntry(tree, values, observedDependencies, usesLuminPack);
            lock (_project.Entries) _project.Entries[tree.FilePath] = frozen;
        }
        else
        {
            Interlocked.Increment(ref IncrementalGenerationMetrics.SemanticFileFallbacks);
            lock (_project.Entries) _project.Entries.Remove(tree.FilePath);
        }
        return new FileTypeObservations(types, usesLuminPack);
    }

    /// <summary>Finds source dependencies of binding and inferred-type observations.</summary>
    /// <param name="root">The bound or observed type.</param>
    /// <param name="trees">The declaring-tree closure being collected.</param>
    /// <param name="visited">Types already traversed for this file.</param>
    private void AddDependencies(ITypeSymbol root, ISet<SyntaxTree> trees, ISet<ITypeSymbol> visited)
    {
        if (root == null || !visited.Add(root)) return;
        if (root is IArrayTypeSymbol array) { AddDependencies(array.ElementType, trees, visited); return; }
        if (root is IPointerTypeSymbol pointer) { AddDependencies(pointer.PointedAtType, trees, visited); return; }
        if (root is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes) AddDependencies(constraint, trees, visited);
            return;
        }
        if (root is not INamedTypeSymbol named) return;
        foreach (var argument in named.TypeArguments) AddDependencies(argument, trees, visited);
        if (!SymbolEqualityComparer.Default.Equals(named, named.OriginalDefinition)) { AddDependencies(named.OriginalDefinition, trees, visited); return; }
        if (named.DeclaringSyntaxReferences.Length == 0) return;
        DependencyNode direct;
        lock (_direct)
        {
            if (!_direct.TryGetValue(named, out direct)) _direct.Add(named, direct = Direct(named));
        }
        foreach (var tree in direct.Trees) trees.Add(tree);
        foreach (var dependency in direct.Types) AddDependencies(dependency, trees, visited);
    }

    /// <summary>Collects direct source facts needed by method binding and type constraints.</summary>
    /// <param name="type">The current source definition.</param>
    /// <returns>Its direct signature, attribute, inheritance, and storage dependencies.</returns>
    private DependencyNode Direct(INamedTypeSymbol type)
    {
        var types = new List<ITypeSymbol>();
        void Add(ITypeSymbol value) { if (value != null) types.Add(value); }
        void Constant(TypedConstant value)
        {
            if (value.Kind == TypedConstantKind.Type) Add(value.Value as ITypeSymbol);
            if (value.Kind == TypedConstantKind.Array && !value.IsNull) foreach (var element in value.Values) Constant(element);
        }
        void Attributes(ISymbol symbol)
        {
            foreach (var attribute in symbol.GetAttributes())
            {
                Add(attribute.AttributeClass);
                foreach (var argument in attribute.ConstructorArguments) Constant(argument);
                foreach (var argument in attribute.NamedArguments) Constant(argument.Value);
                var syntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
                if (syntax?.ArgumentList == null) continue;
                var model = GetSemanticModel(syntax.SyntaxTree);
                foreach (var identifier in syntax.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var target = model.GetSymbolInfo(identifier).Symbol;
                    Add(target as ITypeSymbol ?? target?.ContainingType);
                }
            }
        }
        Add(type.BaseType); Add(type.ContainingType);
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
        return new DependencyNode(type.DeclaringSyntaxReferences.Select(reference => reference.SyntaxTree).ToArray(), types.ToArray());
    }

    /// <summary>Retains only syntax identities and frozen type descriptions across compilations.</summary>
    private sealed class FileEntry
    {
        internal readonly SyntaxTree Tree;
        internal readonly StableTypeIdentity[] Types;
        internal readonly SyntaxTree[] Dependencies;
        internal readonly bool UsesLuminPack;

        /// <summary>Captures a successfully encoded file summary.</summary>
        /// <param name="tree">The original file identity.</param>
        /// <param name="types">The stable observed type values.</param>
        /// <param name="dependencies">The source dependency identities.</param>
        /// <param name="usesLuminPack">Whether the legacy relevance predicate matched any observed type.</param>
        internal FileEntry(SyntaxTree tree, StableTypeIdentity[] types, SyntaxTree[] dependencies, bool usesLuminPack)
        { Tree = tree; Types = types; Dependencies = dependencies; UsesLuminPack = usesLuminPack; }
    }

    /// <summary>Owns the latest bounded per-file summaries for one project state.</summary>
    private sealed class ProjectState
    {
        private readonly CompilationOptions _options;
        private readonly MetadataReference[] _references;
        private readonly ParseOptions[] _parseOptions;
        private readonly string _globalFacts;
        private readonly string _projectKey;
        internal readonly Dictionary<string, FileEntry> Entries = new Dictionary<string, FileEntry>(StringComparer.Ordinal);

        /// <summary>Captures complete project-wide invalidation settings.</summary>
        /// <param name="compilation">The current compilation.</param>
        /// <param name="trees">Its files in compilation order.</param>
        /// <param name="globalFacts">Global syntax and lookup facts.</param>
        internal ProjectState(Compilation compilation, SyntaxTree[] trees, string globalFacts)
        {
            _options = compilation.Options; _references = compilation.References.ToArray();
            _parseOptions = trees.Select(tree => tree.Options).ToArray(); _globalFacts = globalFacts;
            _projectKey = compilation.AssemblyName + "\n" + string.Join("\n", trees.Select(tree => tree.FilePath));
        }

        /// <summary>Declines cache reuse when a compiler host cannot compare its reference resolver.</summary>
        /// <param name="before">The retained immutable compilation options.</param>
        /// <param name="after">The current immutable compilation options.</param>
        /// <returns>Whether the host can establish equivalent settings.</returns>
        private static bool SameOptions(CompilationOptions before, CompilationOptions after)
        {
            if (ReferenceEquals(before, after)) return true;
            try { return before.Equals(after); }
            catch (NotImplementedException) { return false; }
        }

        /// <summary>Checks all immutable guard values without comparing cached file-entry contents.</summary>
        /// <param name="other">A guard from another observed compilation state.</param>
        /// <returns>Whether per-type dependency checks may proceed under the same project settings.</returns>
        internal bool Equivalent(ProjectState other) => _projectKey == other._projectKey && _globalFacts == other._globalFacts &&
            SameOptions(_options, other._options) && _parseOptions.SequenceEqual(other._parseOptions) &&
            _references.Length == other._references.Length && _references.Zip(other._references, ReferenceEquals).All(equal => equal);

        /// <summary>Checks whether cross-compilation file reuse is still permitted.</summary>
        /// <param name="compilation">The current compilation.</param>
        /// <param name="trees">The current file sequence.</param>
        /// <param name="globalFacts">Current global syntax facts.</param>
        /// <returns>Whether references, options, and global lookup facts match.</returns>
        internal bool Matches(Compilation compilation, SyntaxTree[] trees, string globalFacts) =>
            SameOptions(_options, compilation.Options) && _globalFacts == globalFacts && _parseOptions.SequenceEqual(trees.Select(tree => tree.Options)) &&
            _references.Length == compilation.References.Count() && _references.Zip(compilation.References, ReferenceEquals).All(equal => equal);
    }

    /// <summary>Stores stable global lookup syntax once per physical tree identity.</summary>
    private sealed class SyntaxHeader
    {
        internal readonly string GlobalFacts;

        /// <summary>Projects declarations, global imports, assembly attributes, and extension signatures.</summary>
        /// <param name="tree">The file whose global effects are summarized.</param>
        internal SyntaxHeader(SyntaxTree tree)
        {
            var root = tree.GetCompilationUnitRoot();
            var text = new StringBuilder();
            foreach (var import in root.Usings.Where(directive => directive.GlobalKeyword.RawKind != 0)) text.AppendLine(import.ToString());
            foreach (var attribute in root.AttributeLists) text.AppendLine(attribute.ToString());
            foreach (var declaration in root.DescendantNodes().Where(node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax))
            {
                foreach (var ancestor in declaration.Ancestors().Reverse())
                {
                    if (ancestor is BaseNamespaceDeclarationSyntax space) text.Append(space.Name).Append('.');
                    if (ancestor is TypeDeclarationSyntax owner) text.Append(owner.Identifier.ValueText).Append('`').Append(owner.TypeParameterList?.Parameters.Count ?? 0).Append('+');
                }
                if (declaration is BaseTypeDeclarationSyntax type)
                    text.Append(type.RawKind).Append(':').Append(type.Identifier.ValueText).Append('`').Append((type as TypeDeclarationSyntax)?.TypeParameterList?.Parameters.Count ?? 0).AppendLine();
                else if (declaration is DelegateDeclarationSyntax @delegate)
                    text.Append(@delegate.RawKind).Append(':').Append(@delegate.Identifier.ValueText).Append('`').Append(@delegate.TypeParameterList?.Parameters.Count ?? 0).AppendLine();
            }
            foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method => method.ParameterList.Parameters.FirstOrDefault()?.Modifiers.Any(SyntaxKind.ThisKeyword) == true))
                text.Append(method.Modifiers).Append(method.ReturnType).Append(method.Identifier).Append(method.TypeParameterList).Append(method.ParameterList).Append(string.Join(" ", method.ConstraintClauses)).AppendLine();
            GlobalFacts = text.ToString();
        }
    }

    /// <summary>Stores semantic adjacency only inside its current-compilation context.</summary>
    private sealed class DependencyNode
    {
        internal readonly SyntaxTree[] Trees;
        internal readonly ITypeSymbol[] Types;

        /// <summary>Captures current source trees and direct type relationships.</summary>
        /// <param name="trees">Declaring source trees.</param>
        /// <param name="types">Directly referenced types.</param>
        internal DependencyNode(SyntaxTree[] trees, ITypeSymbol[] types) { Trees = trees; Types = types; }
    }
}

/// <summary>Contains current symbols and the legacy relevance result for one observation step.</summary>
internal sealed class FileTypeObservations
{
    internal readonly IReadOnlyList<ITypeSymbol> Types;
    internal readonly bool UsesLuminPack;

    /// <summary>Creates the current-compilation observation result.</summary>
    /// <param name="types">The currently bound observed types.</param>
    /// <param name="usesLuminPack">Whether any observed type is relevant to LuminPack.</param>
    internal FileTypeObservations(IReadOnlyList<ITypeSymbol> types, bool usesLuminPack) { Types = types; UsesLuminPack = usesLuminPack; }
}
