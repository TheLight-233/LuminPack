using System.Linq;
using LuminPack.Code;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.CodeEmitters;

/// <summary>Selects compatible value traversals without changing concurrent capture contracts.</summary>
internal static class ConcurrentTraversalEmitter
{
    /// <summary>Checks the callback-free framework contract and complete staged-publication helper.</summary>
    /// <param name="compilation">The actual consumer metadata.</param>
    /// <param name="element">The declared stack element.</param>
    /// <returns>Whether staged JSON construction preserves every user callback boundary.</returns>
    internal static bool CanStageStack(Compilation compilation, ITypeSymbol element)
    {
        if (element is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            element = nullable.TypeArguments[0];
        if (!(element.SpecialType == SpecialType.System_String || EmitterTypeTraits.IsKnownScalar(element)) ||
            !SymbolEqualityComparer.Default.Equals(element.ContainingAssembly, compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly)) return false;
        var stack = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentStack`1");
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        if (stack is null || owner is null || owner.DeclaredAccessibility != Accessibility.Public || !owner.IsStatic) return false;
        return owner.GetMembers("PublishConcurrentStack").OfType<IMethodSymbol>().Any(method => method.IsStatic &&
            method.DeclaredAccessibility == Accessibility.Public && method.Arity == 1 && method.ReturnsVoid && method.Parameters.Length == 4 &&
            method.Parameters.All(parameter => parameter.RefKind == RefKind.None) && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
            !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasValueTypeConstraint &&
            !method.TypeParameters[0].HasUnmanagedTypeConstraint && !method.TypeParameters[0].HasConstructorConstraint && !method.TypeParameters[0].HasNotNullConstraint &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stack.Construct(method.TypeParameters[0])) &&
            method.Parameters[1].Type is IArrayTypeSymbol { Rank: 1 } array && SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) &&
            method.Parameters[2].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[3].Type.SpecialType == SpecialType.System_Boolean);
    }

    /// <summary>Checks the exact lease or raw snapshot API before selecting it.</summary>
    /// <param name="compilation">The actual runtime metadata.</param>
    /// <param name="raw">Whether to check direct unmanaged output.</param>
    /// <returns>Whether the complete expected contract is present.</returns>
    internal static bool HasBagSnapshot(Compilation compilation, bool raw)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var bag = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentBag`1");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        if (owner is null || owner.DeclaredAccessibility != Accessibility.Public || !owner.IsStatic || bag is null || writer is null) return false;
        return owner.GetMembers(raw ? "TryWriteConcurrentBagRaw" : "RentConcurrentBagSnapshot").OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 1 &&
            !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
            !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasNotNullConstraint &&
            (raw ? method.TypeParameters[0].HasUnmanagedTypeConstraint && method.Parameters.Length == 2 &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters[0].RefKind == RefKind.Ref &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) && method.Parameters[1].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, bag.Construct(method.TypeParameters[0])) :
                !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasConstructorConstraint && method.Parameters.Length == 3 &&
                method.ReturnType is IArrayTypeSymbol { Rank: 1 } array && SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) &&
                method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, bag.Construct(method.TypeParameters[0])) &&
                method.Parameters[1].RefKind == RefKind.Out && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                method.Parameters[2].RefKind == RefKind.Out && method.Parameters[2].Type.SpecialType == SpecialType.System_Boolean));
    }

    /// <summary>Checks the exact public collection parameter and nested traversal result.</summary>
    /// <param name="compilation">The actual consumer metadata.</param>
    /// <param name="family">The concrete concurrent collection name.</param>
    /// <param name="arity">The number of element types.</param>
    /// <returns>Whether the referenced helper has the complete expected signature.</returns>
    internal static bool Available(Compilation compilation, string family, int arity)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var collection = compilation.GetTypeByMetadataName("System.Collections.Concurrent." + family + "`" + arity);
        var traversal = owner?.GetTypeMembers(family + "Traversal", arity).SingleOrDefault();
        if (owner is null || owner.DeclaredAccessibility != Accessibility.Public || !owner.IsStatic || collection is null ||
            traversal is null || !traversal.IsValueType || traversal.IsRefLikeType || traversal.DeclaredAccessibility != Accessibility.Public ||
            !traversal.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract, compilation.GetTypeByMetadataName("System.IDisposable")))) return false;
        var pair = compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2");
        if (arity == 2 && pair is null) return false;
        ITypeSymbol current = arity == 1 ? traversal.TypeParameters[0] : pair!.Construct(traversal.TypeParameters.Cast<ITypeSymbol>().ToArray());
        if (!traversal.GetMembers("MoveNext").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.IsEmpty && method.ReturnType.SpecialType == SpecialType.System_Boolean) ||
            !traversal.GetMembers("GetEnumerator").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.IsEmpty && SymbolEqualityComparer.Default.Equals(method.ReturnType, traversal)) ||
            !traversal.GetMembers("Current").OfType<IPropertySymbol>().Any(property => !property.IsStatic && !property.IsIndexer && !property.ReturnsByRef && !property.ReturnsByRefReadonly && property.GetMethod?.DeclaredAccessibility == Accessibility.Public && SymbolEqualityComparer.Default.Equals(property.Type, current))) return false;
        return owner.GetMembers("Enumerate" + family).OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == arity &&
            !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
            method.TypeParameters.All(parameter => parameter.ConstraintTypes.IsEmpty && !parameter.HasReferenceTypeConstraint && !parameter.HasValueTypeConstraint && !parameter.HasUnmanagedTypeConstraint && !parameter.HasConstructorConstraint && (!parameter.HasNotNullConstraint || arity == 2 && parameter.Ordinal == 0)) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, collection.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray())) &&
            SymbolEqualityComparer.Default.Equals(method.ReturnType, traversal.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray())));
    }

    /// <summary>Uses the selected optional traversal or the original public enumerable.</summary>
    /// <param name="field">The analyzed collection.</param>
    /// <param name="family">The requested concurrent family.</param>
    /// <param name="value">The original collection expression.</param>
    /// <returns>The concrete enumeration expression.</returns>
    internal static string Source(LuminLocalFieldData field, string family, string value)
        => field.CanTraverseConcurrent ? "global::LuminPack.Code.LuminPackCollectionAccess.Enumerate" + family + "(" + value + ")" : value;
}
