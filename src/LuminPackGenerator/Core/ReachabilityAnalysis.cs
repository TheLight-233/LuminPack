using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using LuminPack.SourceGenerator;

namespace LuminPack.Code.Core;

#nullable enable

/// <summary>Source-generator emission tier, from most conservative to most aggressive.</summary>
public enum LuminPackGenerationTier
{
    /// <summary>Emit formatters for every type observed in the project.</summary>
    Full = 0,

    /// <summary>Reachable + keep public non-packable types (library-safe).</summary>
    Medium = 1,

    /// <summary>Reachable from <c>[LuminPackable]</c> member graphs and serializer call sites.</summary>
    Light = 2,

    /// <summary>Only types actually passed to <c>LuminPackSerializer.*</c> call sites (unused packables pruned).</summary>
    Minimal = 3,
}

internal static class LuminPackGenerationTierResolver
{
    private const string GeneratorOptionsAttr = "LuminPack.Attribute.LuminPackGeneratorOptionsAttribute";
    private const string GenerationModeProperty = "build_property.LuminPackGenerationMode";

    /// <summary>Maps the MSBuild property value (including legacy names) to a tier.</summary>
    internal static LuminPackGenerationTier FromProperty(string? value) => value switch
    {
        "Medium" or "Compatible" or "PrunePublic" => LuminPackGenerationTier.Medium,
        "Light" or "Reachable" or "Prune" => LuminPackGenerationTier.Light,
        "Minimal" => LuminPackGenerationTier.Minimal,
        _ => LuminPackGenerationTier.Full,
    };

    /// <summary>Maps an assembly-level <c>[LuminPackGeneratorOptions]</c> attribute to a tier.</summary>
    internal static LuminPackGenerationTier FromAssembly(Compilation compilation)
    {
        foreach (var attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != GeneratorOptionsAttr)
            {
                continue;
            }

            foreach (var argument in attribute.ConstructorArguments)
            {
                int value = argument.Value switch
                {
                    byte b => b,
                    int i => i,
                    _ => -1
                };
                return value switch
                {
                    1 => LuminPackGenerationTier.Medium,
                    2 => LuminPackGenerationTier.Light,
                    3 => LuminPackGenerationTier.Minimal,
                    _ => LuminPackGenerationTier.Full
                };
            }
        }

        return LuminPackGenerationTier.Full;
    }
}

/// <summary>
/// Prune-mode reachability.  Computes the set of no-owner types for which concrete formatter
/// extensions must be emitted: payload types observed at <c>LuminPackSerializer.*</c> call sites,
/// transitively closed through concrete instantiations of generic wrapper methods and generic
/// containing types (e.g. <c>void Save&lt;T&gt;(T x) =&gt; Serialize(x)</c>), then closed over the
/// type-argument graph of every seed.
/// </summary>
/// <remarks>
/// Owned types (the member graphs of <c>[LuminPackable]</c> types) are always emitted by the
/// per-type generator, so this analysis only produces the no-owner complement.  Types which cannot
/// be statically proven reachable from this compilation (reflection, cross-assembly, dynamic call
/// sites) are intentionally excluded; the generic dispatch then throws the normal
/// "no source generated formatter" exception for them, which is the same contract full mode exposes
/// for types that are not formatter candidates.
/// </remarks>
public sealed class ReachabilityAnalysis
{
    private readonly ImmutableArray<ITypeSymbol> _reachableNoOwnerTypes;
    private readonly ImmutableArray<ITypeSymbol> _emittedTypes;
    private readonly ImmutableHashSet<ISymbol> _emittedPackableDefinitions;
    private readonly LuminPackGenerationTier _tier;

    internal ReachabilityAnalysis(
        LuminPackGenerationTier tier,
        ImmutableArray<ITypeSymbol> reachableNoOwnerTypes,
        ImmutableArray<ITypeSymbol> emittedTypes,
        ImmutableHashSet<ISymbol> emittedPackableDefinitions)
    {
        _tier = tier;
        _reachableNoOwnerTypes = reachableNoOwnerTypes;
        _emittedTypes = emittedTypes;
        _emittedPackableDefinitions = emittedPackableDefinitions;
    }

    /// <summary>No-owner formatter types that are reachable from serializer call sites.</summary>
    internal ImmutableArray<ITypeSymbol> ReachableNoOwnerTypes => _reachableNoOwnerTypes;

    /// <summary>
    /// Every type that receives a concrete formatter extension in this compilation: the emitted
    /// owned graphs plus the reachable no-owner complement.  The generic dispatch and the serializer
    /// entry overloads must reference exactly this set.
    /// </summary>
    internal ImmutableArray<ITypeSymbol> EmittedTypes => _emittedTypes;

    /// <summary>
    /// Whether a <c>[LuminPackable]</c> type should be emitted by the per-type generator.
    /// <see cref="LuminPackGenerationTier.Minimal"/> skips packables that are not reachable from any
    /// serializer call site; the other tiers always emit every packable.
    /// </summary>
    internal bool ShouldEmitPackable(ITypeSymbol packable)
        => _tier != LuminPackGenerationTier.Minimal ||
           _emittedPackableDefinitions.Contains(packable.OriginalDefinition);
}

internal static class ReachabilityAnalysisCache
{
    private const string SerializerTypeName = "LuminPack.LuminPackSerializer";
    private const string UnionAttributeName = "LuminPack.Attribute.LuminPackUnionAttribute";

    private sealed class Holder
    {
        private readonly Lazy<ReachabilityAnalysis>[] _byTier = new Lazy<ReachabilityAnalysis>[4];

        internal ReachabilityAnalysis Get(Compilation compilation, LuminPackGenerationTier tier)
        {
            int index = (int)tier;
            ref Lazy<ReachabilityAnalysis>? lazy = ref _byTier[index];
            if (lazy is null)
            {
                lazy = new Lazy<ReachabilityAnalysis>(
                    () => Analyze(compilation, tier),
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }
            return lazy.Value;
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Compilation, Holder> Cache = new();

    internal static ReachabilityAnalysis GetOrCreate(Compilation compilation, LuminPackGenerationTier tier)
        => Cache.GetValue(compilation, static value => new Holder()).Get(compilation, tier);

    private static ReachabilityAnalysis Analyze(Compilation compilation, LuminPackGenerationTier tier)
    {
        CompilationTypeAnalysis analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);

        // Resolve every invocation in the project once.  Semantic models are expensive; slot
        // matching and substitution then operate over this cheap list.
        var invocations = new List<(InvocationExpressionSyntax Syntax, IMethodSymbol? Method)>();
        foreach (SyntaxTree tree in compilation.SyntaxTrees)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            foreach (InvocationExpressionSyntax invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                invocations.Add((invocation, model.GetSymbolInfo(invocation).Symbol as IMethodSymbol));
            }
        }

        var seeds = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var pending = new Queue<TypeParamSlot>();
        var visited = new HashSet<TypeParamSlot>();

        foreach ((_, IMethodSymbol? method) in invocations)
        {
            if (method is null)
            {
                continue;
            }

            ITypeSymbol? payload = null;
            string? containingName = method.ContainingType?.ToDisplayString();

            if (containingName == SerializerTypeName)
            {
                payload = method.Parameters.FirstOrDefault(static p => p.Name == "value")?.Type;
                if (payload is null)
                {
                    payload = method.IsGenericMethod && method.TypeArguments.Length != 0
                        ? method.TypeArguments[0]
                        : null;
                }
            }
            else if (IsGeneratedFormatterMethod(method))
            {
                // Direct calls to generated formatter extensions (e.g.
                // LuminPackExtensions_<asm>.WriteValue(ref writer, in value)) are a documented
                // surface.  Treat their payload type as a seed so prune mode keeps them reachable.
                payload = method.Parameters.FirstOrDefault(static p => p.Name == "value")?.Type
                    ?? method.Parameters.FirstOrDefault()?.Type;
            }

            if (payload is null)
            {
                continue;
            }

            CollectPayload(compilation, payload, seeds, pending);
        }

        while (pending.Count != 0)
        {
            TypeParamSlot slot = pending.Dequeue();
            if (!visited.Add(slot))
            {
                continue;
            }

            foreach ((_, IMethodSymbol? method) in invocations)
            {
                if (method is null || !slot.Matches(method))
                {
                    continue;
                }

                ITypeSymbol substituted = Substitute(compilation, slot.PayloadType, BuildMap(slot, method));
                CollectPayload(compilation, substituted, seeds, pending);
            }
        }

        // Candidate set closed over seed graphs (including union member expansion).
        var candidates = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (ITypeSymbol seed in seeds)
        {
            ExpandType(compilation, seed, candidates, analysis);
        }

        // Which [LuminPackable] types are emitted by the per-type generator?
        var emittedPackableDefinitions = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var finalCandidates = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (ITypeSymbol type in candidates)
        {
            finalCandidates.Add(type);
        }

        if (tier == LuminPackGenerationTier.Minimal)
        {
            // Minimal: seed the emitted set from call-site reachable packables, then close over the
            // member graphs until stable.  A packable that appears as a serialized member of an
            // emitted packable must be emitted by its OWN per-type generator (the parent generator
            // does not emit a concrete formatter for user packable members), otherwise the generic
            // dispatch case for it would fall back to the generic method and recurse.  Base packable
            // chains are kept too, because subclass formatters serialize inherited members.
            bool changed = true;
            while (changed)
            {
                changed = false;

                foreach (ITypeSymbol ownedType in analysis.FormatterTypes)
                {
                    if (analysis.GetOwner(ownedType) is { } owner &&
                        emittedPackableDefinitions.Contains(owner.OriginalDefinition))
                    {
                        ExpandType(compilation, ownedType, finalCandidates, analysis);
                    }
                }

                foreach (ITypeSymbol type in finalCandidates)
                {
                    if (type is INamedTypeSymbol named &&
                        HasPackableAttribute(named.OriginalDefinition) &&
                        emittedPackableDefinitions.Add(named.OriginalDefinition))
                    {
                        changed = true;
                    }
                }

                AddBasePackableChain(compilation, emittedPackableDefinitions);
            }
        }
        else
        {
            foreach (ProjectTypeData declared in analysis.DeclaredTypes)
            {
                if (HasPackableAttribute(declared.Symbol))
                {
                    emittedPackableDefinitions.Add(declared.Symbol.OriginalDefinition);
                }
            }

            // Expand the emitted packables' serialized member graphs.  The per-type generator emits
            // formatter extensions for those owned types, so every no-owner member of an emitted
            // graph must also be reachable to keep the dispatch set aligned with the emitted set.
            foreach (ITypeSymbol ownedType in analysis.FormatterTypes.Where(analysis.HasOwner))
            {
                if (analysis.GetOwner(ownedType) is { } owner &&
                    emittedPackableDefinitions.Contains(owner.OriginalDefinition))
                {
                    ExpandType(compilation, ownedType, finalCandidates, analysis);
                }
            }
        }

        var reachableNoOwner = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (ITypeSymbol type in finalCandidates)
        {
            if (IsReachableNoOwner(type, analysis))
            {
                reachableNoOwner.Add(type);
            }
        }

        if (tier == LuminPackGenerationTier.Medium)
        {
            // Medium tier: keep public non-packable types even when unreachable, because public
            // types are conservative API surfaces that may be serialized across assembly boundaries
            // or via reflection.
            foreach (ITypeSymbol type in analysis.FormatterTypes)
            {
                if (analysis.HasOwner(type) ||
                    LuminPackExtensionGenerator.ContainsTypeParameter(type) ||
                    !LuminPackExtensionGenerator.IsAotVisible(type) ||
                    !LuminPackExtensionGenerator.IsStaticFormatterCandidate(type))
                {
                    continue;
                }

                if (IsPubliclyAccessibleType(type))
                {
                    reachableNoOwner.Add(type);
                }
            }
        }

        ImmutableArray<ITypeSymbol> orderedReachableNoOwner = reachableNoOwner
            .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToImmutableArray();

        // EmittedTypes for the dispatch: emitted owned graphs plus the reachable no-owner complement.
        var emitted = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        foreach (ITypeSymbol type in analysis.FormatterTypes)
        {
            if (analysis.GetOwner(type) is { } owner &&
                emittedPackableDefinitions.Contains(owner.OriginalDefinition))
            {
                emitted.Add(type);
            }
        }
        foreach (ITypeSymbol type in orderedReachableNoOwner)
        {
            emitted.Add(type);
        }

        return new ReachabilityAnalysis(
            tier,
            orderedReachableNoOwner,
            emitted
                .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
                .ToImmutableArray(),
            emittedPackableDefinitions.ToImmutableHashSet(SymbolEqualityComparer.Default));
    }

    private static bool IsReachableNoOwner(ITypeSymbol type, CompilationTypeAnalysis analysis)
        => !analysis.HasOwner(type) &&
           !LuminPackExtensionGenerator.ContainsTypeParameter(type) &&
           LuminPackExtensionGenerator.IsAotVisible(type) &&
           LuminPackExtensionGenerator.IsStaticFormatterCandidate(type);

    private static bool IsPubliclyAccessibleType(ITypeSymbol type)
    {
        INamedTypeSymbol? named = type switch
        {
            IArrayTypeSymbol array => array.ElementType as INamedTypeSymbol,
            INamedTypeSymbol n => n,
            _ => null
        };
        if (named is null)
        {
            return false;
        }

        for (INamedTypeSymbol? current = named; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.NotApplicable))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Adds a type to the candidate set and recursively expands its type-argument graph (array
    /// elements, generic arguments, containing types) and, when it is a <c>[LuminPackable]</c> type,
    /// its serialized member graphs and union members.
    /// </summary>
    private static void ExpandType(Compilation compilation, ITypeSymbol type, ISet<ITypeSymbol> types, CompilationTypeAnalysis analysis)
    {
        if (type is null || type.TypeKind == TypeKind.Error || type is ITypeParameterSymbol)
        {
            return;
        }

        if (!types.Add(type))
        {
            return;
        }

        switch (type)
        {
            case IArrayTypeSymbol array:
                ExpandType(compilation, array.ElementType, types, analysis);
                break;
            case INamedTypeSymbol named:
                if (named.ContainingType is not null)
                {
                    ExpandType(compilation, named.ContainingType, types, analysis);
                }
                foreach (ITypeSymbol argument in named.TypeArguments)
                {
                    ExpandType(compilation, argument, types, analysis);
                }

                if (HasPackableAttribute(named.OriginalDefinition))
                {
                    ExpandPackableMembers(compilation, named, types, analysis);
                    ExpandUnionMembers(compilation, named, types, analysis);
                }
                break;
        }
    }

    /// <summary>
    /// Expands a packable union root's member types so the generated union dispatch can reference
    /// them: the explicit <c>[LuminPackUnion]</c> attributes and the auto-discovered derived types.
    /// This keeps union members reachable in <see cref="LuminPackGenerationTier.Minimal"/> even when
    /// they are never individually passed to a serializer call.
    /// </summary>
    private static void ExpandUnionMembers(Compilation compilation, INamedTypeSymbol packable, ISet<ITypeSymbol> types, CompilationTypeAnalysis analysis)
    {
        bool isLuminUnion = packable.TypeKind == TypeKind.Interface ||
                            packable.IsAbstract ||
                            packable.GetAttributes().Any(static attribute =>
                                attribute.AttributeClass?.ToDisplayString() == UnionAttributeName);

        if (isLuminUnion)
        {
            // Explicit [LuminPackUnion] members.
            foreach (var attribute in packable.GetAttributes())
            {
                if (attribute.AttributeClass?.ToDisplayString() != UnionAttributeName)
                {
                    continue;
                }
                foreach (var argument in attribute.ConstructorArguments)
                {
                    if (argument.Value is INamedTypeSymbol member)
                    {
                        ExpandType(compilation, member, types, analysis);
                    }
                }
            }

            // Auto-discovered derived types declared in this compilation.
            INamedTypeSymbol baseDef = packable.OriginalDefinition;
            foreach (ProjectTypeData declared in analysis.DeclaredTypes)
            {
                if (!HasPackableAttribute(declared.Symbol))
                {
                    continue;
                }
                if (IsDerivedFrom(declared.Symbol, baseDef))
                {
                    ExpandType(compilation, declared.Symbol, types, analysis);
                }
            }
            return;
        }

        // .NET 11 / C# 15 union declarations: expand their case types so pruning modes keep the
        // case formatters reachable.  The synthesized [Union] attribute may not be visible to the
        // generator for `union` keyword declarations, so also detect the union keyword in syntax.
        if (IsCs11Union(packable, compilation))
        {
            foreach (ITypeSymbol caseType in CollectCs11UnionCaseTypes(compilation, packable))
            {
                ExpandType(compilation, caseType, types, analysis);
            }
        }
    }

    private static bool IsCs11Union(INamedTypeSymbol type, Compilation compilation)
    {
        INamedTypeSymbol? unionAttribute = compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.UnionAttribute");
        if (unionAttribute is not null &&
            type.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, unionAttribute)))
        {
            return true;
        }

        foreach (SyntaxReference reference in type.DeclaringSyntaxReferences)
        {
            SyntaxNode node = reference.GetSyntax();
            if (node is TypeDeclarationSyntax tds &&
                (tds.Keyword.Text == "union" || tds.Modifiers.Any(static m => m.Text == "union")))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<ITypeSymbol> CollectCs11UnionCaseTypes(Compilation compilation, INamedTypeSymbol packable)
    {
        var cases = new List<ITypeSymbol>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);

        // The compiler-generated case constructors are visible for the explicit [Union] form and, on
        // a closed generic union, already substitute the type arguments (e.g. Some<UnionCat>).
        foreach (IMethodSymbol ctor in packable.InstanceConstructors)
        {
            if (ctor.DeclaredAccessibility == Accessibility.Public &&
                ctor.Parameters.Length == 1 &&
                ctor.Parameters[0].Type is INamedTypeSymbol caseType &&
                seen.Add(caseType))
            {
                cases.Add(caseType);
            }
        }

        if (cases.Count != 0)
        {
            return cases;
        }

        // `union` keyword: case types live in the declaration's parameter list (read via reflection
        // because the generator targets an older Roslyn).  For a closed generic union, substitute the
        // type parameters so closed case types (Some<UnionCat>) stay reachable.
        foreach (SyntaxReference reference in packable.DeclaringSyntaxReferences)
        {
            SyntaxNode node = reference.GetSyntax();
            if (node is not TypeDeclarationSyntax tds ||
                (tds.Keyword.Text != "union" && !tds.Modifiers.Any(static m => m.Text == "union")))
            {
                continue;
            }

            object? parameterList = node.GetType().GetProperty("ParameterList")?.GetValue(node);
            object? parameters = parameterList?.GetType().GetProperty("Parameters")?.GetValue(parameterList);
            if (parameters is not System.Collections.IEnumerable enumerable)
            {
                continue;
            }

            SemanticModel model = compilation.GetSemanticModel(node.SyntaxTree);
            foreach (object? parameter in enumerable)
            {
                object? typeSyntax = parameter?.GetType().GetProperty("Type")?.GetValue(parameter);
                if (typeSyntax is not SyntaxNode typeNode)
                {
                    continue;
                }
                if (model.GetTypeInfo(typeNode).Type is INamedTypeSymbol caseType && seen.Add(caseType))
                {
                    cases.Add(caseType);
                }
            }

            if (packable.TypeArguments.Length != 0)
            {
                var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
                for (int i = 0; i < packable.TypeParameters.Length && i < packable.TypeArguments.Length; i++)
                {
                    map[packable.TypeParameters[i]] = packable.TypeArguments[i];
                }
                for (int i = 0; i < cases.Count; i++)
                {
                    cases[i] = Substitute(compilation, cases[i], map);
                }
            }
        }

        return cases;
    }

    private static bool IsDerivedFrom(INamedTypeSymbol derived, INamedTypeSymbol baseDef)
    {
        if (baseDef.TypeKind == TypeKind.Interface)
        {
            foreach (INamedTypeSymbol candidate in derived.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, baseDef))
                {
                    return true;
                }
            }
            return false;
        }

        for (INamedTypeSymbol? current = derived; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseDef))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Adds the transitive base-type chain of every emitted packable to the emitted set.  Subclass
    /// formatters serialize inherited base members and reference the base's local class structure, so
    /// base packables must be emitted even when they are never serialized directly.
    /// </summary>
    private static void AddBasePackableChain(Compilation compilation, ISet<ITypeSymbol> definitions)
    {
        var pending = new Queue<INamedTypeSymbol>();
        foreach (ITypeSymbol definition in definitions)
        {
            if (definition is INamedTypeSymbol named)
            {
                pending.Enqueue(named);
            }
        }

        while (pending.Count != 0)
        {
            INamedTypeSymbol current = pending.Dequeue();
            for (INamedTypeSymbol? baseType = current.BaseType;
                 baseType is not null && baseType.SpecialType != SpecialType.System_Object;
                 baseType = baseType.BaseType)
            {
                INamedTypeSymbol baseDef = baseType.OriginalDefinition;
                if (HasPackableAttribute(baseDef) && baseDef.DeclaringSyntaxReferences.Length != 0 &&
                    definitions.Add(baseDef))
                {
                    pending.Enqueue(baseDef);
                }
            }
        }
    }

    private static void ExpandPackableMembers(Compilation compilation, INamedTypeSymbol packable, ISet<ITypeSymbol> types, CompilationTypeAnalysis analysis)
    {
        foreach (ISymbol member in packable.GetMembers())
        {
            ITypeSymbol? memberType = member switch
            {
                IFieldSymbol { IsStatic: false, IsImplicitlyDeclared: false } field =>
                    IsSerializedField(field) ? field.Type : null,
                IPropertySymbol { IsStatic: false } property when IsAutoProperty(property) =>
                    IsSerializedProperty(property) ? property.Type : null,
                _ => null
            };

            if (memberType is null)
            {
                continue;
            }

            // A member whose type still contains this packable's own type parameters cannot be
            // expanded to a concrete formatter here; the closed construction is reached through the
            // concrete instantiation that uses it.
            bool stillOpen = EnumerateTypeParameters(memberType).Any(parameter =>
                parameter.ContainingSymbol is INamedTypeSymbol owner &&
                SymbolEqualityComparer.Default.Equals(owner.OriginalDefinition, packable.OriginalDefinition));
            if (stillOpen)
            {
                continue;
            }

            ExpandType(compilation, memberType, types, analysis);
        }
    }

    private static bool IsAutoProperty(IPropertySymbol property)
        => property.ContainingType.GetMembers().OfType<IFieldSymbol>().Any(f =>
            f.IsImplicitlyDeclared &&
            f.AssociatedSymbol is IPropertySymbol p &&
            SymbolEqualityComparer.Default.Equals(p, property));

    private static bool IsSerializedField(IFieldSymbol field)
        => !TypeMetaChecker.TryCheckIgnoreAttribute(field) &&
           (TypeMetaChecker.TryCheckIncludeAttribute(field) || IsPubliclyAccessible(field.DeclaredAccessibility));

    private static bool IsSerializedProperty(IPropertySymbol property)
        => !TypeMetaChecker.TryCheckIgnoreAttribute(property) &&
           (TypeMetaChecker.TryCheckIncludeAttribute(property) || IsPubliclyAccessible(property.DeclaredAccessibility));

    private static bool IsPubliclyAccessible(Accessibility accessibility)
        => accessibility is not (
            Accessibility.Private or
            Accessibility.ProtectedAndInternal or
            Accessibility.Protected);

    private static bool HasPackableAttribute(ITypeSymbol type)
        => type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString() == LuminPackSourceGenerator.LUMIN_PACKABLE_ATTRIBUTE);

    private static readonly string[] FormatterMethodNames =
    {
        "WriteValue", "ReadValue", "CalculateOffset",
        "WriteValueWithCompress", "ReadValueWithCompress", "ReadFreshValue",
        "WritePolymorphismValue", "ReadPolymorphismValue",
    };

    private static bool IsGeneratedFormatterMethod(IMethodSymbol method)
    {
        if (method.IsGenericMethod)
        {
            return false;
        }
        if (method.ContainingType is not { } type)
        {
            return false;
        }
        if (type.ContainingNamespace?.ToDisplayString() != "LuminPack.Generated")
        {
            return false;
        }
        return Array.IndexOf(FormatterMethodNames, method.Name) >= 0;
    }


    /// <summary>
    /// Adds a payload type as a seed, or, when it still contains source-declared type parameters,
    /// enqueues a slot for each distinct generic owner so concrete instantiations can resolve it.
    /// </summary>
    private static void CollectPayload(
        Compilation compilation,
        ITypeSymbol payload,
        ISet<ITypeSymbol> seeds,
        Queue<TypeParamSlot> pending)
    {
        if (payload is null)
        {
            return;
        }

        var owners = new HashSet<(IMethodSymbol? Method, INamedTypeSymbol? Type, int Ordinal)>();
        foreach (ITypeParameterSymbol parameter in EnumerateTypeParameters(payload))
        {
            switch (parameter.ContainingSymbol)
            {
                case IMethodSymbol method when method.DeclaringSyntaxReferences.Length != 0:
                    owners.Add((method.OriginalDefinition, null, 0));
                    break;
                case INamedTypeSymbol type when type.DeclaringSyntaxReferences.Length != 0:
                    owners.Add((null, type.OriginalDefinition, 0));
                    break;
            }
        }

        if (owners.Count == 0)
        {
            seeds.Add(payload);
            return;
        }

        foreach ((IMethodSymbol? method, INamedTypeSymbol? type, _) in owners)
        {
            pending.Enqueue(new TypeParamSlot(method, type, payload));
        }
    }

    private static IEnumerable<ITypeParameterSymbol> EnumerateTypeParameters(ITypeSymbol type)
    {
        switch (type)
        {
            case IArrayTypeSymbol array:
                foreach (ITypeParameterSymbol parameter in EnumerateTypeParameters(array.ElementType))
                {
                    yield return parameter;
                }
                break;
            case IPointerTypeSymbol pointer:
                foreach (ITypeParameterSymbol parameter in EnumerateTypeParameters(pointer.PointedAtType))
                {
                    yield return parameter;
                }
                break;
            case INamedTypeSymbol named:
                foreach (ITypeParameterSymbol parameter in named.TypeArguments.SelectMany(EnumerateTypeParameters))
                {
                    yield return parameter;
                }
                if (named.ContainingType is not null)
                {
                    foreach (ITypeParameterSymbol parameter in EnumerateTypeParameters(named.ContainingType))
                    {
                        yield return parameter;
                    }
                }
                break;
            case ITypeParameterSymbol parameter:
                yield return parameter;
                break;
        }
    }

    private static Dictionary<ITypeParameterSymbol, ITypeSymbol> BuildMap(TypeParamSlot slot, IMethodSymbol invoked)
    {
        var map = new Dictionary<ITypeParameterSymbol, ITypeSymbol>(SymbolEqualityComparer.Default);
        if (slot.OwnerMethod is { } method)
        {
            for (int i = 0; i < method.TypeParameters.Length; i++)
            {
                if (i < invoked.TypeArguments.Length)
                {
                    map[method.TypeParameters[i]] = invoked.TypeArguments[i];
                }
            }
        }

        if (slot.OwnerType is { } type && invoked.ContainingType is not null)
        {
            for (int i = 0; i < type.TypeParameters.Length; i++)
            {
                if (i < invoked.ContainingType.TypeArguments.Length)
                {
                    map[type.TypeParameters[i]] = invoked.ContainingType.TypeArguments[i];
                }
            }
        }

        return map;
    }

    private static ITypeSymbol Substitute(
        Compilation compilation,
        ITypeSymbol type,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> map)
    {
        switch (type)
        {
            case ITypeParameterSymbol parameter:
                return map.TryGetValue(parameter, out ITypeSymbol? replacement) ? replacement : parameter;
            case IArrayTypeSymbol array:
                return compilation.CreateArrayTypeSymbol(Substitute(compilation, array.ElementType, map), array.Rank);
            case IPointerTypeSymbol pointer:
                return compilation.CreatePointerTypeSymbol(Substitute(compilation, pointer.PointedAtType, map));
            case INamedTypeSymbol named:
                return SubstituteNamed(compilation, named, map);
            default:
                return type;
        }
    }

    private static ITypeSymbol SubstituteNamed(
        Compilation compilation,
        INamedTypeSymbol named,
        IReadOnlyDictionary<ITypeParameterSymbol, ITypeSymbol> map)
    {
        ITypeSymbol[] args = named.TypeArguments
            .Select(static argument => argument)
            .Select(argument => Substitute(compilation, argument, map))
            .ToArray();

        if (named.ContainingType is null)
        {
            bool sameArgs = named.TypeArguments.Length == args.Length;
            if (sameArgs)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    if (!SymbolEqualityComparer.Default.Equals(named.TypeArguments[i], args[i]))
                    {
                        sameArgs = false;
                        break;
                    }
                }
            }
            if (named.TypeArguments.Length == 0 || sameArgs)
            {
                return named;
            }
            return named.OriginalDefinition.Construct(args);
        }

        ITypeSymbol substitutedContaining = Substitute(compilation, named.ContainingType, map);
        if (substitutedContaining is not INamedTypeSymbol containing)
        {
            return named;
        }

        INamedTypeSymbol containingDef = containing.OriginalDefinition;
        INamedTypeSymbol closedContaining = containingDef.TypeArguments.Length == containing.TypeArguments.Length
            ? containing
            : containingDef.Construct(containing.TypeArguments.ToArray());

        INamedTypeSymbol nestedDef = closedContaining.GetTypeMembers(named.Name, named.Arity).FirstOrDefault()
            ?? named.OriginalDefinition;
        return nestedDef.TypeParameters.Length == 0 ? nestedDef : nestedDef.Construct(args);
    }

    private readonly struct TypeParamSlot : IEquatable<TypeParamSlot>
    {
        internal TypeParamSlot(IMethodSymbol? ownerMethod, INamedTypeSymbol? ownerType, ITypeSymbol payloadType)
        {
            OwnerMethod = ownerMethod;
            OwnerType = ownerType;
            PayloadType = payloadType;
        }

        internal IMethodSymbol? OwnerMethod { get; }
        internal INamedTypeSymbol? OwnerType { get; }
        internal ITypeSymbol PayloadType { get; }

        internal bool Matches(IMethodSymbol invoked)
        {
            if (OwnerMethod is not null)
            {
                return SymbolEqualityComparer.Default.Equals(invoked.OriginalDefinition, OwnerMethod);
            }

            return OwnerType is not null &&
                invoked.ContainingType is not null &&
                SymbolEqualityComparer.Default.Equals(invoked.ContainingType.OriginalDefinition, OwnerType);
        }

        public bool Equals(TypeParamSlot other) =>
            SymbolEqualityComparer.Default.Equals(OwnerMethod, other.OwnerMethod) &&
            SymbolEqualityComparer.Default.Equals(OwnerType, other.OwnerType) &&
            SymbolEqualityComparer.Default.Equals(PayloadType, other.PayloadType);

        public override bool Equals(object? obj) => obj is TypeParamSlot other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = OwnerMethod is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(OwnerMethod);
                hash = (hash * 397) ^ (OwnerType is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(OwnerType));
                hash = (hash * 397) ^ SymbolEqualityComparer.Default.GetHashCode(PayloadType);
                return hash;
            }
        }
    }
}
