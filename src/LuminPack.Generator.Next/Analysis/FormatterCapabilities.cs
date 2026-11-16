using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace LuminPack.SourceGenerator.Next.Analysis;
/// <summary>Captures optional formatter APIs as immutable names after validating their actual public signatures.</summary>
internal static class FormatterCapabilities
{
    /// <summary>Captures capabilities for one closed family independently of its rendering template.</summary>
    /// <param name = "compilation">The consuming compilation and its runtime references.</param>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <param name = "modern">The modern used by this operation.</param>
    /// <param name = "unity">The unity used by this operation.</param>
    /// <returns>The normalized result of the operation.</returns>
    internal static string[] Capture(Compilation compilation, ITypeSymbol type, bool modern, bool unity)
    {
        var result = new List<string>();
        FormatterBuiltinCapabilities.Capture(compilation, type, result);
        if (type is IArrayTypeSymbol array)
        {
            ContiguousCapabilities(compilation, array.ElementType, result);
            if (array.Rank == 1 && array.ElementType.SpecialType == SpecialType.System_String &&
                FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteStrings", array.ElementType))
                result.Add("WriteArrayStrings");
        }
        if (type is not INamedTypeSymbol { IsGenericType: true } named)
            return result.ToArray();
        string definition = named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.Name;
        if (definition is "System.Collections.Generic.Queue" or "System.Collections.Generic.Stack")
            FormatterOrderedCollectionTraits.Capture(compilation, named, definition == "System.Collections.Generic.Stack", result);
        if (definition == "System.Collections.Generic.Queue" && FormatterRuntimeTraits.HasJsonArrayCapacityHint(compilation))
            result.Add("QueueJsonCapacity");
        if (definition == "System.Collections.Generic.LinkedList" && named.TypeArguments[0].IsUnmanagedType &&
            FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation))
            result.Add("LinkedListRawWindow");
        if (definition is "System.Collections.ObjectModel.Collection" or "System.Collections.ObjectModel.ObservableCollection" or
            "System.Collections.ObjectModel.ReadOnlyCollection" or "System.Collections.ObjectModel.ReadOnlyObservableCollection" or
            "System.Runtime.CompilerServices.ReadOnlyCollectionBuilder")
        {
            var element = named.TypeArguments[0];
            if (definition == "System.Collections.ObjectModel.ReadOnlyCollection" && modern && !unity &&
                element.SpecialType != SpecialType.System_Boolean &&
                FormatterRuntimeTraits.HasCollectionTransfer(compilation, named, "LuminPack.Code.LuminPackCollectionAccess",
                    "TryReadArrayBackedWrapper", "LuminPackReader", SpecialType.System_Boolean, takesCount: true))
                result.Add("ReadArrayBackedWrapper");
            if (FormatterRuntimeTraits.HasJsonArrayCapacityHint(compilation))
                result.Add("WrapperJsonCapacity");
            if (definition == "System.Runtime.CompilerServices.ReadOnlyCollectionBuilder" && FormatterRuntimeTraits.HasBuilderTraversal(compilation, named))
                result.Add("WrapperBuilderTraversal");
            if (definition != "System.Runtime.CompilerServices.ReadOnlyCollectionBuilder" &&
                FormatterRuntimeTraits.HasSequenceStorage(compilation))
            {
                result.Add("WrapperJsonStorage");
                if (FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation))
                    result.Add("WrapperArrayWrite");
            }
            if (element.SpecialType == SpecialType.System_String && FormatterRuntimeTraits.HasWrappedStringWriter(compilation))
                result.Add("WriteWrappedStrings");
            if (compilation.GetTypeByMetadataName("System.Collections.Generic.IEnumerable`1") is { } enumerable)
            {
                if (element.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 &&
                    FormatterRuntimeTraits.HasCollectionTransfer(compilation, enumerable, "LuminPack.Code.LuminPackCollectionAccess",
                        "TryWriteWrappedIntegers", "LuminPackJsonWriter", SpecialType.System_Boolean, takesCount: false))
                    result.Add("WriteWrappedIntegers");
            }
        }
        if (definition is "System.Collections.ObjectModel.ObservableCollection" or "System.Collections.ObjectModel.ReadOnlyObservableCollection")
        {
            if (FormatterRuntimeTraits.HasObservableAppend(compilation))
                result.Add("AppendObservable");
        }
        if (definition == "System.Collections.Generic.PriorityQueue" && FormatterOrderedCollectionTraits.HasPriorityTraversal(compilation, named))
        {
            result.Add("PriorityQueueTraversal");
            if (FormatterOrderedCollectionTraits.HasPriorityReader(compilation, named))
                result.Add("ReadPriorityQueueStorage");
            if (FormatterOrderedCollectionTraits.HasPriorityWriter(compilation, named))
                result.Add("WritePriorityQueueStorage");
            if ((named.TypeArguments[0].SpecialType == SpecialType.System_String || named.TypeArguments[1].SpecialType == SpecialType.System_String) &&
                FormatterRuntimeTraits.HasJsonArrayCapacityHint(compilation))
                result.Add("PriorityJsonCapacity");
            if (FormatterConcurrentTraits.HasJsonIntegerPairs(compilation))
                result.Add("JsonIntegerPair");
        }
        if (definition is "System.Collections.Generic.List" or "System.Memory" or "System.ReadOnlyMemory" or
            "System.ArraySegment" or "System.Buffers.ReadOnlySequence")
            ContiguousCapabilities(compilation, named.TypeArguments[0], result);
        if (definition == "System.Buffers.ReadOnlySequence" && named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteCheckedStrings", named.TypeArguments[0]))
            result.Add("WriteCheckedStringSpan");
        if (definition.StartsWith("System.Collections.Immutable.", StringComparison.Ordinal))
        {
            string concrete = named.Name.StartsWith("IImmutable", StringComparison.Ordinal) ? named.Name.Substring(1) : named.Name;
            if (concrete == "ImmutableSet")
                concrete = "ImmutableHashSet";
            if (concrete.StartsWith("Immutable", StringComparison.Ordinal) && FormatterImmutableTraits.Available(compilation, concrete.Substring(9), named.Arity))
                result.Add("ImmutableBuilder");
            if (concrete is "ImmutableQueue" or "ImmutableList" && FormatterRuntimeTraits.CanCreateImmutableSequenceFromSpan(compilation, concrete))
                result.Add("ImmutableSpan");
            if (concrete == "ImmutableQueue" && FormatterImmutableTraits.HasQueueTraversal(compilation))
            {
                result.Add("ImmutableQueueTraversal");
                ContiguousCapabilities(compilation, named.TypeArguments[0], result);
                if (FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation))
                    result.Add("ImmutableQueueRawWindow");
            }
            if (definition == "System.Collections.Immutable.ImmutableList" && FormatterImmutableTraits.HasRawListWriter(compilation))
                result.Add("ImmutableRawListWrite");
            if (concrete == "ImmutableList" && FormatterImmutableTraits.HasTreeCopy(compilation, concrete, 1))
            {
                result.Add("CopyImmutableListTree");
                ContiguousCapabilities(compilation, named.TypeArguments[0], result);
                if (FormatterImmutableTraits.HasRawListWriter(compilation))
                    result.Add("WriteRawImmutableList");
            }
            if (definition is "System.Collections.Immutable.IImmutableSet" or "System.Collections.Immutable.ImmutableHashSet" or
                "System.Collections.Immutable.ImmutableSortedSet" or "System.Collections.Immutable.IImmutableDictionary" or
                "System.Collections.Immutable.ImmutableDictionary" or "System.Collections.Immutable.ImmutableSortedDictionary")
            {
                string hash = named.Arity == 1 ? "ImmutableHashSet" : "ImmutableDictionary";
                string sorted = named.Arity == 1 ? "ImmutableSortedSet" : "ImmutableSortedDictionary";
                if ((concrete == hash || named.Name.StartsWith("IImmutable", StringComparison.Ordinal)) &&
                    FormatterImmutableTraits.HasTreeCopy(compilation, hash, named.Arity))
                    result.Add("CopyImmutableHashTree");
                if ((concrete == sorted || named.Name.StartsWith("IImmutable", StringComparison.Ordinal)) &&
                    FormatterImmutableTraits.HasTreeCopy(compilation, sorted, named.Arity))
                    result.Add("CopyImmutableSortedTree");
                if (named.Arity == 1)
                {
                    ContiguousCapabilities(compilation, named.TypeArguments[0], result);
                    foreach (string family in new[] { hash, sorted })
                        if (compilation.GetTypeByMetadataName("System.Collections.Immutable." + family + "`1") is { } tree &&
                            FormatterImmutableTraits.HasRawTreeWriter(compilation, tree))
                            result.Add("WriteRaw" + family);
                }
            }
            var element = named.TypeArguments[0];
            bool frameworkElement = SymbolEqualityComparer.Default.Equals(element.ContainingAssembly,
                compilation.GetSpecialType(SpecialType.System_Object).ContainingAssembly) &&
                (element.SpecialType == SpecialType.System_String || CallbackFree(element));
            if (frameworkElement && (concrete is "ImmutableHashSet" or "ImmutableSortedSet"))
            {
                if (concrete == "ImmutableHashSet" &&
                    FormatterImmutableTraits.HasSetFactory(compilation, concrete, "CreateFrameworkImmutableHashSet"))
                    result.Add("BuildFrameworkImmutableHashSet");
                if (concrete == "ImmutableSortedSet" &&
                    FormatterImmutableTraits.HasSetFactory(compilation, concrete, "CreateOrderedSetIfSupported"))
                    result.Add("BuildOrderedImmutableSortedSet");
            }
            if (definition == "System.Collections.Immutable.ImmutableSortedSet" &&
                FormatterImmutableTraits.HasSetFactory(compilation, concrete, "CreateImmutableSortedSet"))
                result.Add("BuildBinaryImmutableSortedSet");
            if (definition == "System.Collections.Immutable.ImmutableHashSet" &&
                FormatterImmutableTraits.HasSetFactory(compilation, concrete, "CreateImmutableHashSet"))
                result.Add("BuildBinaryImmutableHashSet");
            if (modern && !unity && definition == "System.Collections.Immutable.ImmutableHashSet" &&
                FormatterImmutableTraits.IsIdentityHashElement(compilation, element) &&
                FormatterImmutableTraits.HasIdentityHashSetFactory(compilation))
                result.Add("BuildIdentityImmutableHashSet");
            if (element.SpecialType == SpecialType.System_String &&
                FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteStrings", element))
                result.Add("ImmutableStringRun");
        }

        if (definition == "System.Collections.Generic.Dictionary" && named.TypeArguments[0].SpecialType == SpecialType.System_String && FormatterRuntimeTraits.HasEnumeratedStringKeyCheck(compilation))
            result.Add("EnumeratedStringKey");
        if (definition == "System.Collections.Generic.Dictionary" && modern && !unity && CallbackFree(named.TypeArguments[0]))
        {
            ITypeSymbol value = named.TypeArguments[1];
            if (value.SpecialType == SpecialType.System_String || CallbackFree(value))
                result.Add("DictionaryScalarSlots");
            else if (Leaf(value, compilation))
                result.Add("DictionaryLeafSlots");
            else if (value is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: true, IsGenericType: false } root)
            {
                // As the original slot plan, an abstract class Union root refills its existing exact fixed cases in place; the
                // slot emitter selects those cases and every other value takes the staged continuation.
                var cases = root.GetAttributes().Where(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute").ToArray();
                if (cases.Any(attribute => attribute.ConstructorArguments.Length >= 2 && attribute.ConstructorArguments[0].Value is ushort id && id < 250 && attribute.ConstructorArguments[1].Value is INamedTypeSymbol { TypeKind: TypeKind.Class, IsAbstract: false, IsGenericType: false }))
                    result.Add("DictionaryUnionSlots");
            }
        }

        if (SequenceInterface(definition))
        {
            if (FormatterRuntimeTraits.HasJsonArrayCapacityHint(compilation))
                result.Add("InterfaceJsonCapacity");
            ITypeSymbol element = named.TypeArguments[0];
            if (element.SpecialType == SpecialType.System_String)
            {
                if (FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteStrings", element))
                    result.Add("WriteInterfaceStrings");
                if (FormatterRuntimeTraits.HasStringSpanReader(compilation, element))
                    result.Add("ReadInterfaceStrings");
            }
            bool ordinary = element is INamedTypeSymbol { TypeKind: TypeKind.Class } payload && payload.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackableAttribute" && (attribute.ConstructorArguments.Length == 0 || Convert.ToInt32(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture) == 0)) && !payload.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute");
            if (element.SpecialType == SpecialType.System_String || element.IsUnmanagedType && (element.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) || ordinary)
                result.Add("InterfaceTargetReuse");
        }

        if (definition == "System.Collections.Generic.HashSet")
        {
            var element = named.TypeArguments[0];
            if (FormatterRuntimeTraits.CanGroupSmallHashSetWrites(element, compilation))
                result.Add("SmallHashSetWrite");
            if (modern && !unity && RawCallbackFreeSetElement(element) && FormatterRuntimeTraits.HasCollectionTransfer(compilation, named,
                    "LuminPack.Code.LuminPackMarshal", "ReadRawHashSet", "LuminPackReader", SpecialType.System_Void, takesCount: true))
                result.Add("ReadRawHashSet");
        }
        else if (definition is "System.Collections.Generic.ISet" or "System.Collections.Generic.IReadOnlySet" && modern && !unity &&
            RawCallbackFreeSetElement(named.TypeArguments[0]) &&
            compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1") is { } setStorage &&
            FormatterRuntimeTraits.HasCollectionTransfer(compilation, setStorage.Construct(named.TypeArguments[0]),
                "LuminPack.Code.LuminPackMarshal", "ReadRawHashSet", "LuminPackReader", SpecialType.System_Void, takesCount: true))
            result.Add("ReadRawHashSet");
        if (definition == "System.Collections.Generic.SortedSet")
        {
            var element = named.TypeArguments[0];
            if (FormatterRuntimeTraits.CanBuildSortedSet(element, compilation))
                result.Add("BuildSortedSet");
            if (FormatterRuntimeTraits.CanRebuildSortedSet(element, compilation))
                result.Add("RebuildSortedSet");
            if (element.SpecialType == SpecialType.System_String &&
                FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteCheckedStrings", element))
                result.Add("WriteCheckedStringSpan");
            if (element.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64)
            {
                if (FormatterRuntimeTraits.HasCollectionTransfer(compilation, named, "LuminPack.Utility.LuminTreeStorage",
                        "TryWriteIntegerSortedSetJson", "LuminPackJsonWriter", SpecialType.System_Boolean, takesCount: false))
                    result.Add("SortedSetIntegerTraversal");
                if (FormatterRuntimeTraits.HasCollectionTransfer(compilation, named, "LuminPack.Utility.LuminTreeStorage",
                        "TryReadIntegerSortedSetJson", "LuminPackJsonReader", SpecialType.System_Boolean, takesCount: false))
                    result.Add("SortedSetIntegerConstruction");
            }
        }

        if (definition == "System.Collections.Generic.SortedList")
        {
            if (FormatterRuntimeTraits.HasSortedListTraversal(compilation))
                result.Add("SortedListTraversal");
            if (FormatterRuntimeTraits.CanAppendSortedListOrdered(named.TypeArguments[0], compilation))
                result.Add("AppendSortedList");
            ITypeSymbol key = named.TypeArguments[0];
            if (key.SpecialType == SpecialType.System_String || key.TypeKind == TypeKind.Enum || key.IsUnmanagedType && (key.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))
                result.Add("MatchSortedListKey");
        }

        if (definition == "System.Collections.Concurrent.ConcurrentBag")
        {
            if (FormatterConcurrentTraits.HasBagSnapshot(compilation, false))
                result.Add("BagSnapshot");
            if (FormatterConcurrentTraits.HasBagSnapshot(compilation, true))
                result.Add("BagRawWrite");
        }

        if (definition is "System.Collections.Concurrent.BlockingCollection" or "System.Collections.Concurrent.ConcurrentQueue" && modern && !unity)
        {
            string factory = "Create" + named.Name;
            if (FormatterConcurrentTraits.HasCountedQueueFactory(compilation, named, factory))
                result.Add(factory);
        }

        if (definition == "System.Collections.Concurrent.ConcurrentQueue" && modern && !unity &&
            FormatterConcurrentTraits.HasSizedConcurrentQueueClear(compilation, named))
            result.Add("ClearConcurrentQueue");

        if (definition is "System.Collections.Concurrent.ConcurrentBag" or "System.Collections.Concurrent.ConcurrentStack" &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteCheckedStrings", named.TypeArguments[0]))
            result.Add("WriteCheckedStringSpan");

        if (definition == "System.Collections.Concurrent.ConcurrentStack" && FormatterConcurrentTraits.CanStageStack(compilation, named.TypeArguments[0]))
            result.Add("StageConcurrentStack");
        if (definition == "System.Collections.Generic.IReadOnlySet" && modern &&
            compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenSet`1") is { } frozenSet &&
            frozenSet.Construct(named.TypeArguments[0]).AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, named)))
            result.Add("FrozenSetInterfaceTraversal");
        if ((definition is "System.Collections.Concurrent.ConcurrentQueue" or "System.Collections.Concurrent.ConcurrentStack" or "System.Collections.Concurrent.ConcurrentBag"
            or "System.Collections.Immutable.ImmutableArray" or "System.Collections.Immutable.ImmutableList" or "System.Collections.Immutable.ImmutableQueue"
            or "System.Collections.Immutable.ImmutableStack" or "System.Collections.Immutable.ImmutableHashSet" or "System.Collections.Immutable.ImmutableSortedSet" ||
            SequenceInterface(definition) || definition is "System.Collections.Generic.ISet" or "System.Collections.Generic.IReadOnlySet" or
                "System.Collections.Generic.HashSet") &&
            FormatterConcurrentTraits.HasJsonIntegerElement(compilation, named.TypeArguments[0]))
            result.Add("JsonIntegerElement");
        if (definition is "System.Collections.Concurrent.ConcurrentDictionary" or "System.Collections.Immutable.ImmutableDictionary" or
            "System.Collections.Immutable.ImmutableSortedDictionary" or "System.Collections.Generic.IDictionary" or
            "System.Collections.Generic.IReadOnlyDictionary" or "System.Collections.Immutable.IImmutableDictionary" or
            "System.Collections.Generic.Dictionary" or "System.Collections.Generic.SortedDictionary" or "System.Collections.Generic.SortedList" &&
            FormatterConcurrentTraits.HasJsonIntegerPairs(compilation))
            result.Add("JsonIntegerPair");
        if (definition is "System.Collections.Generic.Dictionary" or "System.Collections.Generic.IDictionary" or "System.Collections.Generic.IReadOnlyDictionary" or
            "System.Collections.Generic.SortedList" or "System.Collections.Generic.SortedDictionary" &&
            FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation))
            result.Add("BinaryRawPairs");
        if (definition == "System.Collections.Concurrent.ConcurrentDictionary" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].IsUnmanagedType &&
            FormatterConcurrentTraits.HasBinaryTextPairWriter(compilation))
            result.Add("ConcurrentBinaryTextPair");
        if ((definition is "System.Collections.Generic.Dictionary" or "System.Collections.Generic.IDictionary" or
             "System.Collections.Generic.IReadOnlyDictionary" or "System.Collections.Generic.SortedList" or
             "System.Collections.Generic.SortedDictionary" or "System.Collections.Immutable.ImmutableDictionary" or
             "System.Collections.Immutable.ImmutableSortedDictionary" or "System.Collections.Immutable.IImmutableDictionary" or
             "System.Collections.Frozen.FrozenDictionary" or "System.Collections.Generic.PriorityQueue" or
             "System.Collections.ObjectModel.ReadOnlyDictionary") && modern && !unity &&
            (definition != "System.Collections.ObjectModel.ReadOnlyDictionary" ||
                named.TypeArguments.All(argument => argument.SpecialType != SpecialType.System_Boolean)) &&
            ((named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].IsUnmanagedType) ||
             (named.TypeArguments[1].SpecialType == SpecialType.System_String && named.TypeArguments[0].IsUnmanagedType) ||
             ((definition is "System.Collections.Generic.IDictionary" or "System.Collections.Generic.IReadOnlyDictionary") &&
              named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].SpecialType == SpecialType.System_String)) &&
            FormatterConcurrentTraits.HasBinaryTextPairWriter(compilation))
            result.Add("BinaryTextPair");
        if (definition is "System.Collections.Generic.IDictionary" or "System.Collections.Generic.IReadOnlyDictionary" &&
            modern && !unity && named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            compilation.GetTypeByMetadataName("System.Collections.Generic.Dictionary`2") is { } dictionary &&
            FormatterConcurrentTraits.HasDictionaryView(compilation, dictionary))
            result.Add("DenseDictionaryView");
        if (definition.StartsWith("System.Collections.Concurrent.Concurrent", StringComparison.Ordinal) && FormatterConcurrentTraits.Available(compilation, named.Name, named.Arity))
            result.Add("ConcurrentTraversal");
        if (definition == "System.Collections.Frozen.FrozenSet")
        {
            var element = named.TypeArguments[0];
            ContiguousCapabilities(compilation, element, result);
            if (element.SpecialType == SpecialType.System_String &&
                FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteStrings", element))
                result.Add("WriteFrozenStrings");
            if (FormatterFrozenTraits.Available(compilation, "Set", 1))
                result.Add("RentFrozenSet");
            if (RawCallbackFreeSetElement(element) && compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1") is { } hashSet &&
                FormatterRuntimeTraits.HasCollectionTransfer(compilation, hashSet, "LuminPack.Utility.LuminFrozenConstruction",
                    "AddRawSetItems", "LuminPackReader", SpecialType.System_Void, takesCount: true))
            {
                result.Add("ReadFrozenRawSetItems");
                if (modern && !unity && FormatterFrozenTraits.CanReturnRawSet(compilation, hashSet))
                    result.Add("ReturnRawFrozenSet");
                if (modern && !unity && FormatterFrozenTraits.CanReadRawSetReuse(compilation, named))
                    result.Add("ReadRawFrozenSetReuse");
            }
        }
        if (definition == "System.Collections.Frozen.FrozenDictionary")
        {
            if (FormatterFrozenTraits.Available(compilation, "Dictionary", 2))
                result.Add("RentFrozenDictionary");
            if (FormatterFrozenTraits.CanWritePairs(compilation))
                result.Add("FrozenRawPairs");
        }

        return result.ToArray();
    }

    /// <summary>Identifies the five sequence interfaces whose exact list and array lanes share traversal.</summary>
    /// <param name="definition">The open framework definition name.</param>
    /// <returns>Whether this is a sequence interface that can hold a list or array.</returns>
    internal static bool SequenceInterface(string definition) => definition is "System.Collections.Generic.IEnumerable" or
        "System.Collections.Generic.ICollection" or "System.Collections.Generic.IReadOnlyCollection" or
        "System.Collections.Generic.IList" or "System.Collections.Generic.IReadOnlyList";

    /// <summary>Captures optional kernels for the exact element types of contiguous sequences.</summary>
    /// <param name="compilation">The actual consumer metadata.</param>
    /// <param name="element">The sequence element.</param>
    /// <param name="result">The resulting capabilities.</param>
    private static void ContiguousCapabilities(Compilation compilation, ITypeSymbol element, List<string> result)
    {
        if (element.SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 &&
            FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackJsonWriter", "WriteIntegerRun", element, withCaptureOrder: true))
            result.Add("JsonIntegerRun");
    }

    /// <summary>Recognizes a callback-free fixed framework key or a raw enum.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    private static bool CallbackFree(ITypeSymbol type) => type is INamedTypeSymbol { IsValueType: true, IsGenericType: false } && (type.TypeKind == TypeKind.Enum || type.IsUnmanagedType && (type.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))));

    // These element moves preserve every wire byte and the default hash cannot invoke application code.
    // Boolean retains canonical validation. The caller separately selects the raw wire representation.
    /// <summary>Handles raw callback free set element for <c>FormatterCapabilities</c>.</summary>
    /// <param name="type">The type being analyzed or emitted.</param>
    /// <returns>Whether the input satisfies the raw callback free set element check.</returns>
    private static bool RawCallbackFreeSetElement(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Enum || type.SpecialType is SpecialType.System_Byte or SpecialType.System_SByte or
        SpecialType.System_Char or SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or
        SpecialType.System_UInt32 or SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_IntPtr or
        SpecialType.System_UIntPtr or SpecialType.System_Single or SpecialType.System_Double;
    /// <summary>Certifies a nonnull sealed leaf whose read cannot invoke user code while a dictionary entry ref is retained.</summary>
    /// <param name = "type">The declared type or its source-ready spelling.</param>
    /// <param name = "compilation">The consuming compilation and its runtime references.</param>
    /// <returns>Whether the requested contract or capability is available.</returns>
    private static bool Leaf(ITypeSymbol type, Compilation compilation)
    {
        if (type is not INamedTypeSymbol { TypeKind: TypeKind.Class, IsSealed: true, IsGenericType: false } named || named.BaseType?.SpecialType != SpecialType.System_Object || !SymbolEqualityComparer.Default.Equals(named.ContainingAssembly, compilation.Assembly))
            return false;
        var attributes = named.GetAttributes();
        var packable = attributes.FirstOrDefault(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackableAttribute");
        if (packable == null || packable.ConstructorArguments.Length != 0 && Convert.ToInt32(packable.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture)is not (0 or 1) || attributes.Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute"))
            return false;
        var fields = named.GetMembers().OfType<IFieldSymbol>().Where(field => !field.IsStatic && !field.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackIgnoreAttribute")).ToArray();
        return fields.Length != 0 && fields.All(field => !field.IsImplicitlyDeclared && !field.IsVolatile && (field.Type.SpecialType == SpecialType.System_String || field.Type.IsUnmanagedType && field.Type.SpecialType != SpecialType.None) && !field.GetAttributes().Any(attribute => attribute.AttributeClass?.Name.IndexOf("Compress", StringComparison.Ordinal) >= 0)) && !named.GetMembers().OfType<IMethodSymbol>().Any(method => method.GetAttributes().Any(attribute => attribute.AttributeClass?.Name.StartsWith("LuminPackOn", StringComparison.Ordinal) == true && attribute.AttributeClass.ContainingNamespace.ToDisplayString() == "LuminPack.Attribute"));
    }
}
