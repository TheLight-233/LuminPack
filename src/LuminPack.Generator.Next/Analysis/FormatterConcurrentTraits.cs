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

    /// <summary>Checks the runtime's optional bounded writer for a dense dictionary of raw pairs.</summary>
    /// <param name="compilation">The consumer's actual Core and framework references.</param>
    /// <returns>Whether a generated interface writer can call the bounded dense operation.</returns>
    internal static bool HasBoundedDenseRawPairs(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        var dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
        return owner != null && writer != null && dictionary != null &&
            owner.GetMembers("TryWriteBoundedDenseRawPairs").OfType<IMethodSymbol>().Any(method =>
                method.IsStatic && method.DeclaredAccessibility == Accessibility.Public && method.Arity == 2 &&
                !method.ReturnsByRef && !method.ReturnsByRefReadonly &&
                method.TypeParameters.All(parameter => parameter.HasUnmanagedTypeConstraint) &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 3 &&
                method.Parameters[0].RefKind == RefKind.Ref &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) &&
                method.Parameters[1].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type,
                    dictionary.Construct(method.TypeParameters[0], method.TypeParameters[1])) &&
                method.Parameters[2].RefKind == RefKind.Out && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32);
    }

    /// <summary>Checks the expansion helper that consumes a validated byte span and a fresh dictionary view.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether generated code can call the expansion before the bucket builder.</returns>
    internal static bool HasRawDictionaryExpansion(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackMarshal");
        var span = compilation.GetTypeByMetadataName("System.ReadOnlySpan`1");
        var view = owner?.GetTypeMembers("DictionaryView", 2).SingleOrDefault();
        if (owner is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public } || span is null ||
            view is not { IsValueType: true, DeclaredAccessibility: Accessibility.Public })
            return false;
        var bytes = span.Construct(compilation.GetSpecialType(SpecialType.System_Byte));
        return owner.GetMembers("TryExpandDenseRawPairs").OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
            method.TypeParameters.All(parameter => parameter.HasUnmanagedTypeConstraint) &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 3 &&
            method.Parameters[0].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, bytes) &&
            method.Parameters[1].RefKind == RefKind.None && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type,
                view.Construct(method.TypeParameters[0], method.TypeParameters[1])) &&
            method.Parameters[2] is { RefKind: RefKind.None, Type.SpecialType: SpecialType.System_Int32 });
    }

    /// <summary>Checks an optional runtime transfer of staged raw pair arrays.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <param name="interleave">Whether keys and values are stored in separate arrays.</param>
    /// <param name="read">Whether the operation reads staged pairs.</param>
    /// <returns>Whether generated code can call the corresponding bounded transfer.</returns>
    internal static bool HasCapturedRawPairTransfer(Compilation compilation, bool interleave, bool read = false)
    {
        var owner = compilation.GetTypeByMetadataName(interleave ? "LuminPack.Utility.LuminFrozenConstruction" : "LuminPack.Utility.LuminTreeStorage");
        var state = compilation.GetTypeByMetadataName(read ? "LuminPack.Core.LuminPackReader" : "LuminPack.Core.LuminPackWriter");
        var pair = compilation.GetTypeByMetadataName("System.Collections.Generic.KeyValuePair`2");
        if (owner is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public } || state is null || pair is null)
            return false;
        string name = interleave
            ? (read ? "TryReadInterleavedPairs" : "TryWriteInterleavedPairs")
            : (read ? "TryReadPackedPairs" : "TryWritePackedPairs");
        return owner.GetMembers(name).OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
            method.Arity == 2 && method.Parameters.Length == (interleave ? 4 : 3) &&
            method.TypeParameters.All(parameter => parameter.HasUnmanagedTypeConstraint) &&
            method.ReturnType.SpecialType == SpecialType.System_Boolean &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, state) &&
            method.Parameters.Last() is { RefKind: RefKind.None, Type.SpecialType: SpecialType.System_Int32 } &&
            (interleave
                ? MatchesArray(method.Parameters[1], method.TypeParameters[0]) && MatchesArray(method.Parameters[2], method.TypeParameters[1])
                : MatchesArray(method.Parameters[1], pair.Construct(method.TypeParameters[0], method.TypeParameters[1]))));

        static bool MatchesArray(IParameterSymbol parameter, ITypeSymbol element) =>
            parameter is { RefKind: RefKind.None, Type: IArrayTypeSymbol { IsSZArray: true } array } &&
            SymbolEqualityComparer.Default.Equals(array.ElementType, element);
    }

    /// <summary>Checks a raw collection writer against its exact framework signature.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <param name="member">The Core entry point.</param>
    /// <param name="collectionName">The framework collection metadata name.</param>
    /// <param name="result">The entry point's return type.</param>
    /// <param name="checksCapacity">Whether the operation can retain checked scalar fallback writes.</param>
    /// <returns>Whether generated code can call the writer.</returns>
    internal static bool HasRawCollectionWriter(Compilation compilation, string member, string collectionName, SpecialType result, bool checksCapacity = false)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var writer = compilation.GetTypeByMetadataName("LuminPack.Core.LuminPackWriter");
        var collection = compilation.GetTypeByMetadataName(collectionName);
        if (owner is not { IsStatic: true, DeclaredAccessibility: Accessibility.Public } || writer is null || collection is null)
            return false;
        return owner.GetMembers(member).OfType<IMethodSymbol>().Any(method =>
            method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, ReturnsByRef: false, ReturnsByRefReadonly: false } &&
            method.Arity == collection.Arity && method.TypeParameters.All(parameter => parameter.HasUnmanagedTypeConstraint) &&
            method.ReturnType.SpecialType == result && method.Parameters.Length == (checksCapacity ? 3 : 2) &&
            method.Parameters[0].RefKind == RefKind.Ref && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, writer) &&
            method.Parameters[1].RefKind == RefKind.None &&
            SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, collection.Construct(method.TypeParameters.Cast<ITypeSymbol>().ToArray())) &&
            (!checksCapacity || method.Parameters[2] is { RefKind: RefKind.None, Type.SpecialType: SpecialType.System_Boolean }));
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

    /// <summary>Checks the optional accessor for the array behind an existing read-only collection.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasReadOnlyCollectionRefill(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var wrapper = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ReadOnlyCollection`1");
        return wrapper is not null && owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("GetArrayBackedWrapperItems").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1 } && method.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type.OriginalDefinition, wrapper) &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 && method.ReturnType is IArrayTypeSymbol);
    }

    /// <summary>Checks the optional accessor for the dictionary behind an existing read-only dictionary.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasReadOnlyDictionaryRefill(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var wrapper = compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ReadOnlyDictionary`2");
        var dictionary = compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2");
        return wrapper is not null && dictionary is not null && owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("GetReadOnlyDictionaryStorage").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2 } && method.Parameters.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type.OriginalDefinition, wrapper) &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType.OriginalDefinition, dictionary));
    }

    /// <summary>Checks the optional in-place concurrent dictionary refill.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether the referenced entry point and its state type have the expected public shape.</returns>
    internal static bool HasConcurrentDictionaryRefill(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var state = compilation.GetTypeByMetadataName("LuminPack.Code.ConcurrentDictionaryRefill`2");
        var map = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentDictionary`2");
        return map is not null && state is { DeclaredAccessibility: Accessibility.Public, IsValueType: true } &&
            owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("BeginConcurrentDictionaryRefill").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2 } && method.Parameters.Length == 3 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type.OriginalDefinition, map) &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                method.Parameters[2].Type.SpecialType == SpecialType.System_Boolean &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType.OriginalDefinition, state)) &&
            new[] { "Ordered", "NextKey", "Find", "Store", "Complete" }.All(name =>
                state.GetMembers(name).Any(member => member.DeclaredAccessibility == Accessibility.Public));
    }

    /// <summary>Checks the optional seeding of a concurrent stack read buffer with the existing items.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasConcurrentStackSeed(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var stack = compilation.GetTypeByMetadataName("System.Collections.Concurrent.ConcurrentStack`1");
        return stack is not null && owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("SeedConcurrentStack").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } &&
                method.Parameters.Length == 3 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type.OriginalDefinition, stack) &&
                method.Parameters[1].Type is IArrayTypeSymbol && method.Parameters[2].Type.SpecialType == SpecialType.System_Int32);
    }

    /// <summary>Checks the optional in-place stack refill operations.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether both referenced helpers have the expected public signatures.</returns>
    internal static bool HasStackRefill(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var stack = compilation.GetTypeByMetadataName("System.Collections.Generic.Stack`1");
        return stack is not null && owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("PrepareStackRefill").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1 } && method.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stack.Construct(method.TypeParameters[0])) &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 && method.ReturnType.Name == "Span") &&
            owner.GetMembers("CompleteStackRefill").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } && method.Parameters.Length == 2);
    }

    /// <summary>Checks the optional in-place queue refill operations.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether both referenced helpers have the expected public signatures.</returns>
    internal static bool HasQueueRefill(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var queue = compilation.GetTypeByMetadataName("System.Collections.Generic.Queue`1");
        return queue is not null && owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("PrepareQueueRefill").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1 } && method.Parameters.Length == 2 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, queue.Construct(method.TypeParameters[0])) &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 && method.ReturnType.Name == "Span") &&
            owner.GetMembers("CompleteQueueRefill").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } && method.Parameters.Length == 2);
    }

    /// <summary>Checks the optional pair-by-pair concurrent dictionary builder.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasConcurrentDictionaryBuilder(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var builder = compilation.GetTypeByMetadataName("LuminPack.Code.ConcurrentDictionaryBuilder`2");
        return builder is { DeclaredAccessibility: Accessibility.Public } &&
            builder.GetMembers("Add").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 2) &&
            builder.GetMembers("Complete").OfType<IMethodSymbol>().Any(method => method.Parameters.IsEmpty) &&
            owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("BeginConcurrentDictionary").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 2 } &&
                method.Parameters.Length == 1 && method.Parameters[0].Type.SpecialType == SpecialType.System_Int32 &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, builder.Construct(method.TypeParameters[0], method.TypeParameters[1])));
    }

    /// <summary>Checks the optional direct linked-list appender.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasLinkedListAppender(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var list = compilation.GetTypeByMetadataName("System.Collections.Generic.LinkedList`1");
        var appender = compilation.GetTypeByMetadataName("LuminPack.Code.LinkedListAppender`1");
        return list is not null && appender is { DeclaredAccessibility: Accessibility.Public } &&
            appender.GetMembers("Append").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 1) &&
            appender.GetMembers("Publish").OfType<IMethodSymbol>().Any(method => method.Parameters.Length == 1) &&
            appender.GetMembers("Direct").OfType<IPropertySymbol>().Any() &&
            owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("BeginLinkedList").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1 } &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, list.Construct(method.TypeParameters[0])) &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, appender.Construct(method.TypeParameters[0])));
    }

    /// <summary>Checks the optional reused hash-set refill preparation.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasHashSetRefill(Compilation compilation)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        var set = compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1");
        return set is not null && owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("PrepareHashSetForRefill").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1 } &&
                method.Parameters.Length == 2 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
                method.TypeParameters[0].ConstraintTypes.IsEmpty && !method.TypeParameters[0].HasValueTypeConstraint &&
                !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                !method.TypeParameters[0].HasNotNullConstraint && !method.TypeParameters[0].HasConstructorConstraint &&
                SymbolEqualityComparer.Default.Equals(method.ReturnType, set.Construct(method.TypeParameters[0])) &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type.OriginalDefinition, set) &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32);
    }

    /// <summary>Checks the optional reused-queue drain operation.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <param name="queue">The exact closed framework queue.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasQueueDrain(Compilation compilation, INamedTypeSymbol queue)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        return owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("DrainConcurrentQueue").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } &&
                method.Parameters.Length == 1 && method.Parameters[0].RefKind == RefKind.None &&
                method.TypeParameters[0].ConstraintTypes.IsEmpty && !method.TypeParameters[0].HasValueTypeConstraint &&
                !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                !method.TypeParameters[0].HasNotNullConstraint && !method.TypeParameters[0].HasConstructorConstraint &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, queue.OriginalDefinition.Construct(method.TypeParameters[0])));
    }

    /// <summary>Checks the optional exact concurrent queue reset in the referenced Core.</summary>
    /// <param name="compilation">The actual consumer metadata.</param>
    /// <param name="queue">The declared concurrent queue.</param>
    /// <returns>Whether the bounded raw reader can attempt the queue reset.</returns>
    internal static bool HasSizedConcurrentQueueClear(Compilation compilation, INamedTypeSymbol queue)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        return owner is { DeclaredAccessibility: Accessibility.Public, IsStatic: true } &&
            owner.GetMembers("TryClearConcurrentQueue").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1,
                    ReturnsByRef: false, ReturnsByRefReadonly: false } &&
                method.ReturnType.SpecialType == SpecialType.System_Boolean && method.Parameters.Length == 2 &&
                method.TypeParameters[0].HasUnmanagedTypeConstraint && method.TypeParameters[0].ConstraintTypes.IsEmpty &&
                method.Parameters[0].RefKind == RefKind.None && method.Parameters[1].RefKind == RefKind.None &&
                method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, queue.OriginalDefinition.Construct(method.TypeParameters[0])));
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

    /// <summary>Checks the optional detached range push operation.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <param name="stack">The exact closed framework stack.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasStackRangePush(Compilation compilation, INamedTypeSymbol stack)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        return owner is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } &&
            owner.GetMembers("PushConcurrentStackRange").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } &&
                method.Parameters.Length == 3 && method.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
                method.TypeParameters[0].ConstraintTypes.IsEmpty && !method.TypeParameters[0].HasValueTypeConstraint &&
                !method.TypeParameters[0].HasReferenceTypeConstraint && !method.TypeParameters[0].HasUnmanagedTypeConstraint &&
                !method.TypeParameters[0].HasNotNullConstraint && !method.TypeParameters[0].HasConstructorConstraint &&
                SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, stack.OriginalDefinition.Construct(method.TypeParameters[0])) &&
                method.Parameters[1].Type is IArrayTypeSymbol { Rank: 1 } array &&
                SymbolEqualityComparer.Default.Equals(array.ElementType, method.TypeParameters[0]) &&
                method.Parameters[2].Type.SpecialType == SpecialType.System_Int32);
    }

    /// <summary>Checks the optional initial work-queue sizing operation.</summary>
    /// <param name="compilation">The consumer's Core and framework references.</param>
    /// <param name="bag">The exact framework bag definition.</param>
    /// <returns>Whether the referenced helper has the expected public signature.</returns>
    internal static bool HasBagFreshSizing(Compilation compilation, INamedTypeSymbol bag)
    {
        var owner = compilation.GetTypeByMetadataName("LuminPack.Code.LuminPackCollectionAccess");
        return owner is { IsStatic: true, DeclaredAccessibility: Accessibility.Public } &&
            owner.GetMembers("PresizeFreshConcurrentBag").OfType<IMethodSymbol>().Any(method =>
                method is { IsStatic: true, DeclaredAccessibility: Accessibility.Public, Arity: 1, ReturnsVoid: true } &&
                method.Parameters.Length == 2 && method.Parameters[0].RefKind == RefKind.None &&
                method.Parameters[1].RefKind == RefKind.None && method.Parameters[1].Type.SpecialType == SpecialType.System_Int32 &&
                !method.TypeParameters[0].HasValueTypeConstraint && !method.TypeParameters[0].HasReferenceTypeConstraint &&
                !method.TypeParameters[0].HasConstructorConstraint && !method.TypeParameters[0].HasNotNullConstraint &&
                method.TypeParameters[0].ConstraintTypes.IsEmpty && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type,
                    bag.OriginalDefinition.Construct(method.TypeParameters[0])));
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
