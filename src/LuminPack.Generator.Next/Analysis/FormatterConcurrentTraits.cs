using System.Linq;
using LuminPack.SourceGenerator.Next.Rendering.Formatters;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Selects compatible value traversals without changing concurrent capture contracts.</summary>
internal static class FormatterConcurrentTraits
{
    /// <summary>Checks the binary writer operations for a bounded UTF-16 key and raw value.</summary>
    /// <param name="compilation">The consuming compilation and its runtime references.</param>
    /// <returns>Whether all fixed record operations required by the generated loop are available.</returns>
    internal static bool HasBinaryTextPairWriter(Compilation compilation)
    {
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        return writer != null &&
            HasBinaryWriterOperation(writer, "RecordRoom", false, SpecialType.System_Int32) &&
            HasBinaryWriterOperation(writer, "Utf16RecordBound", true, SpecialType.System_Int32,
                (SpecialType.System_String, RefKind.None)) &&
            HasBinaryWriterOperation(writer, "WriteReservedUtf16Record", true, SpecialType.System_Int32,
                (SpecialType.System_Byte, RefKind.Ref), (SpecialType.System_String, RefKind.None)) &&
            HasBinaryWriterOperation(writer, "CompleteRun", false, SpecialType.System_Void,
                (SpecialType.System_Int32, RefKind.None));
    }

    /// <summary>Checks the closed dictionary's nonthrowing layout probe.</summary>
    /// <param name="compilation">The compilation that supplies source symbols and references.</param>
    /// <param name="dictionary">The dictionary supplied to <c>HasDictionaryView</c>.</param>
    /// <returns>Whether the input has dictionary view.</returns>
    internal static bool HasDictionaryView(Compilation compilation, INamedTypeSymbol dictionary)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal");
        var view = owner?.GetTypeMembers("DictionaryView", 2).SingleOrDefault();
        return owner is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } &&
            view is { IsValueType: true, DeclaredAccessibility: Accessibility.Public } &&
            owner.GetMembers("TryGetDictionaryView").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2,
                    ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
                method.Parameters[0].RefKind == RefKind.None && method.Parameters[1].RefKind == RefKind.Out &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type,
                    dictionary.Construct(method.TypeParameters[0], method.TypeParameters[1])) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type,
                    view.Construct(method.TypeParameters[0], method.TypeParameters[1])));
    }

    /// <summary>Checks reservation operations shared by the fixed pair writers.</summary>
    /// <param name="compilation">The consuming compilation and its runtime references.</param>
    /// <returns>Whether a generated pair can validate and advance once.</returns>
    internal static bool HasBinaryRawPairWriter(Compilation compilation)
    {
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        return writer != null &&
            HasBinaryWriterOperation(writer, "RecordRoom", false, SpecialType.System_Int32) &&
            HasBinaryWriterOperation(writer, "CompleteRun", false, SpecialType.System_Void, (SpecialType.System_Int32, RefKind.None));
    }

    /// <summary>Checks a non-generic binary writer operation against its expected signature.</summary>
    /// <param name="owner">The binary writer type.</param>
    /// <param name="name">The operation name.</param>
    /// <param name="isStatic">Whether the operation is static.</param>
    /// <param name="result">The operation's return type.</param>
    /// <param name="parameters">The required types and passing conventions.</param>
    /// <returns>Whether the operation can be called by generated code.</returns>
    private static bool HasBinaryWriterOperation(INamedTypeSymbol owner, string name, bool isStatic, SpecialType result,
        params (SpecialType Type, RefKind Passing)[] parameters)
    {
        foreach (IMethodSymbol method in owner.GetMembers(name).OfType<IMethodSymbol>())
        {
            if (method.DeclaredAccessibility != Accessibility.Public || method.IsStatic != isStatic || method.Arity != 0 ||
                method.ReturnType.SpecialType != result || method.ReturnsByRef || method.ReturnsByRefReadonly ||
                method.Parameters.Length != parameters.Length)
                continue;
            if (method.Parameters.Select(parameter => (parameter.Type.SpecialType, parameter.RefKind)).SequenceEqual(parameters))
                return true;
        }
        return false;
    }

    /// <summary>Checks the existing JSON writer's bounded integer element operation.</summary>
    /// <param name="compilation">The consuming compilation and its runtime references.</param>
    /// <param name="element">The concrete sequence element.</param>
    /// <returns>Whether the formatter can combine its separator and integer output.</returns>
    internal static bool HasJsonIntegerElement(Compilation compilation, ITypeSymbol element)
    {
        if (element.SpecialType is not (SpecialType.System_Int32 or SpecialType.System_Int64))
            return false;
        return HasJsonWriterOperation(compilation, "WriteEnumerationElement", element, compilation.GetSpecialType(SpecialType.System_Boolean));
    }

    /// <summary>Checks both complete integer entry writer overloads for generic scalar roots.</summary>
    /// <param name="compilation">The consuming compilation and its runtime references.</param>
    /// <returns>Whether the runtime can combine one pair's framing and numeric writes.</returns>
    internal static bool HasJsonIntegerPairs(Compilation compilation)
    {
        var int32 = compilation.GetSpecialType(SpecialType.System_Int32);
        return HasJsonWriterOperation(compilation, "WriteIntegerEntry", int32, int32) &&
            HasJsonWriterOperation(compilation, "WriteIntegerEntry", int32, compilation.GetSpecialType(SpecialType.System_Int64));
    }

    /// <summary>Checks a profile-specialized JSON writer overload with two by-value operands.</summary>
    /// <param name="compilation">The consuming compilation and its runtime references.</param>
    /// <param name="name">The operation name.</param>
    /// <param name="first">The first operand's type.</param>
    /// <param name="second">The second operand's type.</param>
    /// <returns>Whether the public operation has the expected signature.</returns>
    private static bool HasJsonWriterOperation(Compilation compilation, string name, ITypeSymbol first, ITypeSymbol second)
    {
        var options = compilation.GetTypeByMetadataName("LuminPack.Option.ILuminPackOptions");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackJsonWriter");
        return options != null && writer?.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            !method.IsStatic && method.Arity == 1 && method.ReturnsVoid && method.DeclaredAccessibility == Accessibility.Public &&
            method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
            method.TypeParameters[0].ConstraintTypes.Length == 1 &&
            SymbolEqualityComparer.Default.Equals(method.TypeParameters[0].ConstraintTypes[0], options) &&
            method.Parameters.Length == 2 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, first) &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, second)) == true;
    }

    /// <summary>Checks the complete signature of a counted queue copy factory.</summary>
    /// <param name="compilation">The actual consumer metadata.</param>
    /// <param name="collection">The closed concurrent queue or blocking collection.</param>
    /// <param name="name">The factory method name.</param>
    /// <returns>Whether the referenced Core can construct the collection from a rented array prefix.</returns>
    internal static bool HasCountedQueueFactory(Compilation compilation, INamedTypeSymbol collection, string name)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        return owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
                method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 1 &&
                !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.Parameters.Length == 2 &&
                method.TypeParameters[0].ConstraintTypes.IsEmpty && !method.TypeParameters[0].HasReferenceTypeConstraint &&
                !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                !method.TypeParameters[0].HasConstructorConstraint && !method.TypeParameters[0].HasNotNullConstraint &&
                method.Parameters[0].RefKind == RefKind.None && method.Parameters[0].Type is IArrayTypeSymbol { IsSZArray: true } items &&
                SymbolEqualityComparer.Default.Equals(items.ElementType, method.TypeParameters[0]) &&
                method.Parameters[1].RefKind == RefKind.None && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, collection.OriginalDefinition.Construct(method.TypeParameters[0])));
    }

    /// <summary>Checks the callback-free framework contract and complete staged-publication helper.</summary>
    /// <param name = "compilation">The actual consumer metadata.</param>
    /// <param name = "element">The declared stack element.</param>
    /// <returns>Whether staged JSON construction preserves every user callback boundary.</returns>
    internal static bool CanStageStack(Compilation compilation, ITypeSymbol element)
    {
        if (element is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            element = nullable.TypeArguments[0];
        if (!(element.SpecialType == SpecialType.System_String || element.IsUnmanagedType && (element.SpecialType != SpecialType.None || FrameworkScalarBody.Contains(element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)))) || !SymbolEqualityComparer.Default.Equals(element.ContainingAssembly, compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly))
            return false;
        var stack = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentStack`1");
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        if (stack is null || owner is null || owner.DeclaredAccessibility != Accessibility.Public || !owner.IsStatic)
            return false;
        return owner.GetMembers("PublishConcurrentStack").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 1 && method.ReturnsVoid && method.Parameters.Length == 4 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) && method.TypeParameters[0].ConstraintTypes.IsEmpty && !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint && !method.TypeParameters[0].HasConstructorConstraint && !method.TypeParameters[0].HasNotNullConstraint && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stack.Construct(method.TypeParameters[0])) && method.Parameters[1].Type is IArrayTypeSymbol { Rank: 1 } array && SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[3].Type.SpecialType == SpecialType.System_Boolean);
    }

    /// <summary>Checks the exact lease or raw snapshot API before selecting it.</summary>
    /// <param name = "compilation">The actual runtime metadata.</param>
    /// <param name = "raw">Whether to check direct unmanaged output.</param>
    /// <returns>Whether the complete expected contract is present.</returns>
    internal static bool HasBagSnapshot(Compilation compilation, bool raw)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var bag = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentBag`1");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        if (owner is null || owner.DeclaredAccessibility != Accessibility.Public || !owner.IsStatic || bag is null || writer is null)
            return false;
        return owner.GetMembers(raw ? "TryWriteConcurrentBagRaw" : "RentConcurrentBagSnapshot").OfType<IMethodSymbol>().Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 1 && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.TypeParameters[0].ConstraintTypes.IsEmpty && !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasNotNullConstraint && (raw ? method.TypeParameters[0].HasUnmanagedTypeConstraint && method.Parameters.Length == 2 && method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) && method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, bag.Construct(method.TypeParameters[0])) : !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasConstructorConstraint && method.Parameters.Length == 3 && method.ReturnType is IArrayTypeSymbol { Rank: 1 } array && SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) && method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, bag.Construct(method.TypeParameters[0])) && method.Parameters[1].RefKind == RefKind.Out && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 && method.Parameters[2].RefKind == RefKind.Out && method.Parameters[2].Type.SpecialType == SpecialType.System_Boolean));
    }

    /// <summary>Checks the exact public collection parameter and nested traversal result.</summary>
    /// <param name = "compilation">The actual consumer metadata.</param>
    /// <param name = "family">The concrete concurrent collection name.</param>
    /// <param name = "arity">The number of element types.</param>
    /// <returns>Whether the referenced helper has the complete expected signature.</returns>
    internal static bool Available(Compilation compilation, string family, int arity)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var collection = compilation.GetTypeByMetadataName("System.Collections.Concurrent." + family + "`" + arity);
        var traversal = owner?.GetTypeMembers(family + "Traversal", arity).SingleOrDefault();
        if (owner is null || owner.DeclaredAccessibility != Accessibility.Public || !owner.IsStatic || collection is null || traversal is null || !traversal.IsValueType || traversal.IsRefLikeType || traversal.DeclaredAccessibility != Accessibility.Public || !traversal.AllInterfaces.Any(contract => SymbolEqualityComparer.Default.Equals(contract, compilation.GetTypeByMetadataName("System.IDisposable"))))
            return false;
        var pair = compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2");
        if (arity == 2 && pair is null)
            return false;
        ITypeSymbol current = arity == 1 ? traversal.TypeParameters[0] : pair!.Construct(traversal.TypeParameters.Cast<ITypeSymbol>().ToArray());
        if (!traversal.GetMembers("MoveNext").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.IsEmpty && method.ReturnType.SpecialType == SpecialType.System_Boolean) || !traversal.GetMembers("GetEnumerator").OfType<IMethodSymbol>().Any(method => !method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.DeclaredAccessibility == Accessibility.Public && method.Parameters.IsEmpty && SymbolEqualityComparer.Default.Equals(method.ReturnType, traversal)) || !traversal.GetMembers("Current").OfType<IPropertySymbol>().Any(property => !property.IsStatic && !property.IsIndexer && !property.ReturnsByRef && !property.ReturnsByRefReadonly && property.GetMethod?.DeclaredAccessibility == Accessibility.Public && SymbolEqualityComparer.Default.Equals(property.Type, current)))
            return false;
        return owner.GetMembers("Enumerate" + family).OfType<IMethodSymbol>().Any(method => method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == arity && !method.ReturnsByRef && !method.ReturnsByRefReadonly && method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None && method.TypeParameters.All(parameter => parameter.ConstraintTypes.IsEmpty && !parameter.HasReferenceTypeConstraint && !parameter.HasValueTypeConstraint && !parameter.HasUnmanagedTypeConstraint && !parameter.HasConstructorConstraint && (!parameter.HasNotNullConstraint || arity == 2 && parameter.Ordinal == 0)) && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, collection.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray())) && SymbolEqualityComparer.Default.Equals(method.ReturnType, traversal.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray())));
    }
}
