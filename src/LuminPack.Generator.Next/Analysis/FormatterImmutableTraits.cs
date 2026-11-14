using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Checks optional immutable storage operations in the consumer's runtime.</summary>
internal static class FormatterImmutableTraits
{
    /// <summary>Checks the two runtime operations used to traverse a queue without populating its reversal cache.</summary>
    /// <param name="compilation">The consuming compilation and its framework and runtime references.</param>
    /// <returns>Whether both operations accept the framework's closed queue and stack types.</returns>
    internal static bool HasQueueTraversal(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var queue = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableQueue`1");
        var stack = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableStack`1");
        if (access is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public } || queue is not { IsSealed: true } || stack == null)
            return false;
        return access.GetMembers("TryGetImmutableQueueTraversal").OfType<IMethodSymbol>().Any(method =>
            Eligible(method, 4) && method.ReturnType.SpecialType == SpecialType.System_Boolean &&
            IsParameter(method, 0, RefKind.None, queue) && IsParameter(method, 1, RefKind.Out, stack) &&
            IsParameter(method, 2, RefKind.Out, stack) && method.Parameters[3].RefKind == RefKind.Out &&
            method.Parameters[3].Type.SpecialType == SpecialType.System_Boolean) &&
            access.GetMembers("RentImmutableQueueReversal").OfType<IMethodSymbol>().Any(method =>
            Eligible(method, 2) && IsParameter(method, 0, RefKind.None, stack) &&
            method.Parameters[1].RefKind == RefKind.Out && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
            method.ReturnType is IArrayTypeSymbol { IsSZArray: true } array &&
            SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]));

        static bool Eligible(IMethodSymbol method, int count) => method is
            { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
            method.Parameters.Length == count && Unconstrained(method.TypeParameters[0]) && !method.TypeParameters[0].HasNotNullConstraint;
        static bool IsParameter(IMethodSymbol method, int index, RefKind kind, INamedTypeSymbol type) =>
            method.Parameters[index].RefKind == kind &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[index].Type, type.Construct(method.TypeParameters[0]));
    }

    internal static bool HasTreeCopy(Compilation compilation, string family, int arity)
    {
        var storage = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        var tree = compilation.GetTypeByMetadataName("System.Collections.Immutable." + family + "`" + arity);
        var span = compilation.GetTypeByMetadataName("System.Span`1");
        var pair = compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2");
        return storage is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && tree is { IsSealed: true } && span != null &&
            (arity == 1 || pair != null) && storage.GetMembers("CopyTo").OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, ReturnsVoid: true } &&
            method.Arity == arity && method.Parameters.Length == 2 &&
            method.TypeParameters.All(Unconstrained) &&
            method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, tree.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray())) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, span.Construct(arity == 1 ? method.TypeParameters[0] :
                pair!.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray()))));
    }

    internal static bool HasSetFactory(Compilation compilation, string family, string name)
    {
        var storage = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        var set = compilation.GetTypeByMetadataName("System.Collections.Immutable." + family + "`1");
        return storage is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && set != null &&
            storage.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.Parameters.Length == 2 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
                Unconstrained(method.TypeParameters[0]) && !method.TypeParameters[0].HasNotNullConstraint &&
                method.Parameters[0].Type is IArrayTypeSymbol { IsSZArray: true } array &&
                SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, set.Construct(method.TypeParameters[0])));
    }

    /// <summary>Proves the absence of declared user equality on a source-owned closed sealed reference contract.</summary>
    /// <param name="compilation">The compilation that owns the class and provides the framework symbols.</param>
    /// <param name="element">The exact collection element type.</param>
    /// <returns>Whether the runtime may attempt its independent linked-type check.</returns>
    internal static bool IsIdentityHashElement(Compilation compilation, ITypeSymbol element)
    {
        if (element is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: true, IsGenericType: false } named ||
            !SymbolEqualityComparer.Default.Equals(named.BaseType, compilation.GetSpecialType(SpecialType.System_Object)) ||
            !SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly) ||
            named.DeclaringSyntaxReferences.IsEmpty || named.GetMembers("Equals").Length != 0 ||
            named.GetMembers("GetHashCode").Length != 0 ||
            compilation.GetTypeByMetadataName("System.IEquatable`1") is not { } equatable)
            return false;
        return !named.AllInterfaces.Any(contract =>
            SymbolEqualityComparer.Default.Equals(contract.OriginalDefinition, equatable) &&
            SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], named));
    }

    /// <summary>Checks the optional identity-set construction entry and its result type.</summary>
    /// <param name="compilation">The actual framework and Core references.</param>
    /// <returns>Whether generated code can call the shared Core helper.</returns>
    internal static bool HasIdentityHashSetFactory(Compilation compilation)
    {
        var storage = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        var set = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableHashSet`1");
        return storage is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && set != null &&
            storage.GetMembers("TryCreateIdentityImmutableHashSet").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 3 &&
                Unconstrained(method.TypeParameters[0]) && !method.TypeParameters[0].HasNotNullConstraint &&
                method.Parameters[0] is { RefKind: RefKind.None, Type: IArrayTypeSymbol { IsSZArray: true } array } &&
                SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) &&
                method.Parameters[1].RefKind == RefKind.None && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                method.Parameters[2].RefKind == RefKind.Out &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[2].Type, set.Construct(method.TypeParameters[0])));
    }

    // A nullable annotation such as 'notnull' does not change the storage constraints of the tree parameters.
    private static bool Unconstrained(ITypeParameterSymbol parameter) =>
        !parameter.HasValueTypeConstraint && !parameter.HasReferenceTypeConstraint && !parameter.HasUnmanagedTypeConstraint &&
        !parameter.HasConstructorConstraint && parameter.ConstraintTypes.IsEmpty;

    internal static bool HasRawTreeWriter(Compilation compilation, INamedTypeSymbol tree)
    {
        var storage = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        return storage is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } && writer != null &&
            storage.GetMembers("WriteRaw").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } &&
                method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
                method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.Ref &&
                method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, tree.Construct(method.TypeParameters[0])));
    }

    /// <summary>Checks the runtime's complete immutable-list raw writer.</summary>
    /// <param name="compilation">The consumer's referenced runtime and framework metadata.</param>
    /// <returns>Whether the generated list root can call the direct output operation.</returns>
    internal static bool HasRawListWriter(Compilation compilation)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminTreeStorage");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        var list = compilation.GetTypeByMetadataName("System.Collections.Immutable.ImmutableList`1");
        return writer != null && list != null && access?.DeclaredAccessibility == Accessibility.Public &&
            access.GetMembers("WriteRawList").OfType<IMethodSymbol>().Any(method =>
            method.IsStatic && method.ReturnsVoid && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 1 &&
            method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
            method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.Ref && method.Parameters[1].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, list.Construct(method.TypeParameters[0])));
    }

    /// <summary>Checks both exact methods needed to rent and return the existing result family's builder.</summary>
    /// <param name = "compilation">The consumer's actual runtime and framework metadata.</param>
    /// <param name = "family">The concrete factory name without its Immutable prefix.</param>
    /// <param name = "arity">The number of element contracts.</param>
    /// <returns>Whether both matching ownership operations can be emitted.</returns>
    internal static bool Available(Compilation compilation, string family, int arity)
    {
        var access = compilation.GetTypeByMetadataName("LuminPack.Utility.LuminImmutableBuilders");
        var collection = compilation.GetTypeByMetadataName("System.Collections.Immutable.Immutable" + family + "`" + arity);
        if (access is null || access.DeclaredAccessibility != Accessibility.Public || collection is null)
            return false;
        bool Matches(IMethodSymbol method, bool rent)
        {
            if (!method.IsStatic || method.DeclaredAccessibility != Accessibility.Public || method.Arity != arity || method.ReturnsByRef || method.ReturnsByRefReadonly || method.TypeParameters.Any(parameter => !parameter.ConstraintTypes.IsEmpty || parameter.HasReferenceTypeConstraint || parameter.HasValueTypeConstraint || parameter.HasUnmanagedTypeConstraint || parameter.HasConstructorConstraint || (parameter.HasNotNullConstraint && !(arity == 2 && parameter.Ordinal == 0))))
                return false;
            var builder = collection.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray()).GetTypeMembers("Builder").SingleOrDefault();
            return builder is not null && (rent ? method.Parameters.IsEmpty && SymbolEqualityComparer.Default.Equals(method.ReturnType, builder) : method.ReturnsVoid && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, builder));
        }

        return access.GetMembers("Rent" + family).OfType<IMethodSymbol>().Any(method => Matches(method, true)) && access.GetMembers("Return" + family).OfType<IMethodSymbol>().Any(method => Matches(method, false));
    }
}
