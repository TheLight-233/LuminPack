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
            if (array.Rank > 1 && array.ElementType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } &&
                FormatterRuntimeTraits.HasFramedNullableSpanWriter(compilation))
                result.Add("WriteFramedNullableSpan");
            if (array.Rank > 1 && modern && !unity && FormatterRuntimeTraits.HasFramedNullableInt32Reader(compilation, array.ElementType))
                result.Add("ReadFramedNullableInt32s");
        }
        if (type is not INamedTypeSymbol { IsGenericType: true } named)
            return result.ToArray();
        string definition = named.OriginalDefinition.ContainingNamespace.ToDisplayString() + "." + named.Name;
        if (modern && !unity && named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T &&
            named.TypeArguments[0].IsUnmanagedType && FormatterRuntimeTraits.HasNativeNullablePayloadReader(compilation))
            result.Add("ReadNativeNullablePayload");
        if (definition == "System.Collections.Concurrent.ConcurrentQueue" && modern && !unity &&
            FormatterRuntimeTraits.HasFramedNullableInt32Reader(compilation, named.TypeArguments[0]))
            result.Add("ReadFramedNullableInt32s");
        if ((SequenceInterface(definition) || definition.StartsWith("System.Collections.Immutable.", StringComparison.Ordinal) ||
            definition is "System.Collections.Generic.Stack" or "System.Collections.Generic.SortedSet" or "System.Collections.Generic.LinkedList" or
                "System.Collections.Concurrent.ConcurrentStack" or "System.Collections.Concurrent.ConcurrentBag" or "System.Collections.Frozen.FrozenSet") &&
            FormatterRuntimeTraits.HasStaging(compilation))
            result.Add("CollectionStaging");
        if (definition == "System.Collections.Generic.HashSet" &&
            named.TypeArguments[0].IsReferenceType && named.TypeArguments[0].SpecialType != SpecialType.System_String &&
            FormatterOrderedCollectionTraits.HasHashSetTraversal(compilation))
            result.Add("HashSetTraversal");
        if (definition is "System.Collections.Generic.Queue" or "System.Collections.Generic.Stack")
            FormatterOrderedCollectionTraits.Capture(compilation, named, definition == "System.Collections.Generic.Stack", result);
        if (definition == "System.Collections.Generic.Queue" && FormatterRuntimeTraits.HasJsonArrayCapacityHint(compilation))
            result.Add("QueueJsonCapacity");
        if (definition == "System.Collections.Generic.LinkedList")
        {
            var element = named.TypeArguments[0];
            if (element.IsUnmanagedType && FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation))
                result.Add("LinkedListRawWindow");
            bool text = element.SpecialType == SpecialType.System_String;
            if (modern && !unity && (text || element.IsUnmanagedType) &&
                FormatterConcurrentTraits.HasLinkedListRingWriter(compilation, named, text))
                result.Add("LinkedListRingWriter");
        }
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
        if (definition == "System.Collections.ObjectModel.ReadOnlyDictionary" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            named.TypeArguments[1].SpecialType is SpecialType.System_Int32 or SpecialType.System_String &&
            FormatterRuntimeTraits.HasStableReadOnlyDictionaryRead(compilation, named))
            result.Add("StableReadOnlyDictionaryRead");
        if (definition == "System.Collections.ObjectModel.ReadOnlyDictionary" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            (named.TypeArguments[1].IsReferenceType || named.TypeArguments[1].IsUnmanagedType) &&
            FormatterRuntimeTraits.HasReadOnlyStringDictionaryStorage(compilation, named) &&
            FormatterConcurrentTraits.HasBinaryTextPairWriter(compilation))
            result.Add("ReadOnlyStringDictionaryStorage");
        if (definition == "System.Collections.Generic.PriorityQueue" && FormatterOrderedCollectionTraits.HasPriorityTraversal(compilation, named))
        {
            result.Add("PriorityQueueTraversal");
            if (named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].SpecialType == SpecialType.System_Int32)
                result.Add("PriorityQueueStringReuse");
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
        if (definition == "System.Memory" && modern && !unity && FormatterRuntimeTraits.HasMemoryStorageInitializer(compilation))
            result.Add("InitializeMemoryStorage");
        if (definition == "System.Buffers.ReadOnlySequence" && named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteCheckedStrings", named.TypeArguments[0]))
            result.Add("WriteCheckedStringSpan");
        if (definition.StartsWith("System.Collections.Immutable.", StringComparison.Ordinal))
        {
            string concrete = named.Name.StartsWith("IImmutable", StringComparison.Ordinal) ? named.Name.Substring(1) : named.Name;
            if (concrete == "ImmutableArray" && modern && !unity && named.TypeArguments[0].IsUnmanagedType &&
                named.TypeArguments[0].OriginalDefinition.SpecialType != SpecialType.System_Nullable_T &&
                FormatterImmutableTraits.HasRawArrayReader(compilation))
            {
                result.Add("ReadRawImmutableArray");
                if (FormatterImmutableTraits.HasRawArrayReader(compilation, reuse: true))
                    result.Add("ReuseRawImmutableArray");
            }
            if (concrete == "ImmutableSet")
                concrete = "ImmutableHashSet";
            if (concrete.StartsWith("Immutable", StringComparison.Ordinal) && FormatterImmutableTraits.Available(compilation, concrete.Substring(9), named.Arity))
                result.Add("ImmutableBuilder");
            if (concrete is "ImmutableQueue" or "ImmutableList" && FormatterRuntimeTraits.CanCreateImmutableSequenceFromSpan(compilation, concrete))
            {
                result.Add("ImmutableSpan");
                if (concrete == "ImmutableQueue" && modern && !unity && FormatterImmutableTraits.HasStagedFactory(compilation,
                    concrete, "CreateImmutableQueue", "LuminPack.Code.LuminPackCollectionAccess"))
                    result.Add("ImmutableQueueConstruction");
            }
            if (concrete == "ImmutableQueue" && FormatterImmutableTraits.HasQueueTraversal(compilation))
            {
                result.Add("ImmutableQueueTraversal");
                ContiguousCapabilities(compilation, named.TypeArguments[0], result);
                if (FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation))
                    result.Add("ImmutableQueueRawWindow");
            }
            if ((concrete == "ImmutableStack" || definition == "System.Collections.Immutable.IImmutableQueue") &&
                modern && !unity && named.TypeArguments[0].IsUnmanagedType && FormatterConcurrentTraits.HasRawCollectionWriter(compilation,
                    "WriteImmutableRawStack", "System.Collections.Immutable.ImmutableStack`1", SpecialType.System_Int32, checksCapacity: true))
                result.Add("BoundedImmutableRawStack");
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
                if (named.Arity == 2 && modern && !unity &&
                    named.TypeArguments.All(argument => argument.SpecialType == SpecialType.System_String) &&
                    FormatterImmutableTraits.HasBinaryStringPairPrefix(compilation))
                    result.Add("BinaryStringPairPrefix");
                if (named.Arity == 1)
                {
                    if (concrete == "ImmutableHashSet" && named.TypeArguments[0].SpecialType == SpecialType.System_String &&
                        FormatterRuntimeTraits.HasStringSpanReader(compilation, named.TypeArguments[0]))
                        result.Add("ReadSetStringTail");
                    if (concrete == "ImmutableSortedSet" && named.TypeArguments[0].IsUnmanagedType && FormatterImmutableTraits.HasRawSetMatch(compilation, named))
                        result.Add("MatchRawSet");
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
                    FormatterImmutableTraits.HasStagedFactory(compilation, concrete, "CreateFrameworkImmutableHashSet"))
                    result.Add("BuildFrameworkImmutableHashSet");
                if (concrete == "ImmutableSortedSet" &&
                    FormatterImmutableTraits.HasStagedFactory(compilation, concrete, "CreateOrderedSetIfSupported"))
                    result.Add("BuildOrderedImmutableSortedSet");
            }
            if (definition == "System.Collections.Immutable.ImmutableSortedSet" &&
                FormatterImmutableTraits.HasStagedFactory(compilation, concrete, "CreateImmutableSortedSet"))
                result.Add("BuildBinaryImmutableSortedSet");
            if (definition == "System.Collections.Immutable.ImmutableHashSet" &&
                FormatterImmutableTraits.HasStagedFactory(compilation, concrete, "CreateImmutableHashSet"))
                result.Add("BuildBinaryImmutableHashSet");
            if (modern && !unity && definition == "System.Collections.Immutable.ImmutableHashSet" &&
                FormatterImmutableTraits.IsIdentityHashElement(compilation, element) &&
                FormatterImmutableTraits.HasIdentityHashSetFactory(compilation))
                result.Add("BuildIdentityImmutableHashSet");
            if (element.SpecialType == SpecialType.System_String &&
                FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteStrings", element))
                result.Add("ImmutableStringRun");
        }

        if (definition == "System.Collections.Immutable.ImmutableDictionary" &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].SpecialType == SpecialType.System_Int32 &&
            FormatterImmutableTraits.HasTreeCopy(compilation, "ImmutableDictionary", 2) &&
            FormatterRuntimeTraits.HasEnumeratedStringKeyCheck(compilation))
            result.Add("SeedImmutableStringKeys");
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
                if (FormatterRuntimeTraits.HasWrappedStringWriter(compilation))
                    result.Add("WriteWrappedStrings");
            }
            if (FormatterRuntimeTraits.HasSequenceStorage(compilation))
                result.Add("InterfaceWrapperStorage");
            bool ordinary = element is INamedTypeSymbol { TypeKind: TypeKind.Class } payload && payload.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackableAttribute" && (attribute.ConstructorArguments.Length == 0 || Convert.ToInt32(attribute.ConstructorArguments[0].Value, System.Globalization.CultureInfo.InvariantCulture) == 0)) && !payload.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == "LuminPack.Attribute.LuminPackUnionAttribute");
            if (element.SpecialType == SpecialType.System_String || element.IsUnmanagedType && (element.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(element.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))) || ordinary)
                result.Add("InterfaceTargetReuse");
        }
        if (definition is "System.Linq.IGrouping" or "System.Linq.ILookup" && FormatterRuntimeTraits.HasStaging(compilation) &&
            compilation.GetTypeByMetadataName("System.Linq.IGrouping`2") is { } grouping &&
            FormatterRuntimeTraits.HasFrameworkGroupingCopy(compilation, grouping) &&
            (named.TypeArguments[1].SpecialType != SpecialType.System_String ||
                FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteStrings", named.TypeArguments[1])))
            result.Add("CopyFrameworkGrouping");

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
        if (definition is "System.Collections.Generic.HashSet" or "System.Collections.Generic.ISet" or "System.Collections.Generic.IReadOnlySet" &&
            named.TypeArguments[0] is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable &&
            nullable.TypeArguments[0].IsUnmanagedType && compilation.GetTypeByMetadataName("System.Collections.Generic.HashSet`1") is { } nullableSet)
        {
            var storage = nullableSet.Construct(nullable);
            if (FormatterRuntimeTraits.HasCollectionTransfer(compilation, storage,
                "LuminPack.Code.LuminPackMarshal", "WriteNullableHashSetRaw", "LuminPackWriter", SpecialType.System_Void,
                takesCount: false, nullableElement: true))
                result.Add("WriteNullableHashSetRaw");
            if (modern && !unity && RawCallbackFreeSetElement(nullable.TypeArguments[0]) &&
                FormatterRuntimeTraits.HasCollectionTransfer(compilation, storage,
                    "LuminPack.Code.LuminPackMarshal", "ReadNullableHashSetRaw", "LuminPackReader", SpecialType.System_Void,
                    takesCount: true, nullableElement: true))
                result.Add("ReadNullableHashSetRaw");
        }
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
            if (named.TypeArguments[0].SpecialType == SpecialType.System_String && FormatterRuntimeTraits.HasSortedListArrays(compilation, named))
                result.Add("SortedListArrays");
            if (FormatterRuntimeTraits.HasSortedListTraversal(compilation))
                result.Add("SortedListTraversal");
            if (FormatterRuntimeTraits.CanAppendSortedListOrdered(named.TypeArguments[0], compilation))
                result.Add("AppendSortedList");
            ITypeSymbol key = named.TypeArguments[0];
            if (modern && !unity && key.SpecialType == SpecialType.System_String &&
                named.TypeArguments[1].SpecialType is SpecialType.System_Int32 or SpecialType.System_Int64 &&
                FormatterRuntimeTraits.HasSeededSortedListKeys(compilation, named))
                result.Add("SeededSortedListKeys");
            if (key.SpecialType == SpecialType.System_String || key.TypeKind == TypeKind.Enum || key.IsUnmanagedType && (key.SpecialType != SpecialType.None || Rendering.Formatters.FrameworkScalarBody.Contains(key.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat))))
                result.Add("MatchSortedListKey");
        }

        if (definition == "System.Collections.Concurrent.ConcurrentBag")
        {
            if (modern && !unity && FormatterConcurrentTraits.HasBagFreshSizing(compilation, named))
                result.Add("BagFreshSizing");
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
        if (definition == "System.Collections.Concurrent.ConcurrentQueue" && FormatterConcurrentTraits.HasQueueDrain(compilation, named))
            result.Add("DrainConcurrentQueue");
        if (definition == "System.Collections.Concurrent.ConcurrentQueue" && named.TypeArguments[0].IsUnmanagedType &&
            FormatterConcurrentTraits.HasQueueRefill(compilation, named))
            result.Add("RefillConcurrentQueue");

        if (definition is "System.Collections.Concurrent.ConcurrentBag" or "System.Collections.Concurrent.ConcurrentStack" &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteCheckedStrings", named.TypeArguments[0]))
            result.Add("WriteCheckedStringSpan");

        if (definition == "System.Collections.Concurrent.ConcurrentStack" && FormatterConcurrentTraits.CanStageStack(compilation, named.TypeArguments[0]))
            result.Add("StageConcurrentStack");
        if (definition == "System.Collections.Concurrent.ConcurrentStack" && FormatterConcurrentTraits.HasStackRangePush(compilation, named))
            result.Add("PushConcurrentStackRange");
        if (definition == "System.Collections.Concurrent.ConcurrentStack" && named.TypeArguments[0].IsUnmanagedType &&
            FormatterConcurrentTraits.HasStackRawWrite(compilation, named))
            result.Add("WriteConcurrentStack");
        if (definition is "System.Collections.Generic.ISet" or "System.Collections.Generic.IReadOnlySet" && modern)
        {
            var element = named.TypeArguments[0];
            bool raw = !unity && element.IsUnmanagedType && element.SpecialType != SpecialType.System_Boolean &&
                FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation);
            bool text = !unity && element.SpecialType == SpecialType.System_String &&
                FormatterRuntimeTraits.HasReadOnlySpanWriter(compilation, "LuminPackWriter", "WriteStrings", element);
            if (raw && compilation.GetTypeByMetadataName("System.Collections.Generic.SortedSet`1") is { } sorted &&
                FormatterImmutableTraits.HasRawTreeWriter(compilation, sorted))
                result.Add("WriteSetInterfaceRawSorted");
            foreach (string family in new[] { "ImmutableHashSet", "ImmutableSortedSet" })
            {
                if (unity || compilation.GetTypeByMetadataName("System.Collections.Immutable." + family + "`1") is not { } tree ||
                    !FormatterImmutableTraits.CanEnumerateSet(tree, named))
                    continue;
                result.Add(family + "InterfaceTraversal");
                if (raw && FormatterImmutableTraits.HasRawTreeWriter(compilation, tree))
                    result.Add("WriteSetInterfaceRaw" + family);
                if (text && FormatterImmutableTraits.HasTreeCopy(compilation, family, 1))
                    result.Add("WriteSetInterfaceStrings" + family);
            }
            if (definition == "System.Collections.Generic.IReadOnlySet" &&
                compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenSet`1")?.Construct(element) is { } frozen &&
                frozen.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, named)))
            {
                result.Add("FrozenSetInterfaceTraversal");
                if (FormatterFrozenTraits.CanReadSetItems(compilation, frozen))
                {
                    if (raw)
                        result.Add("WriteSetInterfaceRawFrozen");
                    if (text)
                        result.Add("WriteSetInterfaceStringsFrozen");
                }
            }
        }
        if (definition is "System.Collections.Generic.HashSet" or "System.Collections.Generic.ISet" or "System.Collections.Generic.IReadOnlySet" &&
            modern && !unity && named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            FormatterConcurrentTraits.HasStringSetWriter(compilation))
            result.Add("StringSetWriter");
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
            "System.Collections.Generic.SortedList" or "System.Collections.Generic.SortedDictionary" or
            "System.Collections.Immutable.ImmutableDictionary" or "System.Collections.Immutable.ImmutableSortedDictionary" or
            "System.Collections.Immutable.IImmutableDictionary" &&
            FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation))
            result.Add("BinaryRawPairs");
        if (definition is "System.Collections.Generic.IDictionary" or "System.Collections.Generic.IReadOnlyDictionary" &&
            modern && !unity && FormatterConcurrentTraits.HasBoundedDenseRawPairs(compilation))
            result.Add("BoundedDenseRawPairs");
        if (definition is "System.Collections.Generic.IDictionary" or "System.Collections.Generic.IReadOnlyDictionary" && modern && !unity)
        {
            bool raw = named.TypeArguments.All(argument => argument.IsUnmanagedType);
            foreach (string family in new[] { "ImmutableDictionary", "ImmutableSortedDictionary" })
            {
                if (!FormatterImmutableTraits.CanEnumerateDictionary(compilation, named, family))
                    continue;
                result.Add(family + "Traversal");
                if (raw && FormatterImmutableTraits.HasTreeCopy(compilation, family, 2))
                    result.Add(family + "RawTraversal");
            }
            if (raw && FormatterConcurrentTraits.HasCapturedRawPairTransfer(compilation, interleave: false))
                result.Add("PackedTreeRawPairs");
        }
        if (definition == "System.Collections.Generic.Dictionary" && modern && !unity &&
            named.TypeArguments.All(argument => argument.IsUnmanagedType && argument.SpecialType != SpecialType.System_Boolean) &&
            FormatterConcurrentTraits.HasRawDictionaryExpansion(compilation))
            result.Add("ExpandDenseRawPairs");
        if (definition is "System.Collections.ObjectModel.ReadOnlyDictionary" or "System.Collections.Generic.IDictionary" or
            "System.Collections.Generic.IReadOnlyDictionary" && modern && !unity &&
            FormatterConcurrentTraits.HasReadOnlyDictionaryRefill(compilation))
        {
            result.Add("ReadOnlyTypedDictionaryStorage");
            if (definition == "System.Collections.ObjectModel.ReadOnlyDictionary" &&
                named.TypeArguments.All(argument => argument.IsUnmanagedType) && FormatterConcurrentTraits.HasBoundedDenseRawPairs(compilation))
                result.Add("BoundedReadOnlyRawPairs");
        }
        if (definition == "System.Collections.Generic.SortedList" && modern && !unity &&
            named.TypeArguments.All(argument => argument.IsUnmanagedType))
        {
            if (FormatterConcurrentTraits.HasCapturedRawPairTransfer(compilation, interleave: true))
                result.Add("InterleavedRawPairs");
            if (named.TypeArguments.All(argument => argument.SpecialType != SpecialType.System_Boolean) &&
                FormatterConcurrentTraits.HasCapturedRawPairTransfer(compilation, interleave: true, read: true))
                result.Add("ReadInterleavedRawPairs");
        }
        if (definition is "System.Collections.Generic.SortedDictionary" or "System.Collections.Immutable.ImmutableDictionary" or
            "System.Collections.Immutable.ImmutableSortedDictionary" or "System.Collections.Immutable.IImmutableDictionary" &&
            modern && !unity && named.TypeArguments.All(argument => argument.IsUnmanagedType) &&
            FormatterConcurrentTraits.HasCapturedRawPairTransfer(compilation, interleave: false))
            result.Add("PackedTreeRawPairs");
        if (definition is "System.Collections.Generic.Dictionary" or "System.Collections.Generic.SortedDictionary" or
            "System.Collections.Immutable.ImmutableDictionary" or "System.Collections.Immutable.ImmutableSortedDictionary" &&
            modern && !unity && named.TypeArguments.All(argument => argument.IsUnmanagedType && argument.SpecialType != SpecialType.System_Boolean) &&
            FormatterConcurrentTraits.HasCapturedRawPairTransfer(compilation, interleave: false, read: true))
            result.Add("ReadPackedRawPairs");
        if (definition is "System.Collections.Generic.SortedDictionary" or "System.Collections.Immutable.ImmutableSortedDictionary" &&
            modern && !unity && named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].IsReferenceType &&
            FormatterConcurrentTraits.HasBinaryTextPairWriter(compilation))
            result.Add("SortedTreeBinaryBoundedKey");
        if (definition == "System.Collections.Generic.SortedDictionary" && modern && !unity &&
            named.TypeArguments.All(argument => argument.SpecialType == SpecialType.System_String) &&
            FormatterImmutableTraits.HasBinaryStringPairPrefix(compilation))
            result.Add("BinaryStringPairPrefix");
        if (definition == "System.Collections.Generic.SortedList" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            FormatterConcurrentTraits.HasBinaryTextPairWriter(compilation))
            result.Add("SortedListBinaryBoundedKey");
        if (definition == "System.Collections.Concurrent.ConcurrentDictionary" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].IsReferenceType &&
            FormatterConcurrentTraits.HasConcurrentDictionaryKeyRefill(compilation, named))
            result.Add("ConcurrentDictionaryKeyRefill");
        if (definition == "System.Collections.Concurrent.ConcurrentDictionary" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String && named.TypeArguments[1].IsUnmanagedType &&
            FormatterConcurrentTraits.HasBinaryTextPairWriter(compilation))
            result.Add("ConcurrentBinaryTextPair");
        if (definition == "System.Collections.Concurrent.ConcurrentDictionary" && modern && !unity &&
            named.TypeArguments.All(argument => argument.IsUnmanagedType) &&
            FormatterConcurrentTraits.HasRawCollectionWriter(compilation,
                "WriteConcurrentRawPairs", "System.Collections.Concurrent.ConcurrentDictionary`2", SpecialType.System_Int32))
            result.Add("ConcurrentRawPairs");
        if (definition is "System.Collections.Concurrent.ConcurrentDictionary" or "System.Collections.Frozen.FrozenDictionary" &&
            modern && !unity && named.TypeArguments.All(argument => argument.IsUnmanagedType && argument.SpecialType != SpecialType.System_Boolean) &&
            FormatterConcurrentTraits.HasRawDictionaryReader(compilation, concurrent: definition == "System.Collections.Concurrent.ConcurrentDictionary"))
            result.Add("ReadBoundedRawDictionary");
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
        if (definition is "System.Collections.Generic.Dictionary" or "System.Collections.Generic.IDictionary" or
            "System.Collections.Generic.IReadOnlyDictionary" or "System.Collections.Concurrent.ConcurrentDictionary" && modern && !unity &&
            named.TypeArguments[0].SpecialType == SpecialType.System_String &&
            (named.TypeArguments[1].SpecialType == SpecialType.System_String ||
             definition != "System.Collections.Concurrent.ConcurrentDictionary" && named.TypeArguments[1].SpecialType == SpecialType.System_Int32) &&
            FormatterConcurrentTraits.HasBinaryTextPairCursor(compilation))
            result.Add("BinaryStringPairCursor");
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
        }
        bool concreteFrozenDictionary = definition == "System.Collections.Frozen.FrozenDictionary";
        if (concreteFrozenDictionary || ((definition is "System.Collections.Generic.IDictionary" or "System.Collections.Generic.IReadOnlyDictionary") &&
            modern && !unity && compilation.GetTypeByMetadataName("System.Collections.Frozen.FrozenDictionary`2") is { } frozenDictionary &&
            frozenDictionary.Construct(named.TypeArguments[0], named.TypeArguments[1]).AllInterfaces.Any(candidate =>
                SymbolEqualityComparer.Default.Equals(candidate, named))))
        {
            if (!concreteFrozenDictionary)
                result.Add("FrozenDictionaryTraversal");
            if (named.TypeArguments.All(argument => argument.IsUnmanagedType) && FormatterFrozenTraits.CanWritePairs(compilation) &&
                (concreteFrozenDictionary || FormatterConcurrentTraits.HasBinaryRawPairWriter(compilation)))
                result.Add("FrozenRawPairs");
            if (modern && !unity && named.TypeArguments.All(argument => argument.SpecialType == SpecialType.System_String) &&
                FormatterFrozenTraits.CanWriteStringPairRuns(compilation))
                result.Add("FrozenStringPairRuns");
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
