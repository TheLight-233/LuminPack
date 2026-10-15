using System.Collections.Generic;
using System.Linq;
using LuminPack.Code;
using LuminPack.Code.Core;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator;

/// <summary>Provides lumin pack discovery functionality.</summary>
internal static class LuminPackDiscovery
{
    const string LuminPackableAttributeName = "LuminPackableAttribute";

    /// <summary>Performs the run step for the current operation.</summary>
    /// <param name="baseType">The base type used by this operation.</param>
    /// <param name="dataInfo">The metadata describing the serializable type.</param>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    public static void Run(
        INamedTypeSymbol baseType,
        LuminDataInfo dataInfo,
        Compilation compilation)
    {
        var baseDef = baseType.OriginalDefinition;
        var baseName = baseDef.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
        var baseArity = baseDef.Arity;

        // Merge source declarations with the concrete types observed by the compilation-wide
        // semantic pass.  The latter is important for GenericMember<int>: the declaration index
        // only contains GenericMember<T>, while the syntax walk records every closed construction
        // which is actually used by this project.
        var derivedList = GetProjectPackableCandidates(compilation)
            .Where(t => !t.IsAbstract && !t.IsStatic)
            .Where(t => !IsExplicitlyRegistered(dataInfo, t))
            .Where(t => IsValidDerivedType(t, baseDef, baseName, baseArity))
            .OrderBy(d => d.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
            .ToList();

        var totalMemberCount = dataInfo.UnionMembers.Count + derivedList.Count;
        if (totalMemberCount > 256)
        {
            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.TooManyUnionMembers,
                baseType.Locations.FirstOrDefault(),
                baseType.Name,
                totalMemberCount));

            var remainingSlots = 256 - dataInfo.UnionMembers.Count;
            if (remainingSlots <= 0)
                return;

            derivedList = derivedList.Take(remainingSlots).ToList();
        }

        ushort tag = 0;
        foreach (var d in derivedList)
        {
            while (dataInfo.UnionMembers.Any(m => m.Id == tag)) tag++;
            var assignedTag = tag++;
            dataInfo.UnionMembers.Add(new LuminUnionMemberInfo(
                assignedTag,
                d,
                LuminPackUnionDispatchUtilities.CanGeneratePartial(d, compilation)));

            TypeMetaChecker._reportContext.Add(Diagnostic.Create(
                DiagnosticDescriptors.UnionMemberAutoDiscovered,
                d.Locations.FirstOrDefault() ?? baseType.Locations.FirstOrDefault(),
                d.Name,
                baseType.Name,
                assignedTag));

            if (baseType.IsGenericType)
            {
                RecordGenericTypeInfo(d, baseType, dataInfo);
            }
        }
    }

    /// <summary>Obtains the project packable candidates for the supplied input.</summary>
    /// <param name="compilation">The compilation whose symbols and referenced types are analyzed.</param>
    /// <returns>The result of the operation.</returns>
    private static IEnumerable<INamedTypeSymbol> GetProjectPackableCandidates(Compilation compilation)
    {
        var analysis = CompilationTypeAnalysisCache.GetOrCreate(compilation);
        var candidatesByDefinition = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(
            SymbolEqualityComparer.Default);

        // FormatterTypes contains both every declared [LuminPackable] type (the legacy discovery
        // domain) and the concrete type graph observed in source.  Keep discovery local to this
        // compilation: generated virtual dispatch can only be injected into local partial types.
        foreach (INamedTypeSymbol candidate in analysis.FormatterTypes.OfType<INamedTypeSymbol>())
        {
            if (!LuminPackUnionDispatchUtilities.IsCurrentCompilation(candidate, compilation) ||
                !HasLuminPackableAttribute(candidate))
            {
                continue;
            }

            INamedTypeSymbol definition = candidate.OriginalDefinition;
            if (!candidatesByDefinition.TryGetValue(definition, out HashSet<INamedTypeSymbol> candidates))
            {
                candidates = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
                candidatesByDefinition.Add(definition, candidates);
            }

            candidates.Add(candidate);
        }

        foreach (KeyValuePair<INamedTypeSymbol, HashSet<INamedTypeSymbol>> pair in candidatesByDefinition)
        {
            INamedTypeSymbol definition = pair.Key;
            if (definition.Arity == 0)
            {
                yield return definition;
                continue;
            }

            // Prefer the closed constructions actually seen in source.  Falling back to the open
            // definition preserves the pre-existing behaviour for a generic packable which has
            // not yet been constructed anywhere in this compilation.
            INamedTypeSymbol[] closedTypes = pair.Value
                .Where(static type => IsFullyClosed(type) &&
                                      !SymbolEqualityComparer.Default.Equals(type, type.OriginalDefinition))
                .OrderBy(static type => type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))
                .ToArray();
            if (closedTypes.Length != 0)
            {
                foreach (INamedTypeSymbol closedType in closedTypes)
                {
                    yield return closedType;
                }
            }
            else
            {
                yield return definition;
            }
        }
    }

    /// <summary>Determines whether the supplied value has the explicitly registered classification.</summary>
    /// <param name="dataInfo">The metadata describing the serializable type.</param>
    /// <param name="candidate">The candidate used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsExplicitlyRegistered(LuminDataInfo dataInfo, INamedTypeSymbol candidate)
    {
        foreach (LuminUnionMemberInfo member in dataInfo.UnionMembers)
        {
            if (SymbolEqualityComparer.Default.Equals(member.Type, candidate))
                return true;

            // An explicit open registration is the user's customization for the whole generic
            // family and must not be shadowed by automatically allocated closed-type tags.
            if ((member.Type.IsUnboundGenericType || !IsFullyClosed(member.Type)) &&
                SymbolEqualityComparer.Default.Equals(
                    member.Type.OriginalDefinition,
                    candidate.OriginalDefinition))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether the supplied value has the fully closed classification.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsFullyClosed(ITypeSymbol type)
    {
        switch (type)
        {
            case ITypeParameterSymbol:
                return false;
            case IArrayTypeSymbol array:
                return IsFullyClosed(array.ElementType);
            case INamedTypeSymbol named:
                return !named.IsUnboundGenericType && named.TypeArguments.All(IsFullyClosed);
            default:
                return true;
        }
    }

    /// <summary>Determines whether lumin packable attribute are present.</summary>
    /// <param name="type">The type or symbol to analyze.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool HasLuminPackableAttribute(INamedTypeSymbol type) =>
        type.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.Name == LuminPackableAttributeName);

    /// <summary>
    /// Checks whether a derived type satisfies discovery requirements.
    /// Rules:
    /// 1. Reject a derived type with more generic parameters than its base.
    /// 2. Reject parameters that are not passed directly to matching base positions.
    /// 3. Require derived generic constraints to match the base definition.
    /// 4. A fully constructed base requires a non-generic derived type.
    /// Supported cases:
    /// MyClass&lt;T&gt; : MyClassBase&lt;T&gt; has matching parameters and constraints.
    /// MyClass : MyClassBase&lt;int&gt; is fully constructed and the derived type is non-generic.
    /// </summary>
    /// <param name="derivedType">The derived type used by this operation.</param>
    /// <param name="baseDef">The base def used by this operation.</param>
    /// <param name="baseName">The base name used by this operation.</param>
    /// <param name="baseArity">The base arity used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool IsValidDerivedType(INamedTypeSymbol derivedType, INamedTypeSymbol baseDef, string baseName, int baseArity)
    {
        // First resolve the inherited base type or implemented interface.
        var matchedBase = FindMatchedBaseType(derivedType, baseDef, baseName, baseArity);
        if (matchedBase == null)
            return false;

        // Accept non-generic base types directly.
        if (baseArity == 0)
            return true;

        // Check whether every base argument is a concrete type.
        bool baseIsFullyConcrete = matchedBase.TypeArguments.All(ta => ta is not ITypeParameterSymbol);

        if (baseIsFullyConcrete)
        {
            // The base type is fully constructed, such as AbstractGenericBase<int>.
            // Allow non-generic derived types and fully constructed generic instances observed in syntax.
            // Reject open or partially open types that still contain type parameters.
            return IsFullyClosed(derivedType);
        }

        // The base uses a type parameter, such as AbstractGenericBase<T>.
        // The derived type must be generic with a matching parameter count.

        // Reject a derived type without the required generic parameters.
        if (derivedType.TypeParameters.Length == 0)
            return false;

        // Exclude derived types with more generic parameters than the base.
        if (derivedType.TypeParameters.Length > baseArity)
            return false;

        // Reject derived types with fewer generic parameters than the base.
        // Some generic arguments are concrete, such as MyClass<T> : MyClassBase<T, int>.
        if (derivedType.TypeParameters.Length < baseArity)
            return false;

        // With matching arity, verify a one-to-one type parameter correspondence.
        for (int i = 0; i < baseArity; i++)
        {
            var baseTypeArg = matchedBase.TypeArguments[i];

            // Base arguments must be type parameter symbols.
            if (baseTypeArg is not ITypeParameterSymbol typeParam)
                return false; // Concrete base arguments do not match the MyClass<T> : MyClassBase<T> pattern.

            // Require the type parameter to occupy the corresponding derived-type position.
            var derivedTypeParam = derivedType.TypeParameters[i];

            // Require matching type parameter ordinals.
            if (typeParam.Ordinal != i)
                return false; // Reject reordered type parameters, such as MyClass<T, U> : MyClassBase<U, T>.

            // Require derived generic constraints to match the base definition exactly.
            // Obtain the corresponding type parameter from the base definition.
            var baseDefinitionTypeParam = baseDef.TypeParameters[i];
            if (!AreConstraintsCompatible(derivedTypeParam, baseDefinitionTypeParam))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether derived generic constraints match the base type.
    /// </summary>
    /// <param name="derivedParam">The derived param used by this operation.</param>
    /// <param name="baseParam">The base param used by this operation.</param>
    /// <returns>Whether the requested condition or operation succeeded.</returns>
    private static bool AreConstraintsCompatible(ITypeParameterSymbol derivedParam, ITypeParameterSymbol baseParam)
    {
        // Validate special generic constraints.
        if (derivedParam.HasConstructorConstraint != baseParam.HasConstructorConstraint)
            return false;

        if (derivedParam.HasReferenceTypeConstraint != baseParam.HasReferenceTypeConstraint)
            return false;

        if (derivedParam.HasValueTypeConstraint != baseParam.HasValueTypeConstraint)
            return false;

        if (derivedParam.HasUnmanagedTypeConstraint != baseParam.HasUnmanagedTypeConstraint)
            return false;

        if (derivedParam.HasNotNullConstraint != baseParam.HasNotNullConstraint)
            return false;

        // Compare the number of generic constraints.
        if (derivedParam.ConstraintTypes.Length != baseParam.ConstraintTypes.Length)
            return false;

        // Compare generic constraint types.
        for (int i = 0; i < derivedParam.ConstraintTypes.Length; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(
                derivedParam.ConstraintTypes[i],
                baseParam.ConstraintTypes[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Finds the base type or interface matched by a derived type.
    /// </summary>
    /// <param name="derived">The derived used by this operation.</param>
    /// <param name="baseDef">The base def used by this operation.</param>
    /// <param name="baseName">The base name used by this operation.</param>
    /// <param name="baseArity">The base arity used by this operation.</param>
    /// <returns>The result of the operation.</returns>
    private static INamedTypeSymbol? FindMatchedBaseType(INamedTypeSymbol derived, INamedTypeSymbol baseDef, string baseName, int baseArity)
    {
        // Process the base type chain.
        if (baseDef.TypeKind == TypeKind.Class)
        {
            var cur = derived.BaseType;
            while (cur != null)
            {
                var def = cur.OriginalDefinition;
                if (def.Arity == baseArity &&
                    def.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) == baseName)
                    return cur;
                cur = cur.BaseType;
            }
        }
        // Process implemented interfaces.
        else if (baseDef.TypeKind == TypeKind.Interface)
        {
            // Inspect all implemented interfaces.
            foreach (var implementedInterface in derived.AllInterfaces)
            {
                var def = implementedInterface.OriginalDefinition;
                if (def.Arity == baseArity &&
                    def.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat) == baseName)
                    return implementedInterface;
            }
        }

        return null;
    }

    /// <summary>
    /// Records generic type metadata.
    /// </summary>
    /// <param name="derivedType">The derived type used by this operation.</param>
    /// <param name="baseType">The base type used by this operation.</param>
    /// <param name="dataInfo">The metadata describing the serializable type.</param>
    private static void RecordGenericTypeInfo(INamedTypeSymbol derivedType, INamedTypeSymbol baseType, LuminDataInfo dataInfo)
    {
        if (baseType.TypeKind == TypeKind.Class)
        {
            var current = derivedType;
            while (current != null)
            {
                if (SymbolEqualityComparer.Default.Equals(
                        current.OriginalDefinition,
                        baseType.OriginalDefinition))
                {
                    dataInfo.UnionGenericTypes[derivedType] =
                        current.TypeArguments.Select(t => t.ToDisplayString()).ToList();
                    break;
                }
                current = current.BaseType;
            }
        }
        else if (baseType.TypeKind == TypeKind.Interface)
        {
            foreach (var implementedInterface in derivedType.AllInterfaces)
            {
                if (SymbolEqualityComparer.Default.Equals(
                        implementedInterface.OriginalDefinition,
                        baseType.OriginalDefinition))
                {
                    dataInfo.UnionGenericTypes[derivedType] =
                        implementedInterface.TypeArguments.Select(t => t.ToDisplayString()).ToList();
                    break;
                }
            }
        }
    }
}
