using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using LuminPack.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LuminPack.Code.Core;

/// <summary>
/// Immutable, compilation-wide semantic index.  Every syntax tree and semantic model is
/// traversed once; formatter generation and registration only consume this cached result.
/// </summary>
internal sealed class CompilationTypeAnalysis
{
    private readonly Dictionary<ITypeSymbol, INamedTypeSymbol> _formatterOwners;
    private readonly Dictionary<ITypeSymbol, INamedTypeSymbol> _layoutOwners;
    private readonly ImmutableArray<ITypeSymbol> _assemblyPackableTypes;

    /// <summary>Initializes a new instance of <c>CompilationTypeAnalysis</c>.</summary>
    /// <param name="formatterTypes">The formatter types used by this operation.</param>
    /// <param name="declaredTypes">The declared types used by this operation.</param>
    /// <param name="packableTypes">The local and assembly-marked packable types.</param>
    /// <param name="assemblyPackableTypes">The types explicitly marked on the consuming assembly.</param>
    /// <param name="formatterOwners">The formatter owners used by this operation.</param>
    /// <param name="layoutOwners">The layout owners used by this operation.</param>
    /// <param name="usesLuminPack">The uses lumin pack used by this operation.</param>
    internal CompilationTypeAnalysis(
        ImmutableArray<ITypeSymbol> formatterTypes,
        ImmutableArray<ProjectTypeData> declaredTypes,
        ImmutableArray<INamedTypeSymbol> packableTypes,
        ImmutableArray<ITypeSymbol> assemblyPackableTypes,
        Dictionary<ITypeSymbol, INamedTypeSymbol> formatterOwners,
        Dictionary<ITypeSymbol, INamedTypeSymbol> layoutOwners,
        bool usesLuminPack)
    {
        FormatterTypes = formatterTypes;
        FormatterTypesByName = formatterTypes.GroupBy(FormatterTypeName.Get, System.StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), System.StringComparer.Ordinal);
        DeclaredTypes = declaredTypes;
        PackableTypes = packableTypes;
        _assemblyPackableTypes = assemblyPackableTypes;
        _formatterOwners = formatterOwners;
        _layoutOwners = layoutOwners;
        UsesLuminPack = usesLuminPack;
    }

    /// <summary>Gets the formatter types.</summary>
    internal ImmutableArray<ITypeSymbol> FormatterTypes { get; }

    /// <summary>Gets the compilation-local canonical formatter index shared by operation projections.</summary>
    internal IReadOnlyDictionary<string, ITypeSymbol> FormatterTypesByName { get; }

    /// <summary>Gets the declared types.</summary>
    internal ImmutableArray<ProjectTypeData> DeclaredTypes { get; }

    /// <summary>Gets every local or assembly-marked packable type definition.</summary>
    internal ImmutableArray<INamedTypeSymbol> PackableTypes { get; }

    /// <summary>Determines whether a type was marked by an assembly-level <c>[LuminPackable(typeof(...))]</c>.</summary>
    /// <param name="type">The type or construction to inspect.</param>
    /// <returns>Whether the type is included by an assembly-level marker.</returns>
    internal bool IsAssemblyPackable(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named) return false;
        foreach (ITypeSymbol target in _assemblyPackableTypes)
        {
            if (target is not INamedTypeSymbol marked) continue;
            if (marked.IsUnboundGenericType
                ? SymbolEqualityComparer.Default.Equals(marked.OriginalDefinition, named.OriginalDefinition)
                : SymbolEqualityComparer.Default.Equals(marked, named))
                return true;
        }
        return false;
    }

    /// <summary>Determines whether a type is marked either directly or by the consuming assembly.</summary>
    /// <param name="type">The type or construction to inspect.</param>
    /// <returns>Whether source generation should treat the type as packable.</returns>
    internal bool IsPackable(ITypeSymbol type)
        => type is INamedTypeSymbol named &&
           (HasPackableAttribute(named) || IsAssemblyPackable(named));

    private static bool HasPackableAttribute(INamedTypeSymbol type)
        => type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);

    /// <summary>Gets the uses lumin pack.</summary>
    internal bool UsesLuminPack { get; }

    /// <summary>Determines whether the supplied value has the owned by classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="owner">The owner used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal bool IsOwnedBy(ITypeSymbol type, INamedTypeSymbol owner)
        => _formatterOwners.TryGetValue(type, out INamedTypeSymbol actual) &&
           SymbolEqualityComparer.Default.Equals(actual, owner);

    /// <summary>Determines whether owner are present.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal bool HasOwner(ITypeSymbol type) => _formatterOwners.ContainsKey(type);

    /// <summary>Gets the packable type that owns the formatter graph containing <paramref name="type"/>, if any.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    internal INamedTypeSymbol? GetOwner(ITypeSymbol type)
        => _formatterOwners.TryGetValue(type, out INamedTypeSymbol? owner) ? owner : null;

    /// <summary>Determines whether the supplied value has the layout owned by classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="owner">The owner used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    internal bool IsLayoutOwnedBy(ITypeSymbol type, INamedTypeSymbol owner)
        => _layoutOwners.TryGetValue(NormalizeLayoutType(type), out INamedTypeSymbol actual) &&
           SymbolEqualityComparer.Default.Equals(actual, owner);

    /// <summary>Performs the normalize layout type step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>The result of the operation.</returns>
    private static ITypeSymbol NormalizeLayoutType(ITypeSymbol type)
        => type is INamedTypeSymbol named ? named.OriginalDefinition : type;
}

/// <summary>Reusable structural facts for one source-declared type.</summary>
internal sealed class ProjectTypeData
{
    private readonly Lazy<ImmutableArray<IFieldSymbol>> _fields;
    private readonly Lazy<ImmutableArray<IPropertySymbol>> _properties;
    /// <summary>Initializes a new instance of <c>ProjectTypeData</c>.</summary>
    /// <param name="symbol">The type or symbol to analyze.</param>
    internal ProjectTypeData(INamedTypeSymbol symbol)
    {
        Symbol = symbol;
        BaseType = symbol.BaseType;
        Interfaces = symbol.Interfaces;
        _fields = new Lazy<ImmutableArray<IFieldSymbol>>(() =>
        {
            System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.ProjectMemberInspections);
            return symbol.GetMembers().OfType<IFieldSymbol>()
                .Where(static field => !field.IsStatic && !field.IsImplicitlyDeclared).ToImmutableArray();
        });
        _properties = new Lazy<ImmutableArray<IPropertySymbol>>(() =>
        {
            System.Threading.Interlocked.Increment(ref IncrementalGenerationMetrics.ProjectMemberInspections);
            return symbol.GetMembers().OfType<IPropertySymbol>()
                .Where(static property => !property.IsStatic && !property.IsImplicitlyDeclared).ToImmutableArray();
        });
    }

    /// <summary>Gets the symbol.</summary>
    internal INamedTypeSymbol Symbol { get; }
    /// <summary>Gets the base type.</summary>
    internal INamedTypeSymbol BaseType { get; }
    /// <summary>Gets the interfaces.</summary>
    internal ImmutableArray<INamedTypeSymbol> Interfaces { get; }
    /// <summary>Gets the fields.</summary>
    internal ImmutableArray<IFieldSymbol> Fields => _fields.Value;
    /// <summary>Gets the properties.</summary>
    internal ImmutableArray<IPropertySymbol> Properties => _properties.Value;
}

/// <summary>Provides compilation type analysis cache functionality.</summary>
internal static class CompilationTypeAnalysisCache
{
    /// <summary>Provides holder functionality.</summary>
    private sealed class Holder
    {
        /// <summary>Initializes a new instance of <c>Holder</c>.</summary>
        /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
        internal Holder(Compilation compilation)
        {
            Value = new Lazy<CompilationTypeAnalysis>(
                () => Analyze(compilation),
                System.Threading.LazyThreadSafetyMode.ExecutionAndPublication);
        }

        /// <summary>Gets the value.</summary>
        internal Lazy<CompilationTypeAnalysis> Value { get; }
    }

    private static readonly ConditionalWeakTable<Compilation, Holder> Cache = new();

    /// <summary>Obtains the or create for the supplied input.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    internal static CompilationTypeAnalysis GetOrCreate(Compilation compilation)
        => Cache.GetValue(compilation, static value => new Holder(value)).Value.Value;

    /// <summary>Analyzes  and records the resulting metadata or diagnostics.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    private static CompilationTypeAnalysis Analyze(Compilation compilation)
    {
        var formatterTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var declaredTypes = new List<ProjectTypeData>();
        var packableTypes = new List<INamedTypeSymbol>();
        var assemblyPackableTypes = GetAssemblyPackableTypes(compilation);
        bool usesLuminPack = false;

        CollectDeclaredTypes(compilation.Assembly.GlobalNamespace, declaredTypes, packableTypes);
        foreach (ITypeSymbol markedType in assemblyPackableTypes)
        {
            if (markedType is INamedTypeSymbol named && !packableTypes.Contains(named, SymbolEqualityComparer.Default))
                packableTypes.Add(named);
        }

        var observations = FileSemanticObservationCache.For(compilation);
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            var observed = observations.Read(tree, type => IsLuminPackRelevantType(type) || IsAssemblyPackableType(type, assemblyPackableTypes));
            usesLuminPack |= observed.UsesLuminPack;
            foreach (var type in observed.Types) AddTypeGraph(type, formatterTypes);
        }

        foreach (INamedTypeSymbol packable in packableTypes)
        {
            AddTypeGraph(packable, formatterTypes);
        }

        var owners = new Dictionary<ITypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
        INamedTypeSymbol[] orderedPackables = packableTypes.OrderBy(
                     static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                     StringComparer.Ordinal).ToArray();
        foreach (INamedTypeSymbol packable in orderedPackables)
        {
            var contribution = PackableGraphCache.GetOrBuild(compilation, packable, (owned, analyzed) =>
                AddPackableGraph(packable, owned, analyzed, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), assemblyPackableTypes));
            // Reproduce legacy first-occurrence insertion while retaining every unowned member type.
            foreach (ITypeSymbol type in contribution.Analyzed) AddTypeGraph(type, formatterTypes);
            foreach (ITypeSymbol type in contribution.Owned)
            {
                if (!owners.ContainsKey(type))
                {
                    owners.Add(type, packable);
                }
            }
        }

        // Open packable definitions describe the shared formatter methods. Observed closed
        // constructions also need their substituted member formatters (for example List<string>
        // inside Envelope<string>), which cannot acquire an owner from an open List<T> field.
        var closedPackables = formatterTypes.OfType<INamedTypeSymbol>().Where(type => type.IsGenericType &&
            !LuminPackExtensionGenerator.ContainsTypeParameter(type) && IsPackableType(type, assemblyPackableTypes)).ToArray();
        foreach (var packable in closedPackables)
        {
            var contribution = PackableGraphCache.GetOrBuild(compilation, packable, (owned, analyzed) =>
                AddPackableGraph(packable, owned, analyzed, new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default), assemblyPackableTypes));
            foreach (var type in contribution.Analyzed) AddTypeGraph(type, formatterTypes);
        }

        // Every packable owns its own local marshal layout.  Unannotated base layouts are
        // assigned deterministically to the first derived packable which needs them.
        var layoutOwners = new Dictionary<ITypeSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (INamedTypeSymbol packable in orderedPackables)
        {
            layoutOwners[packable.OriginalDefinition] = packable;
        }
        foreach (INamedTypeSymbol packable in orderedPackables)
        {
            for (INamedTypeSymbol current = packable.BaseType;
                 current is not null && current.SpecialType != SpecialType.System_Object;
                 current = current.BaseType)
            {
                INamedTypeSymbol layoutType = current.OriginalDefinition;
                if (!layoutOwners.ContainsKey(layoutType))
                {
                    layoutOwners.Add(layoutType, packable);
                }
            }
        }

        return new CompilationTypeAnalysis(
            formatterTypes
                .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ToImmutableArray(),
            declaredTypes
                .OrderBy(static data => data.Symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ToImmutableArray(),
            packableTypes.ToImmutableArray(),
            assemblyPackableTypes,
            owners,
            layoutOwners,
            usesLuminPack);
    }

    /// <summary>Determines whether the supplied value has the lumin pack relevant type classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsLuminPackRelevantType(ITypeSymbol type)
    {
        if (type is null) return false;
        if (type.ContainingAssembly?.Identity.Name == "LuminPack") return true;

        return type switch
        {
            IArrayTypeSymbol array => IsLuminPackRelevantType(array.ElementType),
            IPointerTypeSymbol pointer => IsLuminPackRelevantType(pointer.PointedAtType),
            INamedTypeSymbol named => HasPackableAttribute(named) ||
                                      named.TypeArguments.Any(IsLuminPackRelevantType),
            _ => false
        };
    }

    /// <summary>Performs the collect declared types step for the current operation.</summary>
    /// <param name="root">The root used by this operation.</param>
    /// <param name="declaredTypes">The declared types used by this operation.</param>
    /// <param name="packableTypes">The packable types used by this operation.</param>
    private static void CollectDeclaredTypes(
        INamespaceSymbol root,
        ICollection<ProjectTypeData> declaredTypes,
        ICollection<INamedTypeSymbol> packableTypes)
    {
        foreach (INamespaceSymbol child in root.GetNamespaceMembers())
        {
            CollectDeclaredTypes(child, declaredTypes, packableTypes);
        }

        foreach (INamedTypeSymbol type in root.GetTypeMembers())
        {
            CollectDeclaredType(type, declaredTypes, packableTypes);
        }
    }

    /// <summary>Performs the collect declared type step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="declaredTypes">The declared types used by this operation.</param>
    /// <param name="packableTypes">The packable types used by this operation.</param>
    private static void CollectDeclaredType(
        INamedTypeSymbol type,
        ICollection<ProjectTypeData> declaredTypes,
        ICollection<INamedTypeSymbol> packableTypes)
    {
        declaredTypes.Add(new ProjectTypeData(type));
        // Custom types keep their owner role so Minimal-tier reachability still expands
        // their member graphs (their field types get shared formatter extensions); the
        // extension emission itself filters Custom types out (see
        // LuminPackExtensionGenerator.IsCustomGeneratorType).
        if (HasPackableAttribute(type))
        {
            packableTypes.Add(type);
        }

        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            CollectDeclaredType(nested, declaredTypes, packableTypes);
        }
    }

    /// <summary>
    /// Collects the formatter ownership graph of one packable type.
    /// ownedTypes must match the members actually serialized by the per-type generator;
    /// Otherwise a type can acquire an owner without receiving formatter extensions, causing compilation errors.
    /// Serialization rules, matching LuminPackSourceGenerator:
    ///   Fields must be instance, explicitly declared, not ignored, and either included or publicly, internally, or protected-internally accessible.
    ///   Properties must be instance auto-properties, not ignored, and either explicitly included or publicly, internally, or protected-internally accessible.
    /// Ignored members, non-included private members, and computed read-only properties do not acquire ownership;
    /// They remain in analyzedTypes so the shared generator supplies the missing formatter extensions;
    /// Retains all necessary formatter candidates, bounded by IsStaticFormatterCandidate.
    /// </summary>
    /// <param name="packable">The packable used by this operation.</param>
    /// <param name="ownedTypes">The owned types used by this operation.</param>
    /// <param name="analyzedTypes">The analyzed types used by this operation.</param>
    /// <param name="visited">The visited used by this operation.</param>
    /// <param name="assemblyPackableTypes">The types explicitly marked on the consuming assembly.</param>
    private static void AddPackableGraph(
        INamedTypeSymbol packable,
        ISet<ITypeSymbol> ownedTypes,
        ISet<ITypeSymbol> analyzedTypes,
        ISet<ITypeSymbol> visited,
        ImmutableArray<ITypeSymbol> assemblyPackableTypes)
    {
        if (!visited.Add(packable)) return;
        AddTypeGraph(packable, ownedTypes);
        AddTypeGraph(packable, analyzedTypes);

        foreach (ISymbol member in packable.GetMembers())
        {
            switch (member)
            {
                case IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field:
                    // Serialize fields that are not ignored and are included or public.
                    bool fieldSerialized = !TypeMetaChecker.TryCheckIgnoreAttribute(field) &&
                        (TypeMetaChecker.TryCheckIncludeAttribute(field) || IsPubliclyAccessible(field.DeclaredAccessibility));
                    AddMemberGraph(field.Type, ownedTypes, analyzedTypes, fieldSerialized);
                    break;

                case IPropertySymbol property
                    when !property.IsStatic && (LuminPackSourceGenerator.IsAutoProperty(property) ||
                        IsAssemblyPackableType(packable, assemblyPackableTypes) && IsPublicSettableProperty(property)):
                    // Serialize auto-properties that are not ignored and are included or public.
                    bool propSerialized = !TypeMetaChecker.TryCheckIgnoreAttribute(property) &&
                        (TypeMetaChecker.TryCheckIncludeAttribute(property) || IsPubliclyAccessible(property.DeclaredAccessibility));
                    AddMemberGraph(property.Type, ownedTypes, analyzedTypes, propSerialized);
                    break;

                case IPropertySymbol { IsStatic: false } computedProperty:
                    // Computed read-only properties have no backing storage, are not serialized, and do not acquire ownership.
                    // Retain the type in analysis so the shared generator can provide its formatter.
                    AddTypeGraph(computedProperty.Type, analyzedTypes);
                    break;
            }
        }

        if (packable.BaseType is { SpecialType: not SpecialType.System_Object } baseType &&
            IsPackableType(baseType, assemblyPackableTypes))
        {
            AddPackableGraph(baseType, ownedTypes, analyzedTypes, visited, assemblyPackableTypes);
        }
    }

    /// <summary>
    /// Serialized member types acquire an owner; other member types remain in shared analysis for fallback generation.
    /// Retain types in analyzedTypes regardless of ownership so required extensions are emitted.
    /// </summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="ownedTypes">The owned types used by this operation.</param>
    /// <param name="analyzedTypes">The analyzed types used by this operation.</param>
    /// <param name="serialized">The serialized used by this operation.</param>
    private static void AddMemberGraph(
        ITypeSymbol type, ISet<ITypeSymbol> ownedTypes, ISet<ITypeSymbol> analyzedTypes, bool serialized)
    {
        if (serialized) AddTypeGraph(type, ownedTypes);
        AddTypeGraph(type, analyzedTypes);
    }

    /// <summary>Matches the generator's serializable accessibility rules, excluding private, private protected, and protected members.</summary>
    /// <param name="accessibility">The accessibility used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsPubliclyAccessible(Accessibility accessibility)
        => accessibility is not (
            Accessibility.Private or
            Accessibility.ProtectedAndInternal or
            Accessibility.Protected);

    /// <summary>Determines whether a metadata property can be read and populated from the consuming assembly.</summary>
    /// <param name="property">The property used by this operation.</param>
    /// <returns>Whether the property has public accessors for both serialization directions.</returns>
    private static bool IsPublicSettableProperty(IPropertySymbol property)
        => property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
           property.SetMethod?.DeclaredAccessibility == Accessibility.Public;

    /// <summary>Performs the add type graph step for the current operation.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <param name="types">The types used by this operation.</param>
    private static void AddTypeGraph(ITypeSymbol type, ISet<ITypeSymbol> types)
    {
        if (type is null || type.TypeKind == TypeKind.Error || type is ITypeParameterSymbol) return;

        if (!types.Add(type)) return;

        switch (type)
        {
            case IArrayTypeSymbol array:
                AddTypeGraph(array.ElementType, types);
                break;
            case INamedTypeSymbol named:
                if (named.ContainingType is not null)
                {
                    AddTypeGraph(named.ContainingType, types);
                }
                foreach (ITypeSymbol argument in named.TypeArguments)
                {
                    AddTypeGraph(argument, types);
                }
                break;
        }
    }

    /// <summary>Determines whether packable attribute are present.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool HasPackableAttribute(INamedTypeSymbol type)
        => type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);

    /// <summary>Reads external type roots declared by assembly-level <c>[LuminPackable(typeof(...))]</c> attributes.</summary>
    /// <param name="compilation">The consuming compilation whose assembly attributes are inspected.</param>
    /// <returns>The distinct referenced types marked for generation.</returns>
    private static ImmutableArray<ITypeSymbol> GetAssemblyPackableTypes(Compilation compilation)
    {
        var types = ImmutableArray.CreateBuilder<ITypeSymbol>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE ||
                attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol type)
                continue;

            if (seen.Add(type)) types.Add(type);
        }
        return types.ToImmutable();
    }

    /// <summary>Determines whether a type matches one of the consuming assembly's explicit roots.</summary>
    /// <param name="type">The type or construction to inspect.</param>
    /// <param name="assemblyPackableTypes">The assembly-level roots.</param>
    /// <returns>Whether the type is marked by the consuming assembly.</returns>
    private static bool IsAssemblyPackableType(ITypeSymbol type, ImmutableArray<ITypeSymbol> assemblyPackableTypes)
    {
        if (type is not INamedTypeSymbol named) return false;
        foreach (ITypeSymbol target in assemblyPackableTypes)
        {
            if (target is not INamedTypeSymbol marked) continue;
            if (marked.IsUnboundGenericType
                ? SymbolEqualityComparer.Default.Equals(marked.OriginalDefinition, named.OriginalDefinition)
                : SymbolEqualityComparer.Default.Equals(marked, named))
                return true;
        }
        return false;
    }

    /// <summary>Determines whether a type is annotated directly or included by an assembly root.</summary>
    /// <param name="type">The type or construction to inspect.</param>
    /// <param name="assemblyPackableTypes">The assembly-level roots.</param>
    /// <returns>Whether the type should be analyzed as packable.</returns>
    private static bool IsPackableType(ITypeSymbol type, ImmutableArray<ITypeSymbol> assemblyPackableTypes)
        => type is INamedTypeSymbol named &&
           (HasPackableAttribute(named) || IsAssemblyPackableType(named, assemblyPackableTypes));
}
