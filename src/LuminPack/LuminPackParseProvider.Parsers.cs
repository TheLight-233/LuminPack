using System.Buffers;
using System.Collections.Concurrent;
#if NET8_0_OR_GREATER
using System.Collections.Frozen;
#endif
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using LuminPack.Parsers;

namespace LuminPack;

public static partial class LuminPackParseProvider
{
    private static readonly Dictionary<Type, Type> _parsers =
        new(70)
        {
            //Tuple
            { typeof(Tuple<>), typeof(TupleParser<>) },
            { typeof(ValueTuple<>), typeof(ValueTupleParser<>) },
            { typeof(Tuple<,>), typeof(TupleParser<,>) },
            { typeof(ValueTuple<,>), typeof(ValueTupleParser<,>) },
            { typeof(Tuple<,,>), typeof(TupleParser<,,>) },
            { typeof(ValueTuple<,,>), typeof(ValueTupleParser<,,>) },
            { typeof(Tuple<,,,>), typeof(TupleParser<,,,>) },
            { typeof(ValueTuple<,,,>), typeof(ValueTupleParser<,,,>) },
            { typeof(Tuple<,,,,>), typeof(TupleParser<,,,,>) },
            { typeof(ValueTuple<,,,,>), typeof(ValueTupleParser<,,,,>) },
            { typeof(Tuple<,,,,,>), typeof(TupleParser<,,,,,>) },
            { typeof(ValueTuple<,,,,,>), typeof(ValueTupleParser<,,,,,>) },
            { typeof(Tuple<,,,,,,>), typeof(TupleParser<,,,,,,>) },
            { typeof(ValueTuple<,,,,,,>), typeof(ValueTupleParser<,,,,,,>) },
            { typeof(Tuple<,,,,,,,>), typeof(TupleParser<,,,,,,>) },
            { typeof(ValueTuple<,,,,,,,>), typeof(ValueTupleParser<,,,,,,,>) },
        
            //KnownGenericType
            { typeof(KeyValuePair<,>), typeof(KeyValuePairParser<,>) },
            { typeof(Lazy<>), typeof(LazyParser<>) },
            { typeof(Nullable<>), typeof(NullableParser<>) },
        
            //Array
            // If T[], choose UnmanagedTypeParser or DangerousUnmanagedTypeArrayParser or ArrayParser
            { typeof(ArraySegment<>), typeof(ArraySegmentParser<>) },
            { typeof(Memory<>), typeof(MemoryParser<>) },
            { typeof(ReadOnlyMemory<>), typeof(ReadOnlyMemoryParser<>) },
            { typeof(ReadOnlySequence<>), typeof(ReadOnlySequenceParser<>) },
        
            //Collection
            { typeof(List<>), typeof(ListParser<>) },
            { typeof(Stack<>), typeof(StackParser<>) },
            { typeof(Queue<>), typeof(QueueParser<>) },
            { typeof(LinkedList<>), typeof(LinkedListParser<>) },
            { typeof(HashSet<>), typeof(HashSetParser<>) },
            { typeof(SortedSet<>), typeof(SortedSetParser<>) },
#if NET8_0_OR_GREATER
            { typeof(PriorityQueue<,>), typeof(PriorityQueueParser<,>) },
#endif
            { typeof(ObservableCollection<>), typeof(ObservableCollectionParser<>) },
            { typeof(Collection<>), typeof(CollectionParser<>) },
            { typeof(ConcurrentQueue<>), typeof(ConcurrentQueueParser<>) },
            { typeof(ConcurrentStack<>), typeof(ConcurrentStackParser<>) },
            { typeof(ConcurrentBag<>), typeof(ConcurrentBagParser<>) },
            { typeof(Dictionary<,>), typeof(DictionaryParser<,>) },
            { typeof(SortedDictionary<,>), typeof(SortedDictionaryParser<,>) },
            { typeof(SortedList<,>), typeof(SortedListParser<,>) },
            { typeof(ConcurrentDictionary<,>), typeof(ConcurrentDictionaryParser<,>) },
            { typeof(ReadOnlyCollection<>), typeof(ReadOnlyCollectionParser<>) },
            { typeof(ReadOnlyObservableCollection<>), typeof(ReadOnlyObservableCollectionParser<>) },
            { typeof(BlockingCollection<>), typeof(BlockingCollectionParser<>) },
            { typeof(ReadOnlyCollectionBuilder<>), typeof(ReadOnlyCollectionBuilderParser<>) },
        
#if !UNITY_2021_2_OR_NEWER
            //ImmutableCollection
            { typeof(ImmutableArray<>), typeof(ImmutableArrayParser<>) },
            { typeof(ImmutableList<>), typeof(ImmutableListParser<>) },
            { typeof(ImmutableQueue<>), typeof(ImmutableQueueParser<>) },
            { typeof(ImmutableStack<>), typeof(ImmutableStackParser<>) },
            { typeof(ImmutableDictionary<,>), typeof(ImmutableDictionaryParser<,>) },
            { typeof(ImmutableSortedDictionary<,>), typeof(ImmutableSortedDictionaryParser<,>) },
            { typeof(ImmutableSortedSet<>), typeof(ImmutableSortedSetParser<>) },
            { typeof(ImmutableHashSet<>), typeof(ImmutableHashSetParser<>) },
            { typeof(IImmutableList<>), typeof(InterfaceImmutableListParser<>) },
            { typeof(IImmutableQueue<>), typeof(InterfaceImmutableQueueParser<>) },
            { typeof(IImmutableStack<>), typeof(InterfaceImmutableStackParser<>) },
            { typeof(IImmutableDictionary<,>), typeof(InterfaceImmutableDictionaryParser<,>) },
            { typeof(IImmutableSet<>), typeof(InterfaceImmutableSetParser<>) },
#endif

            //FrozenCollection
#if NET8_0_OR_GREATER
            { typeof(FrozenDictionary<,>), typeof(FrozenDictionaryParser<,>) },
            { typeof(FrozenSet<>), typeof(FrozenSetParser<>) },
#endif
        
            //Interface
            { typeof(IEnumerable<>), typeof(InterfaceEnumerableParser<>) },
            { typeof(ICollection<>), typeof(InterfaceCollectionParser<>) },
            { typeof(IReadOnlyCollection<>), typeof(InterfaceReadOnlyCollectionParser<>) },
            { typeof(IList<>), typeof(InterfaceListParser<>) },
            { typeof(IReadOnlyList<>), typeof(InterfaceReadOnlyListParser<>) },
            { typeof(IDictionary<,>), typeof(InterfaceDictionaryParser<,>) },
            { typeof(IReadOnlyDictionary<,>), typeof(InterfaceReadOnlyDictionaryParser<,>) },
            { typeof(ILookup<,>), typeof(InterfaceLookupParser<,>) },
            { typeof(IGrouping<,>), typeof(InterfaceGroupingParser<,>) },
            { typeof(ISet<>), typeof(InterfaceSetParser<>) },
#if NET8_0_OR_GREATER
            { typeof(IReadOnlySet<>), typeof(InterfaceReadOnlySetParser<>) },
#endif
        };
}